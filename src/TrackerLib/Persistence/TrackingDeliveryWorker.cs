using System.Data.SQLite;
using System.Globalization;

namespace ArchipelagoSphereTracker.Tracking.Persistence;

public sealed record TrackingDeliveryEnvelope(
    long DeliveryId,
    string EventKey,
    string EventType,
    string EventPayloadJson,
    string DestinationType,
    string DestinationId,
    int AttemptNumber);

public sealed record TrackingPublicationResult(string? ExternalReceiptId = null);

public interface ITrackingEventPublisher
{
    /// <summary>
    /// Publishes using EventKey as the idempotency key. Retrying the same key must not create a second publication.
    /// </summary>
    Task<TrackingPublicationResult> PublishAsync(
        TrackingDeliveryEnvelope delivery,
        CancellationToken cancellationToken);
}

public sealed class TrackingDeliveryWorker
{
    private readonly ITrackingEventPublisher _publisher;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _leaseDuration;

    public TrackingDeliveryWorker(
        ITrackingEventPublisher publisher,
        TimeProvider? timeProvider = null,
        TimeSpan? leaseDuration = null)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _leaseDuration = leaseDuration ?? TimeSpan.FromMinutes(2);
    }

    public async Task<bool> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var delivery = await ClaimNextAsync(cancellationToken);
        if (delivery == null)
            return false;

        try
        {
            var result = await _publisher.PublishAsync(delivery, cancellationToken);
            await MarkDeliveredAsync(delivery.DeliveryId, result.ExternalReceiptId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await MarkFailedAsync(
                delivery.DeliveryId,
                exception.GetType().Name.ToUpperInvariant(),
                delivery.AttemptNumber,
                cancellationToken);
        }

        return true;
    }

    private Task<TrackingDeliveryEnvelope?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var leaseUntil = now.Add(_leaseDuration);

        return Db.WriteAsync(async connection =>
        {
            using var select = connection.CreateCommand();
            select.CommandText = @"
                SELECT
                    delivery.Id,
                    delivery.AttemptCount,
                    delivery.DestinationType,
                    delivery.DestinationId,
                    event.EventKey,
                    event.EventType,
                    event.PayloadJson
                FROM EventDeliveries delivery
                JOIN TrackingEvents event ON event.Id = delivery.EventId
                WHERE
                    (
                        delivery.Status IN ('Pending', 'Failed')
                        AND delivery.NextAttemptAtUtc <= @Now
                    )
                    OR
                    (
                        delivery.Status = 'Delivering'
                        AND delivery.LeaseUntilUtc IS NOT NULL
                        AND delivery.LeaseUntilUtc <= @Now
                    )
                ORDER BY delivery.NextAttemptAtUtc, delivery.Id
                LIMIT 1;";
            select.Parameters.AddWithValue("@Now", TrackingV2Store.Format(now));

            long id;
            int attemptCount;
            string destinationType;
            string destinationId;
            string eventKey;
            string eventType;
            string eventPayload;
            using (var reader = await select.ExecuteReaderAsync(cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken))
                    return null;

                id = Convert.ToInt64(reader["Id"], CultureInfo.InvariantCulture);
                attemptCount = Convert.ToInt32(reader["AttemptCount"], CultureInfo.InvariantCulture);
                destinationType = reader["DestinationType"].ToString() ?? string.Empty;
                destinationId = reader["DestinationId"].ToString() ?? string.Empty;
                eventKey = reader["EventKey"].ToString() ?? string.Empty;
                eventType = reader["EventType"].ToString() ?? string.Empty;
                eventPayload = reader["PayloadJson"].ToString() ?? string.Empty;
            }

            using var update = connection.CreateCommand();
            update.CommandText = @"
                UPDATE EventDeliveries
                SET Status = 'Delivering',
                    AttemptCount = AttemptCount + 1,
                    LastAttemptAtUtc = @Now,
                    LeaseUntilUtc = @LeaseUntil,
                    LastErrorCode = NULL
                WHERE Id = @Id;";
            update.Parameters.AddWithValue("@Now", TrackingV2Store.Format(now));
            update.Parameters.AddWithValue("@LeaseUntil", TrackingV2Store.Format(leaseUntil));
            update.Parameters.AddWithValue("@Id", id);
            await update.ExecuteNonQueryAsync(cancellationToken);

            return new TrackingDeliveryEnvelope(
                id,
                eventKey,
                eventType,
                eventPayload,
                destinationType,
                destinationId,
                attemptCount + 1);
        }, cancellationToken);
    }

    private Task MarkDeliveredAsync(
        long deliveryId,
        string? externalReceiptId,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        return Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE EventDeliveries
                SET Status = 'Delivered',
                    DeliveredAtUtc = @Now,
                    LeaseUntilUtc = NULL,
                    LastErrorCode = NULL,
                    ExternalReceiptId = @ExternalReceiptId
                WHERE Id = @Id AND Status = 'Delivering';";
            command.Parameters.AddWithValue("@Now", TrackingV2Store.Format(now));
            command.Parameters.AddWithValue(
                "@ExternalReceiptId",
                string.IsNullOrWhiteSpace(externalReceiptId) ? DBNull.Value : externalReceiptId);
            command.Parameters.AddWithValue("@Id", deliveryId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        });
    }

    private Task MarkFailedAsync(
        long deliveryId,
        string errorCode,
        int attemptNumber,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var delaySeconds = Math.Min(300, Math.Pow(2, Math.Min(attemptNumber, 8)));
        var nextAttempt = now.AddSeconds(delaySeconds);

        return Db.WriteAsync(async connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE EventDeliveries
                SET Status = 'Failed',
                    NextAttemptAtUtc = @NextAttemptAtUtc,
                    LeaseUntilUtc = NULL,
                    LastErrorCode = @LastErrorCode
                WHERE Id = @Id AND Status = 'Delivering';";
            command.Parameters.AddWithValue("@NextAttemptAtUtc", TrackingV2Store.Format(nextAttempt));
            command.Parameters.AddWithValue("@LastErrorCode", errorCode);
            command.Parameters.AddWithValue("@Id", deliveryId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        });
    }
}

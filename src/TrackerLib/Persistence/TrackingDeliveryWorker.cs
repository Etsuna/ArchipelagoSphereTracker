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
    int AttemptNumber,
    string SnapshotPayloadJson = "",
    long SnapshotId = 0);

public sealed record TrackingPublicationResult(string? ExternalReceiptId = null);

public interface ITrackingEventPublisher
{
    /// <summary>
    /// Publishes using EventKey as the recovery key. Publishers must recover an existing
    /// publication before retrying after an uncertain transport result.
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
    private readonly string? _destinationType;

    public TrackingDeliveryWorker(
        ITrackingEventPublisher publisher,
        TimeProvider? timeProvider = null,
        TimeSpan? leaseDuration = null,
        string? destinationType = null)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _leaseDuration = leaseDuration ?? TimeSpan.FromMinutes(2);
        if (_leaseDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        _destinationType = destinationType;
    }

    public async Task<bool> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var delivery = await ClaimNextAsync(cancellationToken);
        if (delivery == null)
            return false;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_leaseDuration / 2);
            var result = await _publisher.PublishAsync(delivery, timeout.Token);
            await MarkDeliveredAsync(delivery.DeliveryId, delivery.AttemptNumber, result.ExternalReceiptId, cancellationToken);
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
                    event.PayloadJson,
                    snapshot.PayloadJson AS SnapshotPayloadJson,
                    snapshot.PayloadPruned,
                    snapshot.Id AS SnapshotId
                FROM EventDeliveries delivery
                JOIN TrackingEvents event ON event.Id = delivery.EventId
                JOIN RoomSnapshots snapshot ON snapshot.Id = event.SnapshotId
                WHERE (@DestinationType IS NULL OR delivery.DestinationType = @DestinationType)
                  AND NOT EXISTS (
                    SELECT 1 FROM EventDeliveries earlier
                    WHERE earlier.DestinationType = delivery.DestinationType
                      AND earlier.DestinationId = delivery.DestinationId
                      AND earlier.Id < delivery.Id AND earlier.Status <> 'Delivered'
                  )
                  AND (
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
                  )
                ORDER BY delivery.NextAttemptAtUtc, delivery.Id
                LIMIT 1;";
            select.Parameters.AddWithValue("@Now", TrackingV2Store.Format(now));
            select.Parameters.AddWithValue("@DestinationType", (object?)_destinationType ?? DBNull.Value);

            long id;
            int attemptCount;
            string destinationType;
            string destinationId;
            string eventKey;
            string eventType;
            string eventPayload;
            string snapshotPayload;
            long snapshotId;
            using (var reader = await select.ExecuteReaderAsync(cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken))
                    return null;

                if (Convert.ToInt32(reader["PayloadPruned"], CultureInfo.InvariantCulture) != 0)
                    throw new InvalidOperationException("An unfinished delivery requires its complete snapshot payload.");

                id = Convert.ToInt64(reader["Id"], CultureInfo.InvariantCulture);
                attemptCount = Convert.ToInt32(reader["AttemptCount"], CultureInfo.InvariantCulture);
                destinationType = reader["DestinationType"].ToString() ?? string.Empty;
                destinationId = reader["DestinationId"].ToString() ?? string.Empty;
                eventKey = reader["EventKey"].ToString() ?? string.Empty;
                eventType = reader["EventType"].ToString() ?? string.Empty;
                eventPayload = reader["PayloadJson"].ToString() ?? string.Empty;
                snapshotPayload = reader["SnapshotPayloadJson"].ToString() ?? string.Empty;
                snapshotId = Convert.ToInt64(reader["SnapshotId"], CultureInfo.InvariantCulture);
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
                attemptCount + 1,
                snapshotPayload,
                snapshotId);
        }, cancellationToken);
    }

    private Task MarkDeliveredAsync(
        long deliveryId,
        int attemptNumber,
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
                WHERE Id = @Id AND Status = 'Delivering' AND AttemptCount = @AttemptNumber;";
            command.Parameters.AddWithValue("@Now", TrackingV2Store.Format(now));
            command.Parameters.AddWithValue(
                "@ExternalReceiptId",
                string.IsNullOrWhiteSpace(externalReceiptId) ? DBNull.Value : externalReceiptId);
            command.Parameters.AddWithValue("@Id", deliveryId);
            command.Parameters.AddWithValue("@AttemptNumber", attemptNumber);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return 0;
        }, cancellationToken);
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
                WHERE Id = @Id AND Status = 'Delivering' AND AttemptCount = @AttemptNumber;";
            command.Parameters.AddWithValue("@NextAttemptAtUtc", TrackingV2Store.Format(nextAttempt));
            command.Parameters.AddWithValue("@LastErrorCode", errorCode);
            command.Parameters.AddWithValue("@Id", deliveryId);
            command.Parameters.AddWithValue("@AttemptNumber", attemptNumber);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return 0;
        }, cancellationToken);
    }
}

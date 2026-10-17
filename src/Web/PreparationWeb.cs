using System.Text;
using ArchipelagoSphereTracker.src.Resources;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

public static class PreparationWeb
{
    public static object View(PreparationRoom room, string user, PreparationService? service = null)
    {
        service ??= PreparationService.Instance;
        var canManage = room.OwnerId == user;
        var me = room.Participants.FirstOrDefault(player => player.UserId == user);
        var ownFiles = room.Files.Where(file => file.UserId == user).ToArray();
        return new
        {
            title = room.Title, state = room.State.ToString(), stateLabel = PreparationTexts.State(room.State),
            maximumPlayers = room.MaximumPlayers, canManage, canInvite = canManage && room.IsPrivate, canEdit = room.CanEdit, isMember = me != null,
            meReady = me?.Ready ?? false, canReady = ownFiles.Length > 0 && ownFiles.All(file => file.Validation == "Valid"),
            canGenerate = canManage && room.CanGenerate,
            canTest = canManage && room.CanEdit && room.Files.Count > 0 && room.Files.All(file => file.Validation == "Valid"),
            canDownload = service.GetArchivePath(room, user) != null,
            trackingChannelId = room.TrackingChannelId,
            testPassed = room.LastRunStatus == "Validated",
            error = canManage ? room.LastError : room.LastError != null ? Resource.PrepGenerationFailed : null,
            summary = string.Format(Resource.PrepSummary, room.Participants.Count, room.MaximumPlayers,
                room.Participants.Count(player => player.Ready), room.Files.Sum(file => file.Slots.Count)),
            participants = room.Participants.Select(player =>
            {
                var files = room.Files.Where(file => file.UserId == player.UserId).ToArray();
                return new
                {
                    name = player.DisplayName, ready = player.Ready,
                    slots = files.SelectMany(file => file.Slots).Select(slot => new { name = slot.Name, game = slot.Game }),
                    validation = files.Length == 0 ? Resource.PrepNoFiles :
                        files.Any(file => file.Validation == "Pending") ? Resource.PrepPending :
                        files.All(file => file.Validation == "Valid") ? Resource.PrepValid : Resource.PrepInvalid
                };
            }),
            files = ownFiles.Select(file => new { id = file.Id, fileName = file.FileName,
                validation = PreparationTexts.Validation(file.Validation), pending = file.Validation == "Pending", error = file.Error })
        };
    }

    public static void Map(WebApplication app, Func<string,string,string,Task<AstPortalActor?>> resolve, PreparationService? service = null)
    {
        service ??= PreparationService.Instance;
        var downloadService = service;
        app.MapGet("/downloads/preparations/{token}", (string token) => {
            if (!Declare.IsArchipelagoMode) return Results.NotFound();
            var archive = downloadService.GetTemporaryDownload(token);
            return archive == null ? Results.NotFound() : Results.File(archive, "application/zip", "Archipelago.zip", enableRangeProcessing:true);
        });
        _ = Task.Run(async () => {
            try
            {
                downloadService.CleanupExpiredDownloads();
                using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
                while (await timer.WaitForNextTickAsync(app.Lifetime.ApplicationStopping)) downloadService.CleanupExpiredDownloads();
            }
            catch (OperationCanceledException) { }
        });
        async Task<AstPortalActor?> Actor(string guild, string channel, string token)
        {
            if (!Declare.IsArchipelagoMode) return null;
            var actor = await resolve(guild, channel, token);
            return actor != null && AstAuthorizationService.CanUseArchipelagoTools(actor.Authorization) ? actor : null;
        }

        app.MapGet("/portal/{guild}/{channel}/{token}/prepare.html", async (string guild, string channel, string token) =>
        {
            if (await Actor(guild, channel, token) == null || await service.GetAsync(guild, channel) == null)
                return Results.NotFound();
            return Results.Content(WebPortalPreparationPage.Build(), "text/html; charset=utf-8");
        });
        const string route = "/api/portal/{guild}/{channel}/{token}/preparation";
        app.MapGet(route, async (string guild, string channel, string token) =>
        {
            var actor = await Actor(guild, channel, token);
            if (actor == null) return Results.NotFound();
            var room = await service.GetAsync(guild, channel);
            return room == null ? Results.Json(new { message = Resource.ResourceManager.GetString("PrepGone") }, statusCode:410) : Results.Ok(View(room, actor.UserId, service));
        });
        app.MapGet(route + "/files/{fileId}", async (string guild, string channel, string token, string fileId) =>
        {
            var actor = await Actor(guild, channel, token);
            if (actor == null) return Results.NotFound();
            var room = await service.GetAsync(guild, channel);
            var file = room?.Files.FirstOrDefault(file => file.Id == fileId && file.UserId == actor.UserId);
            return file == null ? Results.NotFound() : Results.File(Encoding.UTF8.GetBytes(file.Content), "application/yaml", file.FileName);
        });
        app.MapGet(route + "/download", async (string guild, string channel, string token) =>
        {
            var actor = await Actor(guild, channel, token);
            if (actor == null) return Results.NotFound();
            var room = await service.GetAsync(guild, channel);
            var archive = room == null ? null : service.GetArchivePath(room, actor.UserId);
            if (archive == null) return Results.NotFound();
            await SecurityAuditLog.WriteAsync(Guid.NewGuid().ToString("N"), SecurityAuditSource.Web, actor.UserId, guild, channel,
                SecurityAuditAction.YamlDownload, SecurityAuditOutcome.Succeeded);
            return Results.File(archive, "application/zip", Path.GetFileName(archive));
        });
        app.MapPost(route + "/{action}", async (string guild, string channel, string token, string action, HttpRequest request) =>
        {
            var actor = await Actor(guild, channel, token);
            if (actor == null) return Results.NotFound();
            try
            {
                if (!request.HasFormContentType) return Results.BadRequest(new { message = Resource.PrepInvalidSettings });
                var form = await request.ReadFormAsync();
                await using var audit = await SecurityAuditScope.StartAsync(SecurityAuditSource.Web, actor.UserId, guild, channel,
                    action == "upload" ? SecurityAuditAction.YamlUpload : action == "remove" ? SecurityAuditAction.YamlDelete :
                    action is "generate" or "test" ? SecurityAuditAction.Generation : SecurityAuditAction.RoomSettingsUpdate);
                string message;
                if (action == "upload")
                {
                    var file = form.Files.FirstOrDefault();
                    if (file == null || file.Length is <= 0 or > PreparationService.MaximumYamlBytes)
                        throw new PreparationException(Resource.PrepInvalidYaml);
                    await using var stream = file.OpenReadStream();
                    message = await service.UploadStreamAsync(guild, channel, actor.UserId, file.FileName, stream);
                }
                else if (action == "remove")
                {
                    await service.RemoveFileAsync(guild, channel, actor.UserId, form["fileId"].FirstOrDefault() ?? "");
                    message = Resource.PrepSaved;
                }
                else if (action == "generate" || action == "test")
                {
                    await service.GenerateAsync(guild, channel, actor.UserId,
                        form["skipBalancing"].FirstOrDefault() == "true", testOnly:action == "test");
                    message = Resource.PrepQueued;
                }
                else if (action == "invite")
                {
                    await PreparationDiscord.InviteAsync(guild, channel, actor.UserId, form["userId"].FirstOrDefault() ?? "");
                    message = Resource.PrepSaved;
                }
                else if (action == "remind")
                {
                    await PreparationDiscord.RemindAsync(guild, channel, actor.UserId);
                    message = Resource.PrepSaved;
                }
                else if (action == "deliver")
                {
                    await PreparationDiscord.RetryDeliveryAsync(guild, channel, actor.UserId);
                    message = Resource.PrepSaved;
                }
                else message = await service.ApplyActionAsync(guild, channel, actor.UserId, actor.User.Nickname ?? actor.User.Username, action);
                audit.Succeed();
                return Results.Ok(new { message });
            }
            catch (PreparationException error) { return Results.BadRequest(new { message = error.Message }); }
            catch { return Results.Problem(Resource.PrepGenerationFailed, statusCode:500); }
        });
    }
}

using ArchipelagoSphereTracker.src.Resources;
using Discord;
using Discord.WebSocket;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Linq;
using System.Net;

public static class WebPortalServer
{
    

    private static WebApplication? _app;
    private static Task? _runTask;

    public static void Start()
    {
        if (!Declare.EnableWebPortal || _app != null)
            return;

        if (!int.TryParse(Declare.WebPortalPort, out var port) || port <= 0)
        {
            Console.WriteLine($"[Portal] Invalid port: {Declare.WebPortalPort}");
            return;
        }

        var culture = ResolveCulture(Declare.Language);

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        Directory.CreateDirectory(Declare.WebPortalPath);
        EnsureRobotsTxtFile();

        var builder = WebApplication.CreateBuilder();
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = Declare.WebPortalMaxUploadBytes;
        });

        var app = builder.Build();
        _app = app;

        app.UseRequestLocalization(CreateLocalizationOptions(culture));
        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Content-Security-Policy"] =
                "default-src 'self'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'; " +
                "img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; connect-src 'self'";

            if (context.Request.Path.StartsWithSegments("/portal") ||
                context.Request.Path.StartsWithSegments("/api/portal"))
            {
                context.Response.Headers.CacheControl = "no-store";
            }

            if (context.Request.Path.StartsWithSegments("/portal") &&
                context.Request.Path.Value?.Contains("/downloads/", StringComparison.OrdinalIgnoreCase) == true)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (TryParsePersonalPortalPath(context.Request.Path, out var guildId, out var channelId, out var token))
            {
                var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildMember);
                if (actor == null)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(WebPortalUserPage.Build(guildId, channelId, token));
                return;
            }

            await next();
        });

        app.MapGet("/robots.txt", () =>
        {
            var robotsPath = EnsureRobotsTxtFile();
            return Results.File(robotsPath, "text/plain; charset=utf-8");
        });


        app.MapGet("/", () => Results.Redirect("/portal/"));
        app.MapGet("/portal", () => Results.Redirect("/portal/"));

        app.MapGet("/portal/{guildId}/{channelId}/commands.html", () =>
            Results.NotFound(new { message = "This portal URL is no longer valid. Request a new link from Discord." }));

        app.MapGet("/portal/{guildId}/{channelId}/thread-commands.html", () =>
            Results.NotFound(new { message = "This portal URL is no longer valid. Request a new link from Discord." }));

        app.MapGet("/portal/{guildId}/{channelId}/{token}/commands.html", async (
            string guildId,
            string channelId,
            string token) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildManager);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid portal link or insufficient permissions." });

            var commandsPath = Path.Combine(Declare.WebPortalPath, "commands.html");
            if (!File.Exists(commandsPath))
                return Results.NotFound(new { message = "commands.html not found" });

            return Results.File(commandsPath, "text/html; charset=utf-8");
        });

        app.MapGet("/portal/{guildId}/{channelId}/{token}/thread-commands.html", async (
            string guildId,
            string channelId,
            string token) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.RoomManager);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid portal link or insufficient permissions." });

            var threadCommandsPath = Path.Combine(Declare.WebPortalPath, "thread-commands.html");
            if (!File.Exists(threadCommandsPath))
                return Results.NotFound(new { message = "thread-commands.html not found" });

            return Results.File(threadCommandsPath, "text/html; charset=utf-8");
        });

        app.MapGet("/api/portal/{guildId}/{channelId}/{token}/summary", async (string guildId, string channelId, string token) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildMember);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid token." });
            var userId = actor.UserId;

            var aliases = await RecapListCommands.GetAliasesForUserAsync(guildId, channelId, userId);
            var recapMap = await ReceiverAliasesCommands.GetUserAliasesWithItemsAsync(guildId, channelId, userId);
            var gameStatuses = await GameStatusCommands.GetGameStatusForGuildAndChannelAsync(guildId, channelId);

            var recaps = recapMap
                .Select(kvp => new PortalRecap(
                    kvp.Key,
                    GroupRecapItemsByFlag(kvp.Value)))
                .ToList();

            var receivedItems = new List<PortalAliasItems>();
            var hints = new List<PortalAliasHints>();

            foreach (var alias in aliases)
            {
                var items = await DisplayItemCommands.GetUserItemsGroupedAsync(guildId, channelId, alias);
                receivedItems.Add(new PortalAliasItems(alias, GroupReceivedItemsByFlag(items)));

                var receiverHints = await HintStatusCommands.GetHintStatusForReceiver(guildId, channelId, alias);
                var finderHints = await HintStatusCommands.GetHintStatusForFinder(guildId, channelId, alias);

                hints.Add(new PortalAliasHints(alias,
                    receiverHints.Select(h => new PortalHintItem(h.Finder, h.Receiver, h.Item, h.Location, h.Game)).ToList(),
                    finderHints.Select(h => new PortalHintItem(h.Finder, h.Receiver, h.Item, h.Location, h.Game)).ToList()));
            }

            var summary = new PortalSummary(
                guildId,
                channelId,
                userId,
                DateTimeOffset.UtcNow,
                gameStatuses,
                recaps,
                receivedItems,
                hints
            );

            return Results.Ok(summary);
        });

        app.MapPost("/api/portal/{guildId}/{channelId}/{token}/recap/delete", async (
            string guildId,
            string channelId,
            string token,
            HttpRequest request) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildMember);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid token." });
            var userId = actor.UserId;

            var form = await request.ReadFormAsync();
            var alias = form["alias"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(alias))
                return Results.BadRequest(new { message = "alias is required" });

            await using var audit = await SecurityAuditScope.StartAsync(
                SecurityAuditSource.Web,
                actor.UserId,
                guildId,
                channelId,
                SecurityAuditAction.DataCleanup);
            await RecapListCommands.DeleteRecapListAsync(guildId, channelId, userId, alias);
            audit.Succeed();
            return Results.Ok(new { message = string.Format(Resource.AstCenterRecapForCleared, alias) });
        });

        app.MapPost("/api/portal/{guildId}/{channelId}/{token}/recap/delete-all", async (
            string guildId,
            string channelId,
            string token) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildMember);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid token." });

            await using var audit = await SecurityAuditScope.StartAsync(
                SecurityAuditSource.Web,
                actor.UserId,
                guildId,
                channelId,
                SecurityAuditAction.DataCleanup);
            await RecapListCommands.DeleteAliasAndItemsForUserIdAsync(guildId, channelId, actor.UserId);
            audit.Succeed();
            return Results.Ok(new { message = Resource.AstCenterAllYourRecapsWereCleared });
        });

        app.MapGet("/api/portal/{guildId}/{channelId}/{token}/aliases", async (string guildId, string channelId, string token) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildMember);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid token." });

            var aliases = await AliasChoicesCommands.GetAliasesForGuildAndChannelAsync(guildId, channelId);
            if (aliases.Count == 0)
            {
                aliases = await ReceiverAliasesCommands.GetReceiver(guildId, channelId);
            }

            var uniqueAliases = aliases
                .Where(alias => !string.IsNullOrWhiteSpace(alias))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Results.Ok(new { aliases = uniqueAliases });
        });

        app.MapGet("/api/portal/{guildId}/{channelId}/{token}/spoiler/status", async (
            string guildId,
            string channelId,
            string token) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildMember);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid token." });

            var spoilerPath = SpoilerLogClass.GetLatestSpoilerPath(channelId);
            return Results.Ok(new
            {
                exists = spoilerPath != null,
                fileName = spoilerPath == null ? null : Path.GetFileName(spoilerPath),
                lastModifiedUtc = spoilerPath == null ? (DateTime?)null : File.GetLastWriteTimeUtc(spoilerPath)
            });
        });

        app.MapPost("/api/portal/{guildId}/{channelId}/{token}/spoiler/upload", async (
            string guildId,
            string channelId,
            string token,
            HttpRequest request) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildMember);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid token." });

            var form = await request.ReadFormAsync();
            var file = form.Files.FirstOrDefault();
            if (file == null)
                return Results.BadRequest(new { message = "spoiler file is required." });
            if (file.Length <= 0 ||
                file.Length > Declare.WebPortalMaxUploadBytes ||
                !FileUploadSecurity.TryGetSafeFileName(file.FileName, [".txt", ".json"], out _))
            {
                return Results.BadRequest(new { message = "Invalid or oversized spoiler file." });
            }

            await using var audit = await SecurityAuditScope.StartAsync(
                SecurityAuditSource.Web,
                actor.UserId,
                guildId,
                channelId,
                SecurityAuditLog.ForCommand("send-spoiler-log"));
            await using var stream = file.OpenReadStream();
            var message = await SpoilerLogClass.SendSpoilerLogFromStreamAsync(
                channelId,
                file.FileName,
                stream);
            audit.Succeed();
            return Results.Ok(new { message });
        });

        app.MapPost("/api/portal/{guildId}/{channelId}/{token}/spoiler/analyze", async (
            string guildId,
            string channelId,
            string token,
            HttpRequest request) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildMember);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid token." });

            var form = await request.ReadFormAsync();
            if (!TryParseOptionalNonNegativeInt(form["sphere"].FirstOrDefault(), out var sphereLimit) ||
                !TryParseOptionalNonNegativeInt(form["validateSphere"].FirstOrDefault(), out var sphereToValidate))
            {
                return Results.BadRequest(new { message = Resource.AstCenterEnterASlotAndUseOnlyNonNegativeIntegers });
            }

            var requestedAlias = form["alias"].FirstOrDefault();
            string? canonicalAlias = null;
            if (!string.IsNullOrWhiteSpace(requestedAlias))
            {
                var roomAliases = await AliasChoicesCommands.GetAliasesForGuildAndChannelAsync(guildId, channelId);
                canonicalAlias = roomAliases.FirstOrDefault(alias =>
                    string.Equals(alias, requestedAlias, StringComparison.OrdinalIgnoreCase));
                if (canonicalAlias == null)
                    return Results.BadRequest(new { message = string.Format(Resource.AliasNotFound, requestedAlias) });
            }

            await using var audit = await SecurityAuditScope.StartAsync(
                SecurityAuditSource.Web,
                actor.UserId,
                guildId,
                channelId,
                SecurityAuditLog.ForCommand("analyze-spoiler-log"));
            var message = await SpoilerAnalysisClass.AnalyzeSpoilerLogAsync(
                channelId,
                guildId,
                canonicalAlias,
                sphereLimit,
                string.Equals(form["missingMode"].FirstOrDefault(), "full", StringComparison.OrdinalIgnoreCase),
                !string.Equals(form["hideItems"].FirstOrDefault(), "false", StringComparison.OrdinalIgnoreCase),
                sphereToValidate,
                string.Equals(form["resetValidation"].FirstOrDefault(), "true", StringComparison.OrdinalIgnoreCase));
            audit.Succeed();
            return Results.Ok(new { message });
        });

        app.MapGet("/api/portal/{guildId}/{channelId}/{token}/aliases/user", async (string guildId, string channelId, string token) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildMember);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid token." });
            var userId = actor.UserId;

            var aliases = await RecapListCommands.GetAliasesForUserAsync(guildId, channelId, userId);
            var uniqueAliases = aliases
                .Where(alias => !string.IsNullOrWhiteSpace(alias))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Results.Ok(new { aliases = uniqueAliases });
        });

        app.MapPost("/api/portal/{guildId}/{channelId}/{token}/alias/add", async (
            string guildId,
            string channelId,
            string token,
            HttpRequest request) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildMember);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid token." });
            var userId = actor.UserId;

            var form = await request.ReadFormAsync();
            var alias = form["alias"].FirstOrDefault();
            var skipMention = form["skipMention"].FirstOrDefault() ?? "0";
            if (string.IsNullOrWhiteSpace(alias))
                return Results.BadRequest(new { message = "alias is required" });

            var owners = await ReceiverAliasesCommands.GetAllUsersIds(guildId, channelId, alias);
            if (owners.Count > 0)
                return Results.Conflict(new { message = "Alias already registered for this user." });

            await using var audit = await SecurityAuditScope.StartAsync(
                SecurityAuditSource.Web,
                actor.UserId,
                guildId,
                channelId,
                SecurityAuditAction.AliasAdd);
            await ReceiverAliasesCommands.InsertReceiverAlias(guildId, channelId, alias, userId, skipMention);

            var recapExists = await RecapListCommands.CheckIfExists(guildId, channelId, userId, alias);
            if (!recapExists)
                await RecapListCommands.AddOrEditRecapListAsync(guildId, channelId, userId, alias);

            var aliasItems = await DisplayItemCommands.GetAliasItems(guildId, channelId, alias);
            if (aliasItems != null)
                await RecapListCommands.AddOrEditRecapListItemsAsync(guildId, channelId, alias, aliasItems);

            audit.Succeed();
            return Results.Ok(new { message = "ok" });
        });

        app.MapPost("/api/portal/{guildId}/{channelId}/{token}/alias/delete", async (
            string guildId,
            string channelId,
            string token,
            HttpRequest request) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildMember);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid token." });
            var userId = actor.UserId;

            var form = await request.ReadFormAsync();
            var alias = form["alias"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(alias))
                return Results.BadRequest(new { message = "alias is required" });

            var owners = await ReceiverAliasesCommands.GetAllUsersIds(guildId, channelId, alias);
            if (!owners.Contains(userId))
                return Results.NotFound(new { message = "Alias not found for this user." });

            await using var audit = await SecurityAuditScope.StartAsync(
                SecurityAuditSource.Web,
                actor.UserId,
                guildId,
                channelId,
                SecurityAuditAction.AliasDelete);
            await ReceiverAliasesCommands.DeleteReceiverAliasForUser(guildId, channelId, alias, userId);
            await RecapListCommands.DeleteAliasAndRecapListAsync(guildId, channelId, userId, alias);

            audit.Succeed();
            return Results.Ok(new { message = "ok" });
        });

        app.MapGet("/api/portal/{guildId}/{channelId}/{token}/patches", async (
            string guildId,
            string channelId,
            string token) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildMember);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid token." });

            await using var audit = await SecurityAuditScope.StartAsync(
                SecurityAuditSource.Web,
                actor.UserId,
                guildId,
                channelId,
                SecurityAuditAction.PatchAccess);
            var ownedAliases = (await RecapListCommands.GetAliasesForUserAsync(guildId, channelId, actor.UserId))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var patches = (await ChannelsAndUrlsCommands.GetPatchesForChannelAsync(guildId, channelId))
                .Where(entry => ownedAliases.Contains(entry.Alias))
                .Select(entry => new PortalPatchAlias(entry.Alias, entry.GameName, entry.Patch))
                .ToList();

            audit.Succeed();
            return Results.Ok(new { aliases = patches });
        });

        app.MapGet("/api/portal/{guildId}/{channelId}/{token}/exclusions", async (
            string guildId,
            string channelId,
            string token) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildMember);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid token." });

            var aliases = await RecapListCommands.GetAliasesForUserAsync(guildId, channelId, actor.UserId);
            var exclusions = new List<PortalAliasExclusions>();
            foreach (var alias in aliases.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                exclusions.Add(new PortalAliasExclusions(
                    alias,
                    await ExcludedItemsCommands.GetExcludedItemsForUserByAliasAsync(
                        guildId,
                        channelId,
                        actor.UserId,
                        alias)));
            }

            return Results.Ok(new { aliases = exclusions });
        });

        app.MapGet("/api/portal/{guildId}/{channelId}/{token}/exclusions/items", async (
            string guildId,
            string channelId,
            string token,
            HttpRequest request) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildMember);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid token." });

            var alias = request.Query["alias"].FirstOrDefault();
            var ownedAlias = await ResolveOwnedAliasAsync(guildId, channelId, actor.UserId, alias);
            if (ownedAlias == null)
                return Results.NotFound(new { message = string.Format(Resource.AliasNotFound, alias) });

            var items = await ExcludedItemsCommands.GetItemNamesForAliasAsync(guildId, channelId, ownedAlias);
            return Results.Ok(new { alias = ownedAlias, items });
        });

        app.MapPost("/api/portal/{guildId}/{channelId}/{token}/exclusion/add", async (
            string guildId,
            string channelId,
            string token,
            HttpRequest request) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildMember);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid token." });

            var form = await request.ReadFormAsync();
            var requestedAlias = form["alias"].FirstOrDefault();
            var ownedAlias = await ResolveOwnedAliasAsync(
                guildId,
                channelId,
                actor.UserId,
                requestedAlias);
            var item = form["item"].FirstOrDefault();
            if (ownedAlias == null)
                return Results.BadRequest(new { message = string.Format(Resource.AliasNotFound, requestedAlias) });
            if (string.IsNullOrWhiteSpace(item))
                return Results.BadRequest(new { message = Resource.WebSelectAliasAndItem });

            var availableItems = await ExcludedItemsCommands.GetItemNamesForAliasAsync(guildId, channelId, ownedAlias);
            var canonicalItem = availableItems.FirstOrDefault(candidate =>
                string.Equals(candidate, item, StringComparison.OrdinalIgnoreCase));
            if (canonicalItem == null)
                return Results.BadRequest(new { message = string.Format(Resource.ExcludeItemNotFound, item, ownedAlias) });

            await using var audit = await SecurityAuditScope.StartAsync(
                SecurityAuditSource.Web,
                actor.UserId,
                guildId,
                channelId,
                SecurityAuditAction.RoomSettingsUpdate);
            var message = await ExcludedItemsCommands.AddExcludedItemForUserAsync(
                guildId,
                channelId,
                actor.UserId,
                ownedAlias,
                canonicalItem);
            audit.Succeed();
            return Results.Ok(new { message });
        });

        app.MapPost("/api/portal/{guildId}/{channelId}/{token}/exclusion/delete", async (
            string guildId,
            string channelId,
            string token,
            HttpRequest request) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildMember);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid token." });

            var form = await request.ReadFormAsync();
            var requestedAlias = form["alias"].FirstOrDefault();
            var ownedAlias = await ResolveOwnedAliasAsync(
                guildId,
                channelId,
                actor.UserId,
                requestedAlias);
            var item = form["item"].FirstOrDefault();
            if (ownedAlias == null)
                return Results.BadRequest(new { message = string.Format(Resource.AliasNotFound, requestedAlias) });
            if (string.IsNullOrWhiteSpace(item))
                return Results.BadRequest(new { message = Resource.WebSelectAliasAndItem });

            await using var audit = await SecurityAuditScope.StartAsync(
                SecurityAuditSource.Web,
                actor.UserId,
                guildId,
                channelId,
                SecurityAuditAction.RoomSettingsUpdate);
            var message = await ExcludedItemsCommands.DeleteExcludedItemForUserAsync(
                guildId,
                channelId,
                actor.UserId,
                ownedAlias,
                item);
            audit.Succeed();
            return Results.Ok(new { message });
        });

        app.MapPost("/api/portal/{guildId}/{channelId}/{token}/revoke", async (
            string guildId,
            string channelId,
            string token) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildMember);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid token." });

            await using var audit = await SecurityAuditScope.StartAsync(
                SecurityAuditSource.Web,
                actor.UserId,
                guildId,
                channelId,
                SecurityAuditAction.PortalAccessRevoke);
            await PortalAccessCommands.RevokePortalTokenAsync(guildId, channelId, actor.UserId);
            audit.Succeed();
            return Results.Ok(new { message = Resource.AstCenterThePortalLinkWasRevoked });
        });

        app.MapGet("/api/portal/{guildId}/{channelId}/{token}/commands/templates/{templateName}", async (
            string guildId,
            string channelId,
            string token,
            string templateName) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildManager);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid portal link or insufficient permissions." });

            if (!Declare.IsArchipelagoMode)
                return Results.BadRequest(new { message = "Archipelago mode is disabled." });

            var safeTemplateName = Path.GetFileName(templateName);
            if (string.IsNullOrWhiteSpace(safeTemplateName) ||
                !string.Equals(safeTemplateName, templateName, StringComparison.Ordinal) ||
                !safeTemplateName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { message = "Invalid template name." });
            }

            var templatePath = Path.Combine(Declare.BasePath, "extern", "Archipelago", "Players", "Templates", safeTemplateName);
            if (!File.Exists(templatePath))
                return Results.NotFound(new { message = "Template not found." });

            return Results.File(templatePath, "application/x-yaml", safeTemplateName);
        });

        app.MapGet("/api/portal/{guildId}/{channelId}/{token}/commands/yamls", async (
            string guildId,
            string channelId,
            string token) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildManager);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid portal link or insufficient permissions." });

            if (!Declare.IsArchipelagoMode)
                return Results.BadRequest(new { message = "Archipelago mode is disabled." });

            var yamls = YamlClass.GetYamlFileNames(channelId);
            return Results.Ok(new { files = yamls });
        });

        app.MapGet("/api/portal/{guildId}/{channelId}/{token}/commands/yamls/{yamlName}", async (
            string guildId,
            string channelId,
            string token,
            string yamlName) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildManager);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid portal link or insufficient permissions." });

            if (!Declare.IsArchipelagoMode)
                return Results.BadRequest(new { message = "Archipelago mode is disabled." });

            var safeYamlName = Path.GetFileName(yamlName);
            if (string.IsNullOrWhiteSpace(safeYamlName) ||
                !string.Equals(safeYamlName, yamlName, StringComparison.Ordinal) ||
                !safeYamlName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { message = "Invalid yaml name." });
            }

            var yamlPath = Path.Combine(Declare.BasePath, "extern", "Archipelago", "Players", channelId, "yaml", safeYamlName);
            if (!File.Exists(yamlPath))
                return Results.NotFound(new { message = "Yaml not found." });

            return Results.File(yamlPath, "application/x-yaml", safeYamlName);
        });

        app.MapGet("/api/portal/{guildId}/{channelId}/{token}/thread-commands/patches", async (
            string guildId,
            string channelId,
            string token) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.RoomManager);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid portal link or insufficient permissions." });

            if (!Declare.EnableWebPortal)
                return Results.BadRequest(new { message = "Web portal is disabled." });

            await using var audit = await SecurityAuditScope.StartAsync(
                SecurityAuditSource.Web,
                actor.UserId,
                guildId,
                channelId,
                SecurityAuditAction.PatchAccess);
            var patches = await ChannelsAndUrlsCommands.GetPatchesForChannelAsync(guildId, channelId);

            var aliases = patches
                .GroupBy(x => x.Alias, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var withPatch = group.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Patch));
                    var selected = string.IsNullOrWhiteSpace(withPatch.Patch) ? group.First() : withPatch;
                    return new PortalPatchAlias(selected.Alias, selected.GameName, selected.Patch);
                })
                .OrderBy(item => item.Alias, StringComparer.OrdinalIgnoreCase)
                .ToList();

            audit.Succeed();
            return Results.Ok(new { aliases });
        });

        app.MapGet("/api/portal/{guildId}/{channelId}/{token}/info", async (
            string guildId,
            string channelId,
            string token) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildMember);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid token." });

            if (!Declare.EnableWebPortal)
                return Results.BadRequest(new { message = "Web portal is disabled." });

            if (string.IsNullOrWhiteSpace(channelId))
                return Results.BadRequest(new { message = "channelId is required." });

            var message = await HelperClass.Info(channelId, guildId);
            return Results.Ok(new { message });
        });

        app.MapGet("/api/portal/{guildId}/{channelId}/{token}/room-links", async (
            string guildId,
            string channelId,
            string token) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildMember);
            if (actor == null || !ulong.TryParse(actor.UserId, out var userId))
                return Results.NotFound(new { message = "Invalid token." });

            if (!Declare.EnableWebPortal)
                return Results.BadRequest(new { message = "Web portal is disabled." });

            if (string.IsNullOrWhiteSpace(guildId))
                return Results.BadRequest(new { message = "guildId is required." });

            var channels = await DatabaseCommands.GetAllChannelsAsync(guildId, "ChannelsAndUrlsTable");
            var distinctChannels = channels
                .Where(channel => !string.IsNullOrWhiteSpace(channel))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(channel => channel, StringComparer.Ordinal)
                .ToList();

            SocketGuild? guild = null;
            if (ulong.TryParse(guildId, out var guildIdValue))
                guild = Declare.Client.GetGuild(guildIdValue);

            var links = new List<PortalThreadLink>();
            foreach (var roomChannelId in distinctChannels)
            {
                var targetAuthorization = await AstAuthorizationService.CreateDiscordContextAsync(
                    guildId,
                    roomChannelId,
                    userId,
                    actor.User);
                if (targetAuthorization == null ||
                    !AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildMember, targetAuthorization))
                {
                    continue;
                }

                links.Add(new PortalThreadLink(
                    roomChannelId,
                    ResolveThreadName(guild, roomChannelId),
                    $"/api/portal/{guildId}/{channelId}/{token}/open-room/{roomChannelId}"));
            }

            return Results.Ok(new { links });
        });

        app.MapGet("/api/portal/{guildId}/{channelId}/{token}/open-room/{targetChannelId}", async (
            string guildId,
            string channelId,
            string token,
            string targetChannelId) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildMember);
            if (actor == null || !ulong.TryParse(actor.UserId, out var userId))
                return Results.NotFound(new { message = "Invalid token." });

            var targetAuthorization = await AstAuthorizationService.CreateDiscordContextAsync(
                guildId,
                targetChannelId,
                userId,
                actor.User);
            if (targetAuthorization == null ||
                !AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildMember, targetAuthorization))
            {
                return Results.NotFound(new { message = "Room not found or unavailable." });
            }

            await using var audit = await SecurityAuditScope.StartAsync(
                SecurityAuditSource.Web,
                actor.UserId,
                guildId,
                targetChannelId,
                SecurityAuditAction.PortalAccessIssue);
            var targetToken = await PortalAccessCommands.IssuePortalTokenAsync(guildId, targetChannelId, actor.UserId);
            audit.Succeed();
            return Results.Redirect($"/portal/{guildId}/{targetChannelId}/{targetToken}/");
        });

        app.MapPost("/api/portal/{guildId}/{channelId}/{token}/thread-commands/execute", async (
            string guildId,
            string channelId,
            string token,
            HttpRequest request) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.RoomManager);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid portal link or insufficient permissions." });

            if (!Declare.EnableWebPortal)
                return Results.BadRequest(new { message = "Web portal is disabled." });

            var form = await request.ReadFormAsync();
            var command = form["command"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(command) || string.IsNullOrWhiteSpace(channelId))
                return Results.BadRequest(new { message = "command and channelId are required." });

            if (command == "ast-health" &&
                !AstAuthorizationService.IsAllowed(AstAuthorizationLevel.GuildManager, actor.Authorization))
            {
                return Results.NotFound(new { message = "Invalid portal link or insufficient permissions." });
            }

            await using var audit = await SecurityAuditScope.StartAsync(
                SecurityAuditSource.Web,
                actor.UserId,
                guildId,
                channelId,
                SecurityAuditLog.ForCommand(command));
            string message;

            switch (command)
            {
                case "get-aliases":
                    message = await AliasClass.GetAlias(channelId, guildId);
                    break;
                case "info":
                    message = await HelperClass.Info(channelId, guildId);
                    break;
                case "status-games-list":
                    message = await HelperClass.StatusGameList(channelId, guildId);
                    break;
                case "ast-health":
                    message = TrackingControlCommands.GetGuildHealth(guildId);
                    break;
                case "ast-room-health":
                case "ast-sync-now":
                case "ast-pause":
                case "ast-resume":
                    message = await TrackingControlCommands.ExecuteRoomAsync(command, guildId, channelId);
                    break;
                case "ast-polling":
                    message = await ChannelsAndUrlsCommands.UpdatePollingPolicyFromWeb(
                        form["mode"].FirstOrDefault(),
                        form["maximumFrequency"].FirstOrDefault(),
                        channelId,
                        guildId);
                    break;
                case "send-spoiler-log":
                    {
                        var file = form.Files.FirstOrDefault();
                        if (file == null)
                            return Results.BadRequest(new { message = "spoiler file is required." });
                        if (file.Length <= 0 ||
                            file.Length > Declare.WebPortalMaxUploadBytes ||
                            !FileUploadSecurity.TryGetSafeFileName(
                                file.FileName,
                                [".txt", ".json"],
                                out _))
                        {
                            return Results.BadRequest(new { message = "Invalid or oversized spoiler file." });
                        }

                        await using var stream = file.OpenReadStream();
                        message = await SpoilerLogClass.SendSpoilerLogFromStreamAsync(
                            channelId,
                            file.FileName,
                            stream);
                        break;
                    }
                case "analyze-spoiler-log":
                    {
                        if (!TryParseOptionalNonNegativeInt(form["sphere"].FirstOrDefault(), out var sphereLimit) ||
                            !TryParseOptionalNonNegativeInt(form["validateSphere"].FirstOrDefault(), out var sphereToValidate))
                        {
                            return Results.BadRequest(new { message = Resource.AstCenterEnterASlotAndUseOnlyNonNegativeIntegers });
                        }

                        var requestedAlias = form["alias"].FirstOrDefault();
                        string? canonicalAlias = null;
                        if (!string.IsNullOrWhiteSpace(requestedAlias))
                        {
                            var roomAliases = await AliasChoicesCommands.GetAliasesForGuildAndChannelAsync(guildId, channelId);
                            canonicalAlias = roomAliases.FirstOrDefault(alias =>
                                string.Equals(alias, requestedAlias, StringComparison.OrdinalIgnoreCase));
                            if (canonicalAlias == null)
                                return Results.BadRequest(new { message = string.Format(Resource.AliasNotFound, requestedAlias) });
                        }

                        message = await SpoilerAnalysisClass.AnalyzeSpoilerLogAsync(
                            channelId,
                            guildId,
                            canonicalAlias,
                            sphereLimit,
                            string.Equals(form["missingMode"].FirstOrDefault(), "full", StringComparison.OrdinalIgnoreCase),
                            !string.Equals(form["hideItems"].FirstOrDefault(), "false", StringComparison.OrdinalIgnoreCase),
                            sphereToValidate,
                            string.Equals(form["resetValidation"].FirstOrDefault(), "true", StringComparison.OrdinalIgnoreCase));
                        break;
                    }
                case "get-patch":
                    {
                        var alias = form["alias"].FirstOrDefault();
                        message = await HelperClass.GetPatchByAlias(alias, channelId, guildId);
                        break;
                    }
                case "update-frequency-check":
                    {
                        var checkFrequency = form["checkFrequency"].FirstOrDefault();
                        message = await ChannelsAndUrlsCommands.UpdateFrequencyCheckFromWeb(checkFrequency, channelId, guildId);
                        break;
                    }
                case "update-silent-option":
                    {
                        var silent = form["silent"].FirstOrDefault();
                        message = await ChannelsAndUrlsCommands.UpdateSilentOptionFromWeb(silent, channelId, guildId);
                        break;
                    }
                case "delete-url":
                    message = await UrlClass.DeleteChannelAndUrl(channelId, guildId);
                    break;
                default:
                    return Results.BadRequest(new { message = "Unknown command." });
            }

            audit.Succeed();
            return Results.Ok(new { message });
        });

        app.MapGet("/api/portal/{guildId}/{channelId}/{token}/downloads/{fileName}", async (
            string guildId,
            string channelId,
            string token,
            string fileName) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildManager);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid portal link or insufficient permissions." });

            var safeFileName = Path.GetFileName(fileName);
            if (!IsExactSafeFileName(fileName, safeFileName) ||
                !safeFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { message = "Invalid download name." });
            }

            var filePath = Path.Combine(Declare.WebPortalDownloadPath, guildId, channelId, actor.UserId, safeFileName);
            if (!File.Exists(filePath))
                return Results.NotFound(new { message = "Download not found or expired." });

            return Results.File(filePath, "application/zip", safeFileName);
        });

        app.MapGet("/api/portal/{guildId}/{channelId}/{token}/audit", async (
            string guildId,
            string channelId,
            string token,
            HttpRequest request) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildManager);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid portal link or insufficient permissions." });

            var requestedLimit = int.TryParse(request.Query["limit"].FirstOrDefault(), out var parsedLimit)
                ? parsedLimit
                : 100;
            var entries = await SecurityAuditLog.GetRecentAsync(guildId, requestedLimit);
            return Results.Ok(new
            {
                entries = entries.Select(entry => new
                {
                    entry.Id,
                    entry.OccurredAtUtc,
                    entry.CorrelationId,
                    Source = entry.Source.ToString(),
                    entry.ActorUserId,
                    entry.GuildId,
                    entry.ChannelId,
                    Action = entry.Action.ToString(),
                    Outcome = entry.Outcome.ToString()
                })
            });
        });

        app.MapPost("/api/portal/{guildId}/{channelId}/{token}/commands/execute", async (
            string guildId,
            string channelId,
            string token,
            HttpRequest request) =>
        {
            var actor = await AuthorizePortalAsync(guildId, channelId, token, AstAuthorizationLevel.GuildManager);
            if (actor == null)
                return Results.NotFound(new { message = "Invalid portal link or insufficient permissions." });

            if (!Declare.EnableWebPortal)
                return Results.BadRequest(new { message = "Web portal is disabled." });

            var form = await request.ReadFormAsync();
            var command = form["command"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(command) || string.IsNullOrWhiteSpace(channelId))
                return Results.BadRequest(new { message = "command and channelId are required." });

            if ((command is "list-apworld" or "backup-apworld" or "send-apworld") &&
                !AstAuthorizationService.IsAllowed(AstAuthorizationLevel.InstanceOwner, actor.Authorization))
            {
                return Results.NotFound(new { message = "Invalid portal link or insufficient permissions." });
            }

            await using var audit = await SecurityAuditScope.StartAsync(
                SecurityAuditSource.Web,
                actor.UserId,
                guildId,
                channelId,
                SecurityAuditLog.ForCommand(command));

            var downloadRoot = Path.Combine(Declare.WebPortalDownloadPath, guildId, channelId, actor.UserId);
            if (Declare.IsArchipelagoMode)
            {
                Directory.CreateDirectory(downloadRoot);
            }

            string? downloadUrl = null;
            var prefix = request.PathBase.Value?.TrimEnd('/') ?? "";
            string message;

            switch (command)
            {
                case "ast-health":
                    message = TrackingControlCommands.GetGuildHealth(guildId);
                    break;

                case "add-url":
                    {
                        var url = form["url"].FirstOrDefault() ?? string.Empty;
                        var threadName = form["threadName"].FirstOrDefault() ?? "Archipelago";
                        var threadType = form["threadType"].FirstOrDefault() ?? "Private";
                        var autoAddMembers = form["autoAddMembers"].FirstOrDefault() == "true";
                        var silent = form["silent"].FirstOrDefault() == "true";
                        var checkFrequency = form["checkFrequency"].FirstOrDefault() ?? "5m";
                        if (!ulong.TryParse(channelId, out var channelIdValue))
                            return Results.BadRequest(new { message = "Invalid channelId." });

                        var channel = Declare.Client.GetChannel(channelIdValue) as ITextChannel;
                        if (channel == null)
                            return Results.BadRequest(new { message = "Channel not found." });

                        var options = new UrlClass.UrlAddOptions(
                            url,
                            threadName,
                            threadType,
                            autoAddMembers,
                            silent,
                            checkFrequency,
                            actor.User);

                        message = await UrlClass.AddUrlFromWebAsync(options, channelId, guildId, channel);
                        break;
                    }

                case "list-yamls":
                    message = YamlClass.ListYamls(channelId);
                    break;

                case "backup-yamls":
                    {
                        var fileName = $"backup_yaml_{channelId}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.zip";
                        var zipPath = Path.Combine(downloadRoot, fileName);
                        message = await YamlClass.BackupYamlsToFileAsync(channelId, zipPath);
                        if (string.IsNullOrWhiteSpace(message))
                            downloadUrl = ProtectedDownloadUrl(prefix, guildId, channelId, token, fileName);
                        break;
                    }

                case "delete-yaml":
                    {
                        var fileName = form["fileName"].FirstOrDefault();
                        message = YamlClass.DeleteYamlByName(channelId, fileName);
                        break;
                    }

                case "download-yaml":
                    {
                        var fileName = form["fileName"].FirstOrDefault() ?? string.Empty;
                        var safeFileName = Path.GetFileName(fileName);
                        if (!IsExactSafeFileName(fileName, safeFileName) ||
                            !safeFileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
                        {
                            message = Resource.NoFileSelected;
                            break;
                        }

                        var yamlPath = Path.Combine(Declare.BasePath, "extern", "Archipelago", "Players", channelId, "yaml", safeFileName);
                        if (!File.Exists(yamlPath))
                        {
                            message = Resource.YamlFileNotExists;
                            break;
                        }

                        message = string.Empty;
                        downloadUrl = $"{prefix}/api/portal/{guildId}/{channelId}/{token}/commands/yamls/{Uri.EscapeDataString(safeFileName)}";
                        break;
                    }

                case "clean-yamls":
                    message = YamlClass.CleanYamls(channelId);
                    break;

                case "send-yaml":
                    {
                        var file = form.Files.FirstOrDefault();
                        if (file == null)
                            return Results.BadRequest(new { message = "yaml file is required." });
                        if (!IsAllowedUpload(file, ".yaml"))
                            return Results.BadRequest(new { message = "Invalid or oversized YAML file." });

                        await using var stream = file.OpenReadStream();
                        message = await YamlClass.SendYamlFromStreamAsync(channelId, file.FileName, stream);
                        break;
                    }

                case "download-template":
                    {
                        var template = form["template"].FirstOrDefault() ?? string.Empty;
                        var safeTemplate = Path.GetFileName(template);
                        if (string.IsNullOrWhiteSpace(safeTemplate) || !safeTemplate.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
                        {
                            message = Resource.NoFileSelected;
                            break;
                        }

                        var templatePath = Path.Combine(Declare.BasePath, "extern", "Archipelago", "Players", "Templates", safeTemplate);
                        if (!File.Exists(templatePath))
                        {
                            message = Resource.YamlFileNotExists;
                            break;
                        }

                        message = string.Empty;
                        downloadUrl = $"{prefix}/api/portal/{guildId}/{channelId}/{token}/commands/templates/{Uri.EscapeDataString(safeTemplate)}";
                        break;
                    }

                case "list-apworld":
                    message = ApworldClass.ListApworld();
                    break;

                case "apworlds-info":
                    message = string.Format(Resource.ApworldInfo, Declare.ApworldInfoSheet);
                    break;

                case "backup-apworld":
                    {
                        var fileName = $"backup_apworld_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.zip";
                        var zipPath = Path.Combine(downloadRoot, fileName);
                        message = await ApworldClass.BackupApworldToFileAsync(zipPath);
                        if (string.IsNullOrWhiteSpace(message))
                            downloadUrl = ProtectedDownloadUrl(prefix, guildId, channelId, token, fileName);
                        break;
                    }

                case "send-apworld":
                    {
                        var file = form.Files.FirstOrDefault();
                        if (file == null)
                            return Results.BadRequest(new { message = "apworld file is required." });
                        if (!IsAllowedUpload(file, ".apworld"))
                            return Results.BadRequest(new { message = "Invalid or oversized APWorld file." });

                        await using var stream = file.OpenReadStream();
                        message = await ApworldClass.SendApworldFromStreamAsync(file.FileName, stream);
                        break;
                    }

                case "generate":
                    {
                        var skipProgBalancing = string.Equals(
                            form["skipProgBalancing"].FirstOrDefault(),
                            "true",
                            StringComparison.OrdinalIgnoreCase);
                        var result = await GenerationClass.GenerateAsyncForWeb(channelId, skipProgBalancing);
                        message = result.Message;
                        if (!string.IsNullOrWhiteSpace(result.ZipPath))
                        {
                            var fileName = $"{Path.GetFileNameWithoutExtension(result.ZipPath)}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.zip";
                            var destinationPath = Path.Combine(downloadRoot, fileName);
                            File.Copy(result.ZipPath, destinationPath, overwrite: true);
                            downloadUrl = ProtectedDownloadUrl(prefix, guildId, channelId, token, fileName);
                        }
                        break;
                    }

                case "test-generate":
                    message = await GenerationClass.TestGenerateAsyncForWeb(channelId);
                    break;

                case "generate-with-zip":
                    {
                        var file = form.Files.FirstOrDefault();
                        if (file == null)
                            return Results.BadRequest(new { message = "zip file is required." });
                        if (!IsAllowedUpload(file, ".zip"))
                            return Results.BadRequest(new { message = "Invalid or oversized ZIP file." });

                        await using var stream = file.OpenReadStream();
                        var skipProgBalancing = string.Equals(
                            form["skipProgBalancing"].FirstOrDefault(),
                            "true",
                            StringComparison.OrdinalIgnoreCase);
                        var result = await GenerationClass.GenerateWithZipFromStreamAsync(
                            channelId,
                            file.FileName,
                            stream,
                            skipProgBalancing);
                        message = result.Message;

                        if (!string.IsNullOrWhiteSpace(result.ZipPath))
                        {
                            var fileName = $"{Path.GetFileNameWithoutExtension(result.ZipPath)}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.zip";
                            var destinationPath = Path.Combine(downloadRoot, fileName);
                            File.Copy(result.ZipPath, destinationPath, overwrite: true);
                            downloadUrl = ProtectedDownloadUrl(prefix, guildId, channelId, token, fileName);
                        }
                        break;
                    }

                case "discord":
                    message = Resource.Discord;
                    break;

                default:
                    return Results.BadRequest(new { message = "Unknown command." });
            }

            audit.Succeed();
            return Results.Ok(new { message, downloadUrl });
        });

        app.MapPost("/telemetry.php", () => Results.NoContent());

        _runTask = app.RunAsync();
        Console.WriteLine($"[Portal] Web portal running on port {port}. Culture={culture.Name}");
    }

    public static async Task StopAsync()
    {
        var app = _app;
        if (app == null)
            return;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await app.StopAsync(cts.Token);
        await app.DisposeAsync();
        _app = null;
        _runTask = null;
    }

    private record PortalSummary(
        string GuildId,
        string ChannelId,
        string UserId,
        DateTimeOffset LastUpdated,
        List<GameStatus> GameStatuses,
        List<PortalRecap> Recaps,
        List<PortalAliasItems> ReceivedItems,
        List<PortalAliasHints> Hints);

    private record PortalRecap(string Alias, List<PortalRecapGroup> Groups);
    private record PortalRecapItem(string Item, int Count);
    private record PortalRecapGroup(string FlagKey, string FlagLabel, List<PortalRecapItem> Items);

    private record PortalAliasItems(string Alias, List<PortalReceivedGroup> Groups);
    private record PortalReceivedItem(string Item, string Finder, string Location, string Game);
    private record PortalReceivedGroup(string FlagKey, string FlagLabel, List<PortalReceivedItem> Items);

    private record PortalAliasHints(string Alias, List<PortalHintItem> AsReceiver, List<PortalHintItem> AsFinder);
    private record PortalHintItem(string Finder, string Receiver, string Item, string Location, string Game);
    private record PortalPatchAlias(string Alias, string GameName, string Patch);
    private record PortalAliasExclusions(string Alias, List<string> Items);
    private record PortalThreadLink(string ChannelId, string ThreadName, string Url);

    private static async Task<string?> ResolveOwnedAliasAsync(
        string guildId,
        string channelId,
        string userId,
        string? requestedAlias)
    {
        if (string.IsNullOrWhiteSpace(requestedAlias))
            return null;

        var aliases = await RecapListCommands.GetAliasesForUserAsync(guildId, channelId, userId);
        return aliases.FirstOrDefault(alias =>
            string.Equals(alias, requestedAlias, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<AstPortalActor?> AuthorizePortalAsync(
        string guildId,
        string channelId,
        string token,
        AstAuthorizationLevel required)
    {
        var actor = await AstAuthorizationService.ResolvePortalActorAsync(guildId, channelId, token);
        return actor != null && AstAuthorizationService.IsAllowed(required, actor.Authorization)
            ? actor
            : null;
    }

    private static bool TryParsePersonalPortalPath(
        PathString path,
        out string guildId,
        out string channelId,
        out string token)
    {
        guildId = string.Empty;
        channelId = string.Empty;
        token = string.Empty;

        var segments = (path.Value ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length is not (4 or 5) ||
            !string.Equals(segments[0], "portal", StringComparison.OrdinalIgnoreCase) ||
            (segments.Length == 5 && !string.Equals(segments[4], "index.html", StringComparison.OrdinalIgnoreCase)) ||
            !ulong.TryParse(segments[1], out _) ||
            !ulong.TryParse(segments[2], out _) ||
            segments[3].Length is not (32 or 64) ||
            segments[3].Any(character => !Uri.IsHexDigit(character)))
        {
            return false;
        }

        guildId = segments[1];
        channelId = segments[2];
        token = segments[3];
        return true;
    }

    private static bool IsAllowedUpload(IFormFile file, string requiredExtension)
    {
        var safeFileName = Path.GetFileName(file.FileName);
        return file.Length > 0 &&
               file.Length <= Declare.WebPortalMaxUploadBytes &&
               IsExactSafeFileName(file.FileName, safeFileName) &&
               safeFileName.EndsWith(requiredExtension, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExactSafeFileName(string submittedName, string safeFileName)
    {
        return !string.IsNullOrWhiteSpace(safeFileName) &&
               string.Equals(submittedName, safeFileName, StringComparison.Ordinal) &&
               safeFileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    private static bool TryParseOptionalNonNegativeInt(string? value, out int? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
            return false;

        result = parsed;
        return true;
    }

    private static string ProtectedDownloadUrl(
        string prefix,
        string guildId,
        string channelId,
        string token,
        string fileName)
    {
        return $"{prefix}/api/portal/{guildId}/{channelId}/{token}/downloads/{WebUtility.UrlEncode(fileName)}";
    }

    private static string ResolveThreadName(SocketGuild? guild, string channelId)
    {
        if (guild == null || !ulong.TryParse(channelId, out var channelIdValue))
            return $"Thread {channelId}";

        var channel = guild.GetChannel(channelIdValue);
        return string.IsNullOrWhiteSpace(channel?.Name)
            ? $"Thread {channelId}"
            : channel.Name;
    }

    private static List<PortalRecapGroup> GroupRecapItemsByFlag(List<(string Item, long? Flag)> items)
    {
        return items
            .GroupBy(item => item.Flag?.ToString() ?? "Unknown")
            .OrderBy(group => FlagOrderIndex(group.Key))
            .Select(group => new PortalRecapGroup(
                group.Key,
                FlagLabel(group.Key),
                group
                    .GroupBy(item => item.Item)
                    .OrderBy(itemGroup => itemGroup.Key)
                    .Select(itemGroup => new PortalRecapItem(itemGroup.Key, itemGroup.Count()))
                    .ToList()))
            .ToList();
    }

    private static List<PortalReceivedGroup> GroupReceivedItemsByFlag(List<DisplayedItem> items)
    {
        return items
            .GroupBy(item => string.IsNullOrWhiteSpace(item.Flag) ? "Unknown" : item.Flag)
            .OrderBy(group => FlagOrderIndex(group.Key))
            .Select(group => new PortalReceivedGroup(
                group.Key,
                FlagLabel(group.Key),
                group.Select(item => new PortalReceivedItem(
                    item.Item,
                    item.Finder,
                    item.Location,
                    item.Game)).ToList()))
            .ToList();
    }

    private static int FlagOrderIndex(string flag)
    {
        return flag switch
        {
            "3" => 0,
            "1" => 1,
            "2" => 2,
            "4" => 3,
            "0" => 4,
            _ => 5
        };
    }

    private static string FlagLabel(string flag)
    {
        return flag switch
        {
            "3" => "Required",
            "1" => "Progression",
            "2" => "Useful",
            "4" => "Trap",
            "0" => "Filler",
            _ => "Unknown"
        };
    }

    private static string EnsureRobotsTxtFile()
    {
        var robotsPath = Path.Combine(Declare.WebPortalPath, "robots.txt");
        const string robotsContent = "User-agent: *\nDisallow: /\n";

        if (!File.Exists(robotsPath) || !string.Equals(File.ReadAllText(robotsPath), robotsContent, StringComparison.Ordinal))
            File.WriteAllText(robotsPath, robotsContent);

        return robotsPath;
    }

    // ----------------------------
    // Helpers localisation/culture
    // ----------------------------
    private static RequestLocalizationOptions CreateLocalizationOptions(CultureInfo defaultCulture)
    {
        var fallbackEn = CultureInfo.GetCultureInfo("en");
        var supported = new List<CultureInfo> { defaultCulture };
        if (!string.Equals(defaultCulture.Name, fallbackEn.Name, StringComparison.OrdinalIgnoreCase))
            supported.Add(fallbackEn);

        var options = new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(defaultCulture),
            SupportedCultures = supported,
            SupportedUICultures = supported
        };

        options.RequestCultureProviders.Clear();

        return options;
    }

    private static CultureInfo ResolveCulture(string language)
    {
        var token = (language ?? "en").Trim();

        token = token
            .Split(new[] { ':', ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()
            ?.Trim() ?? "en";

        token = token.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "en";
        token = token.Replace('_', '-');

        try
        {
            return CultureInfo.GetCultureInfo(token);
        }
        catch (CultureNotFoundException)
        {
            var primary = token.Split('-', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "en";
            try
            {
                return CultureInfo.GetCultureInfo(primary);
            }
            catch
            {
                return CultureInfo.GetCultureInfo("en");
            }
        }
    }
}

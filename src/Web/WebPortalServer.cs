using ArchipelagoSphereTracker.src.Resources;
using Discord;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
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

        Directory.CreateDirectory(Declare.WebPortalPath);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

        var app = builder.Build();
        _app = app;

        var portalFiles = new PhysicalFileProvider(Declare.WebPortalPath);

        app.UseDefaultFiles(new DefaultFilesOptions
        {
            FileProvider = portalFiles,
            RequestPath = "/portal"
        });

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = portalFiles,
            RequestPath = "/portal"
        });

        app.MapGet("/portal/{guildId}/{channelId}/commands.html", (string guildId, string channelId) =>
        {
            var commandsPath = Path.Combine(Declare.WebPortalPath, "commands.html");
            if (!File.Exists(commandsPath))
            {
                return Results.NotFound();
            }

            return Results.File(commandsPath, "text/html; charset=utf-8");
        });

        app.MapGet("/api/portal/{guildId}/{channelId}/{token}/summary", async (string guildId, string channelId, string token) =>
        {
            var userId = await PortalAccessCommands.GetUserIdByTokenAsync(guildId, channelId, token);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.NotFound();
            }

            var aliases = await RecapListCommands.GetAliasesForUserAsync(guildId, channelId, userId);
            var recapMap = await ReceiverAliasesCommands.GetUserAliasesWithItemsAsync(guildId, channelId, userId);

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
            var userId = await PortalAccessCommands.GetUserIdByTokenAsync(guildId, channelId, token);
            if (string.IsNullOrWhiteSpace(userId))
                return Results.NotFound();

            var form = await request.ReadFormAsync();
            var alias = form["alias"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(alias))
                return Results.BadRequest("alias is required");

            await RecapListCommands.DeleteRecapListAsync(guildId, channelId, userId, alias);
            return Results.Ok();
        });

        app.MapPost("/api/portal/{guildId}/{channelId}/commands/execute", async (string guildId, string channelId, HttpRequest request) =>
        {
            if (!Declare.EnableWebPortal)
            {
                return Results.BadRequest("Web portal is disabled.");
            }

            var form = await request.ReadFormAsync();
            var command = form["command"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(command) || string.IsNullOrWhiteSpace(channelId))
            {
                return Results.BadRequest("command and channelId are required.");
            }

            var downloadRoot = Path.Combine(Declare.WebPortalPath, guildId, "downloads");
            if (Declare.IsArchipelagoMode)
            {
                Directory.CreateDirectory(downloadRoot);
            }

            static string SanitizeFileName(string fileName)
            {
                return string.Join("_", fileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
            }

            string? downloadUrl = null;
            string message;

            switch (command)
            {
                case "add-url":
                {
                    var url = form["url"].FirstOrDefault() ?? string.Empty;
                    var threadName = form["threadName"].FirstOrDefault() ?? "Archipelago";
                    var threadType = form["threadType"].FirstOrDefault() ?? "Private";
                    var autoAddMembers = form["autoAddMembers"].FirstOrDefault() == "true";
                    var silent = form["silent"].FirstOrDefault() == "true";
                    var checkFrequency = form["checkFrequency"].FirstOrDefault() ?? "5m";
                    var userId = form["userId"].FirstOrDefault();

                    if (!ulong.TryParse(channelId, out var channelIdValue))
                    {
                        return Results.BadRequest("Invalid channelId.");
                    }

                    var channel = Declare.Client.GetChannel(channelIdValue) as ITextChannel;
                    if (channel == null)
                    {
                        return Results.BadRequest("Channel not found.");
                    }

                    IGuildUser? requestUser = null;
                    if (ulong.TryParse(userId, out var userIdValue))
                    {
                        requestUser = await channel.Guild.GetUserAsync(userIdValue);
                    }

                    var options = new UrlClass.UrlAddOptions(
                        url,
                        threadName,
                        threadType,
                        autoAddMembers,
                        silent,
                        checkFrequency,
                        requestUser);

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
                    {
                        downloadUrl = $"/portal/{guildId}/downloads/{WebUtility.UrlEncode(fileName)}";
                    }
                    break;
                }
                case "delete-yaml":
                {
                    var fileName = form["fileName"].FirstOrDefault();
                    message = YamlClass.DeleteYamlByName(channelId, fileName);
                    break;
                }
                case "clean-yamls":
                    message = YamlClass.CleanYamls(channelId);
                    break;
                case "send-yaml":
                {
                    var file = request.Form.Files.FirstOrDefault();
                    if (file == null)
                    {
                        return Results.BadRequest("yaml file is required.");
                    }

                    await using var stream = file.OpenReadStream();
                    message = await YamlClass.SendYamlFromStreamAsync(channelId, file.FileName, stream);
                    break;
                }
                case "download-template":
                {
                    var template = form["template"].FirstOrDefault() ?? string.Empty;
                    var safeName = SanitizeFileName(template);
                    var destinationPath = Path.Combine(downloadRoot, safeName);
                    message = YamlClass.DownloadTemplateToFile(template, destinationPath);
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        downloadUrl = $"/portal/{guildId}/downloads/{WebUtility.UrlEncode(safeName)}";
                    }
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
                    {
                        downloadUrl = $"/portal/{guildId}/downloads/{WebUtility.UrlEncode(fileName)}";
                    }
                    break;
                }
                case "send-apworld":
                {
                    var file = request.Form.Files.FirstOrDefault();
                    if (file == null)
                    {
                        return Results.BadRequest("apworld file is required.");
                    }

                    await using var stream = file.OpenReadStream();
                    message = await ApworldClass.SendApworldFromStreamAsync(file.FileName, stream);
                    break;
                }
                case "generate":
                {
                    var result = await GenerationClass.GenerateAsyncForWeb(channelId);
                    message = result.Message;
                    if (!string.IsNullOrWhiteSpace(result.ZipPath))
                    {
                        var fileName = $"{Path.GetFileNameWithoutExtension(result.ZipPath)}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.zip";
                        var destinationPath = Path.Combine(downloadRoot, fileName);
                        File.Copy(result.ZipPath, destinationPath, overwrite: true);
                        downloadUrl = $"/portal/{guildId}/downloads/{WebUtility.UrlEncode(fileName)}";
                    }
                    break;
                }
                case "test-generate":
                    message = await GenerationClass.TestGenerateAsyncForWeb(channelId);
                    break;
                case "generate-with-zip":
                {
                    var file = request.Form.Files.FirstOrDefault();
                    if (file == null)
                    {
                        return Results.BadRequest("zip file is required.");
                    }

                    await using var stream = file.OpenReadStream();
                    var result = await GenerationClass.GenerateWithZipFromStreamAsync(channelId, file.FileName, stream);
                    message = result.Message;
                    if (!string.IsNullOrWhiteSpace(result.ZipPath))
                    {
                        var fileName = $"{Path.GetFileNameWithoutExtension(result.ZipPath)}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.zip";
                        var destinationPath = Path.Combine(downloadRoot, fileName);
                        File.Copy(result.ZipPath, destinationPath, overwrite: true);
                        downloadUrl = $"/portal/{guildId}/downloads/{WebUtility.UrlEncode(fileName)}";
                    }
                    break;
                }
                case "discord":
                    message = Resource.Discord;
                    break;
                default:
                    return Results.BadRequest("Unknown command.");
            }

            return Results.Ok(new { message, downloadUrl });
        });

        app.MapPost("/telemetry.php", () => Results.NoContent());

        _runTask = app.RunAsync();
        Console.WriteLine($"[Portal] Web portal running on port {port}.");
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
}

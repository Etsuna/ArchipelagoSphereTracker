using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

public static class WebPortalServer
{
    private static WebApplication? _app;

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

        app.MapGet("/api/portal/{guildId}/{channelId}/{userId}/summary", async (string guildId, string channelId, string userId) =>
        {
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

        app.MapPost("/api/portal/{guildId}/{channelId}/{userId}/recap/delete", async (
            string guildId,
            string channelId,
            string userId,
            HttpRequest request) =>
        {
            var form = await request.ReadFormAsync();
            var alias = form["alias"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(alias))
                return Results.BadRequest("alias is required");

            await RecapListCommands.DeleteRecapListAsync(guildId, channelId, userId, alias);
            return Results.Ok();
        });

        app.MapPost("/telemetry.php", () => Results.NoContent());

        _ = app.RunAsync();
        Console.WriteLine($"[Portal] Web portal running on port {port}.");
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

using System.Text.Json.Nodes;

namespace TrackerLib.Services
{
    public static class TrackerItemsEnricher
    {
        // jsonContent = contenu JSON de /api/tracker/<id>
        public static async Task<List<DisplayedItem>> FetchAndEnrichItemsReceivedAsync(
            string guildId, string channelId, string jsonContent)
        {
            var list = new List<DisplayedItem>();

            var root = JsonNode.Parse(jsonContent)!;
            var teams = root["player_items_received"]?.AsArray();
            if (teams is null) return list;

            foreach (var teamNode in teams)
            {
                var players = teamNode?["players"]?.AsArray();
                if (players is null) continue;

                foreach (var p in players)
                {
                    int receiverSlot = p!["player"]!.GetValue<int>();
                    var items = p!["items"]?.AsArray();
                    if (items is null) continue;

                    foreach (var it in items)
                    {
                        // items: [ itemId, locationId, fromPlayer, flags ]
                        var arr = it!.AsArray();
                        long itemId = arr.ElementAtOrDefault(0)?.GetValue<long>() ?? -1;
                        long locationId = arr.ElementAtOrDefault(1)?.GetValue<long>() ?? -1;
                        int finderSlot = arr.ElementAtOrDefault(2)?.GetValue<int>() ?? -1;
                        int flagInt = arr.ElementAtOrDefault(3)?.GetValue<int>() ?? -1;

                        if (itemId < 0 || locationId < 0 || finderSlot < 0 || flagInt < 0)
                            continue;

                        // alias + jeu depuis la BDD (AliasChoicesTable)
                        var (receiverAlias, receiverGame) = await DatapackageStore.GetAliasAndGame(guildId, channelId, receiverSlot);
                        var (finderAlias, finderGame) = await DatapackageStore.GetAliasAndGame(guildId, channelId, finderSlot);

                        var datasetKeyFinder = await DatapackageStore.GetDatasetKey(guildId, channelId, finderGame);
                        var datasetKeyReceiver = await DatapackageStore.GetDatasetKey(guildId, channelId, receiverGame);

                        // noms depuis Datapackage* (BDD)
                        string locationName = await DatapackageStore.GetDatapackageLocationName(guildId, channelId, datasetKeyFinder, locationId);
                        string itemName = await DatapackageStore.GetDatapackageItemName(guildId, channelId, datasetKeyReceiver, itemId);

                        list.Add(new DisplayedItem
                        {
                            Finder = finderAlias,
                            Receiver = receiverAlias,
                            Item = itemName,
                            Location = locationName,
                            Game = finderGame,
                            Flag = flagInt.ToString()
                        });
                    }
                }
            }
            return list;
        }
    }
}

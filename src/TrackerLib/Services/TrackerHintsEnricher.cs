using System.Globalization;
using System.Text.Json.Nodes;

namespace TrackerLib.Services
{
    public static class TrackerHintsEnricher
    {
        // jsonContent = contenu JSON de /api/tracker/<id>
        public static async Task<List<HintStatus>> FetchAndEnrichHintsAsync(
            string guildId, string channelId, string jsonContent)
        {
            var hintsList = new List<HintStatus>();

            var root = JsonNode.Parse(jsonContent)!;
            var teams = root["hints"]?.AsArray();
            if (teams is null) return hintsList;

            foreach (var teamNode in teams)
            {
                var players = teamNode?["players"]?.AsArray();
                if (players is null) continue;

                foreach (var p in players)
                {
                    int receiverSlot = p!["player"]!.GetValue<int>();
                    var hints = p!["hints"]?.AsArray();
                    if (hints is null) continue;

                    foreach (var h in hints)
                    {
                        // hints: [ fromPlayer, toPlayer, location, item, found, entrance ]
                        int fromPlayer = h![0]!.GetValue<int>();
                        int toPlayer = h![1]!.GetValue<int>();
                        long locationId = h![2]!.GetValue<long>();
                        long itemId = h![3]!.GetValue<long>();
                        bool found = h![4]!.GetValue<bool>();
                        string entrance = h![5]!.GetValue<string>();

                        // Sanity: l'entrée doit concerner le receiver courant
                        if (toPlayer != receiverSlot)
                            continue;

                        var (receiverAlias, receiverGame) = await DatapackageStore.GetAliasAndGame(guildId, channelId, toPlayer);
                        var (finderAlias, finderGame) = await DatapackageStore.GetAliasAndGame(guildId, channelId, fromPlayer);

                        var datasetKeyFinder = await DatapackageStore.GetDatasetKey(guildId, channelId, finderGame);
                        var datasetKeyReceiver = await DatapackageStore.GetDatasetKey(guildId, channelId, receiverGame);

                        string locationName = await DatapackageStore.GetDatapackageLocationName(guildId, channelId, datasetKeyReceiver, locationId);
                        string itemName = await DatapackageStore.GetDatapackageItemName(guildId, channelId, datasetKeyFinder, itemId);

                        string entranceDisplay = string.IsNullOrWhiteSpace(entrance) ? "Vanilla" : entrance;

                        hintsList.Add(new HintStatus
                        {
                            Finder = finderAlias,
                            Receiver = receiverAlias,
                            Item = itemName,
                            Location = locationName,
                            Game = receiverGame,
                            Entrance = entranceDisplay,
                            Flag = found.ToString()
                        });
                    }
                }
            }
            return hintsList;
        }
    }
}

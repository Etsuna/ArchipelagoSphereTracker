namespace TrackerLib.Services
{
    public static class GetData
    {
        public static async Task<(List<DisplayedItem> Items, List<HintStatus>)> Data (string baseUrl, string trackerId, string guildId, string channelId)
        {
            var url = $"{baseUrl.TrimEnd('/')}/api/tracker/{trackerId}";
            using var http = new HttpClient();
            var json = await http.GetStringAsync(url).ConfigureAwait(false);

            var enrichedItems = await TrackerItemsEnricher.FetchAndEnrichItemsReceivedAsync(guildId, channelId, json);
            var enrichedHints = await TrackerHintsEnricher.FetchAndEnrichHintsAsync(guildId, channelId, json);

            return (enrichedItems, enrichedHints);
        }
    }
}

using System.Net.Http;

namespace TrackerLib.Services
{
    public static class TablePipeline
    {
        private static readonly HttpClient Http = new HttpClient(); // réutilisé

        public static async Task GetTableDataAsync(string guild, string channel, string baseUrl, string tracker, bool silent)
        {

        }
    }
}

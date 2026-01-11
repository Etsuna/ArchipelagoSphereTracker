using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TrackerLib.Services;
using Xunit;

public class DatapackageClientTests
{
    [Fact]
    public async Task FetchOneAsync_ParsesIdsAndGroups()
    {
        var json = "{" +
                   "\"checksum\":\"abc123\"," +
                   "\"item_name_to_id\":{\"Sword\":1,\"Shield\":\"2\"}," +
                   "\"location_name_to_id\":{\"Cave\":3}," +
                   "\"item_name_groups\":{\"Weapons\":[\"Sword\"]}," +
                   "\"location_name_groups\":{\"Dungeons\":[\"Cave\"]}" +
                   "}";

        using var http = new HttpClient(new StubHttpMessageHandler(json));
        var result = await DatapackageClient.FetchOneAsync("http://example", "fallback", http, CancellationToken.None);

        Assert.Equal("abc123", result.Checksum);
        Assert.Equal("Sword", result.ItemIdToName[1]);
        Assert.Equal("Shield", result.ItemIdToName[2]);
        Assert.Equal("Cave", result.LocationIdToName[3]);
        Assert.Contains("Weapons", result.ItemGroups.Keys);
        Assert.Contains("Dungeons", result.LocationGroups.Keys);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _response;

        public StubHttpMessageHandler(string response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var message = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_response)
            };

            return Task.FromResult(message);
        }
    }
}

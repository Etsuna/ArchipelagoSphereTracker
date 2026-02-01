using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ArchipelagoSphereTracker.src.TrackerLib.Services;
using Xunit;

public class TrackerDatapackageFetcherTests
{
    [Fact]
    public async Task GetRoots_ParsesTrackerRoot()
    {
        var json = "{\"datapackage\":{\"GameA\":{\"checksum\":\"abc\",\"version\":2}}}";
        using var http = new HttpClient(new StubHttpMessageHandler(json));

        var result = await TrackerDatapackageFetcher.getRoots("http://example", "tracker", http);

        Assert.NotNull(result.DataPackage);
        Assert.Equal("abc", result.DataPackage!["GameA"].Checksum);
        Assert.Equal(2, result.DataPackage["GameA"].Version);
    }

    [Fact]
    public void GetDatapackageChecksums_FiltersEmptyEntries()
    {
        var root = new TrackerDatapackageFetcher.TrackerRoot
        {
            DataPackage = new()
            {
                ["GameA"] = new TrackerDatapackageFetcher.PackageInfo { Checksum = "abc" },
                ["GameB"] = new TrackerDatapackageFetcher.PackageInfo { Checksum = "" },
                ["GameC"] = new TrackerDatapackageFetcher.PackageInfo { Checksum = null }
            }
        };

        var checksums = TrackerDatapackageFetcher.GetDatapackageChecksums(root);

        Assert.Single(checksums);
        Assert.Equal("abc", checksums["GameA"]);
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

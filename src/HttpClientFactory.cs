using System.Net;

internal static class HttpClientFactory
{
    private static readonly TimeSpan DefaultJsonTimeout = TimeSpan.FromSeconds(120);

    public static HttpClient CreateJsonClient(TimeSpan? timeout = null)
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip
                                     | DecompressionMethods.Deflate
                                     | DecompressionMethods.Brotli
        };

        return new HttpClient(handler)
        {
            Timeout = timeout ?? DefaultJsonTimeout
        };
    }
}

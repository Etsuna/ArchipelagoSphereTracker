using System.Net;
using System.Net.Sockets;

internal static class HttpClientFactory
{
    private static readonly TimeSpan DefaultJsonTimeout = TimeSpan.FromSeconds(120);

    public static HttpClient CreateJsonClient(TimeSpan? timeout = null)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip
                                     | DecompressionMethods.Deflate
                                     | DecompressionMethods.Brotli,
            UseProxy = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectCallback = ConnectToValidatedHostAsync
        };

        return new HttpClient(handler)
        {
            Timeout = timeout ?? DefaultJsonTimeout
        };
    }

    private static async ValueTask<Stream> ConnectToValidatedHostAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        var explicitlyAllowed = Declare.AllowedArchipelagoHosts.Contains(host);

        if (addresses.Length == 0 ||
            (!explicitlyAllowed && addresses.Any(address => !ArchipelagoUrlSecurity.IsPublicAddress(address))))
        {
            throw new HttpRequestException($"Connection to non-public host '{host}' was blocked.");
        }

        Exception? lastError = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken)
                    .ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException)
            {
                socket.Dispose();
                lastError = ex;
                if (ex is OperationCanceledException)
                    throw;
            }
        }

        throw new HttpRequestException($"Unable to connect to host '{host}'.", lastError);
    }
}

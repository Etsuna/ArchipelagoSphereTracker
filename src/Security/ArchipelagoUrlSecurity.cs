using System.Net;
using System.Net.Sockets;

public sealed record ValidatedArchipelagoRoomUrl(string BaseUrl, string RoomId, string Host);

public static class ArchipelagoUrlSecurity
{
    public static async Task<ValidatedArchipelagoRoomUrl?> ValidateRoomUrlAsync(
        string? value,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseRoomUrl(value, out var parsed) || parsed == null)
            return null;

        if (Declare.AllowedArchipelagoHosts.Contains(parsed.Host))
            return parsed;

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(parsed.Host, cancellationToken);
            return addresses.Length > 0 && addresses.All(IsPublicAddress) ? parsed : null;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    public static bool TryParseRoomUrl(string? value, out ValidatedArchipelagoRoomUrl? parsed)
    {
        parsed = null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 2 ||
            !string.Equals(segments[0], "room", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(segments[1]))
        {
            return false;
        }

        parsed = new ValidatedArchipelagoRoomUrl(
            uri.GetLeftPart(UriPartial.Authority),
            Uri.UnescapeDataString(segments[1]),
            uri.DnsSafeHost);
        return true;
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            return IsPublicAddress(address.MapToIPv4());

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
            return false;

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return !(
                bytes[0] == 0 ||
                bytes[0] == 10 ||
                bytes[0] == 127 ||
                (bytes[0] == 100 && bytes[1] is >= 64 and <= 127) ||
                (bytes[0] == 169 && bytes[1] == 254) ||
                (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0) ||
                (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2) ||
                (bytes[0] == 192 && bytes[1] == 168) ||
                (bytes[0] == 198 && bytes[1] is 18 or 19) ||
                (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) ||
                (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113) ||
                bytes[0] >= 224);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !(
                address.Equals(IPAddress.IPv6None) ||
                address.IsIPv6LinkLocal ||
                address.IsIPv6Multicast ||
                (bytes[0] & 0xfe) == 0xfc ||
                (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8));
        }

        return false;
    }
}

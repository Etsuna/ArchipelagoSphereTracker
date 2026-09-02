using System.Net;
using Xunit;

public class ArchipelagoUrlSecurityTests
{
    [Fact]
    public void TryParseRoomUrl_AcceptsExactHttpRoomPath()
    {
        var accepted = ArchipelagoUrlSecurity.TryParseRoomUrl(
            "https://archipelago.example:38281/room/room-token",
            out var parsed);

        Assert.True(accepted);
        Assert.NotNull(parsed);
        Assert.Equal("https://archipelago.example:38281", parsed.BaseUrl);
        Assert.Equal("room-token", parsed.RoomId);
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://user:password@example.test/room/id")]
    [InlineData("https://example.test/api/room_status/id")]
    [InlineData("https://example.test/room/id/extra")]
    [InlineData("https://example.test/room/id?target=internal")]
    [InlineData("https://example.test/room/id#fragment")]
    public void TryParseRoomUrl_RejectsUnexpectedShapes(string value)
    {
        Assert.False(ArchipelagoUrlSecurity.TryParseRoomUrl(value, out _));
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    public void IsPublicAddress_RejectsLocalAndPrivateRanges(string value)
    {
        Assert.False(ArchipelagoUrlSecurity.IsPublicAddress(IPAddress.Parse(value)));
    }

    [Theory]
    [InlineData("1.1.1.1")]
    [InlineData("8.8.8.8")]
    [InlineData("2606:4700:4700::1111")]
    public void IsPublicAddress_AcceptsPublicRanges(string value)
    {
        Assert.True(ArchipelagoUrlSecurity.IsPublicAddress(IPAddress.Parse(value)));
    }
}

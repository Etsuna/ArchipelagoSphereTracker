using System;
using Xunit;

public sealed class MetricsExporterActivityTests
{
    private static readonly DateTimeOffset ReferenceTime =
        new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("00:05:00", 300d)]
    [InlineData("1:00", 3600d)]
    [InlineData("2 days, 03:04:05", 183845d)]
    [InlineData("1 day, 00:00:00.500000", 86400.5d)]
    public void ParseActivityAgeSeconds_ParsesArchipelagoDurations(string value, double expected)
        => Assert.Equal(expected, MetricsExporter.ParseActivityAgeSeconds(value, ReferenceTime));

    [Fact]
    public void ParseActivityAgeSeconds_SupportsLegacyAbsoluteTimestamps()
    {
        var value = MetricsExporter.ParseActivityAgeSeconds(
            "Mon, 14 Sep 2026 12:00:00 GMT",
            ReferenceTime);

        Assert.Equal(86400d, value);
    }

    [Fact]
    public void ParseActivityAgeSeconds_ClampsFutureLegacyTimestamps()
    {
        var value = MetricsExporter.ParseActivityAgeSeconds(
            "Wed, 16 Sep 2026 12:00:00 GMT",
            ReferenceTime);

        Assert.Equal(0d, value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-duration")]
    public void ParseActivityAgeSeconds_RejectsMissingOrInvalidValues(string? value)
        => Assert.Null(MetricsExporter.ParseActivityAgeSeconds(value, ReferenceTime));
}

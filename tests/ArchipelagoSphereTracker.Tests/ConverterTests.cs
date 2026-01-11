using System;
using System.Globalization;
using System.Text.Json;
using TrackerLib.Converters;
using TrackerLib.Models;
using Xunit;

public class ConverterTests
{
    [Fact]
    public void PlayerEntryConverter_ReadsAndWritesArray()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new PlayerEntryConverter());

        var entry = JsonSerializer.Deserialize<PlayerEntry>("[\"Alice\",\"GameA\"]", options);

        Assert.NotNull(entry);
        Assert.Equal("Alice", entry!.Name);
        Assert.Equal("GameA", entry.Game);

        var json = JsonSerializer.Serialize(entry, options);
        Assert.Equal("[\"Alice\",\"GameA\"]", json);
    }

    [Fact]
    public void Rfc1123DateTimeOffsetConverter_ParsesAndWritesRfc1123()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new Rfc1123DateTimeOffsetConverter());

        var now = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var json = JsonSerializer.Serialize<DateTimeOffset?>(now, options);

        var expected = now.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture);
        Assert.Equal("\"" + expected + "\"", json);

        var parsed = JsonSerializer.Deserialize<DateTimeOffset?>("\"" + expected + "\"", options);
        Assert.Equal(now, parsed);

        var nullParsed = JsonSerializer.Deserialize<DateTimeOffset?>("null", options);
        Assert.Null(nullParsed);
    }
}

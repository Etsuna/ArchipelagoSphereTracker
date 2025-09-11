using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrackerLib.Converters
{
    public sealed class Rfc1123DateTimeOffsetConverter : JsonConverter<DateTimeOffset?>
    {
        public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            if (reader.TokenType != JsonTokenType.String) throw new JsonException("Expected string for DateTimeOffset.");

            var s = reader.GetString();
            if (string.IsNullOrWhiteSpace(s)) return null;

            if (DateTimeOffset.TryParseExact(
                    s, "r", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
                return dto;

            if (DateTimeOffset.TryParse(s, out dto)) return dto;

            throw new JsonException($"Invalid date: {s}");
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
        {
            if (value is null) { writer.WriteNullValue(); return; }
            writer.WriteStringValue(value.Value.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture));
        }
    }
}

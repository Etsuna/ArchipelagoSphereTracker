using System.Text.Json;
using System.Text.Json.Serialization;
using TrackerLib.Models;

namespace TrackerLib.Converters
{
    public sealed class PlayerEntryConverter : JsonConverter<PlayerEntry>
    {
        public override PlayerEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException("Expected StartArray for PlayerEntry.");

            reader.Read();
            if (reader.TokenType != JsonTokenType.String) throw new JsonException("Expected name string.");
            var name = reader.GetString() ?? "";

            reader.Read();
            if (reader.TokenType != JsonTokenType.String) throw new JsonException("Expected game string.");
            var game = reader.GetString() ?? "";

            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) { }

            return new PlayerEntry { Name = name, Game = game };
        }

        public override void Write(Utf8JsonWriter writer, PlayerEntry value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteStringValue(value.Name);
            writer.WriteStringValue(value.Game);
            writer.WriteEndArray();
        }
    }
}

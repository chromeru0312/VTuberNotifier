using System;
using System.Text.Json;

namespace VTuberNotifier
{
    public static class Extensions
    {
        public static void CheckStartToken(this ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException($"The start position of json is invalid. position:{GetPosition(reader)}");
            reader.Read();
        }
        public static bool IsEndToken(this ref Utf8JsonReader reader)
        {
            return reader.TokenType == JsonTokenType.EndObject;
        }
        public static void CheckEndToken(this ref Utf8JsonReader reader)
        {
            if (!reader.IsEndToken())
                throw new JsonException($"The end position of json is invalid. position:{GetPosition(reader)}");
        }
        private static string GetPosition(Utf8JsonReader reader)
        {
            return $"({reader.CurrentState}[{reader.CurrentDepth}])";
        }
        public static T GetNextValue<T>(this ref Utf8JsonReader reader, JsonSerializerOptions options = null)
        {
            if (typeof(T) == typeof(DateTime))
                return (T)(object)DateTime.Parse(reader.GetNextValue<string>(options), Settings.Data.Culture);

            reader.Read();
            var value = JsonSerializer.Deserialize<T>(ref reader, options);
            reader.Read();
            return value;
        }
        public static (string, T) GetNextValueAndPropartyName<T>(this ref Utf8JsonReader reader, JsonSerializerOptions options = null)
        {
            if (typeof(T) == typeof(DateTime))
            {
                var (p, s) = GetNextValueAndPropartyName<string>(ref reader, options);
                return (p, (T)(object)DateTime.Parse(s, Settings.Data.Culture));
            }

            var propaty = reader.GetString();
            reader.Read();
            var value = JsonSerializer.Deserialize<T>(ref reader, options);
            reader.Read();
            return (propaty, value);
        }

        public static void WriteValue<T>(this Utf8JsonWriter writer, string propaty, T value, JsonSerializerOptions options = null)
        {
            writer.WritePropertyName(propaty);
            if (value != null) JsonSerializer.Serialize(writer, value, options);
            else writer.WriteNullValue();
        }
    }
}
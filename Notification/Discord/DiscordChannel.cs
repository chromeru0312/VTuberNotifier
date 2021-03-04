using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VTuberNotifier.Notification.Discord
{
    [Serializable]
    [JsonConverter(typeof(DiscordChannelConverter))]
    public class DiscordChannel : NotificationAddress, IEquatable<DiscordChannel>
    {
        public ulong GuildId { get; }
        public ulong ChannelId { get; }

        public DiscordChannel(ulong guild, ulong ch) : this(guild, ch, new()) { }
        private DiscordChannel(ulong guild, ulong ch, Dictionary<Type, string> dic) : base(dic)
        {
            GuildId = guild;
            ChannelId = ch;
        }

        public override bool Equals(object obj)
        {
            return obj is DiscordChannel dc && Equals(dc);
        }
        public bool Equals(DiscordChannel other)
        {
            return GuildId == other.GuildId && ChannelId == other.ChannelId;
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(GuildId, ChannelId);
        }

        public class DiscordChannelConverter : NotificationAddressConverter<DiscordChannel>
        {
            public override DiscordChannel Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

                reader.Read();
                reader.Read();
                var guild = reader.GetUInt64();
                reader.Read();
                reader.Read();
                var channel = reader.GetUInt64();
                var dic = ReadBase(ref reader);

                reader.Read();
                if (reader.TokenType == JsonTokenType.EndObject) return new(guild, channel, dic);
                throw new JsonException();
            }

            public override void Write(Utf8JsonWriter writer, DiscordChannel value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                writer.WriteNumber("Guild", value.GuildId);
                writer.WriteNumber("Channel", value.ChannelId);
                WriteBase(writer, value);

                writer.WriteEndObject();
            }
        }
    }
}

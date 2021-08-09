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
        public bool IsEditContent { get; }

        public DiscordChannel(ulong guild, ulong ch, bool edit = false) : this(guild, ch, edit, new()) { }
        private DiscordChannel(ulong guild, ulong ch, bool edit, Dictionary<Type, string> dic) : base(dic)
        {
            GuildId = guild;
            ChannelId = ch;
            IsEditContent = edit;
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
                reader.CheckStartToken();

                var guild = reader.GetNextValue<ulong>(options);
                var channel = reader.GetNextValue<ulong>(options);
                var edit = reader.GetNextValue<bool>(options);
                var dic = ReadBase(ref reader, options);

                reader.CheckEndToken();
                return new(guild, channel, edit, dic);
            }

            public override void Write(Utf8JsonWriter writer, DiscordChannel value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                writer.WriteNumber("Guild", value.GuildId);
                writer.WriteNumber("Channel", value.ChannelId);
                writer.WriteBoolean("IsEditContent", value.IsEditContent);
                WriteBase(writer, value);

                writer.WriteEndObject();
            }
        }
    }
}
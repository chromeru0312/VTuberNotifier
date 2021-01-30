using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VTuberNotifier.Liver;

namespace VTuberNotifier.Discord
{
    public class DiscordNotify
    {
        public static IReadOnlyDictionary<LiverDetail, IReadOnlyList<DiscordChannel>> NotifyChannelList { get; private set; } = null;

        internal static void LoadChannelList()
        {
            if (NotifyChannelList != null) return;
            static KeyValuePair<LiverDetail, IReadOnlyList<DiscordChannel>> func(LiverDetail l)
                => new KeyValuePair<LiverDetail, IReadOnlyList<DiscordChannel>>(l, new List<DiscordChannel>());

            if (DataManager.Instance.TryDataLoad($"NotifyChannelList", out Dictionary<LiverDetail, IReadOnlyList<DiscordChannel>> dic))
                NotifyChannelList = dic;
            else NotifyChannelList = new Dictionary<LiverDetail, IReadOnlyList<DiscordChannel>>(LiverData.GetAllLiversList().Select(func));
        }

        public static async Task NotifyInformation<T>(LiverDetail liver, T value)
        {
            if (!NotifyChannelList.ContainsKey(liver)) return;
            foreach(var dc in NotifyChannelList[liver])
            {
                if (dc.MsgContentList.ContainsKey(typeof(T))) continue;
                var guild = SettingData.DiscordClient.GetGuild(dc.GuildId);
                var ch = guild.GetTextChannel(dc.ChannelId);
                string content;

                if (value is IDiscordContent c)
                {
                    var format = dc.MsgContentList[typeof(T)];
                    content = format == null ? c.GetDiscordContent() : c.ConvertContent(format);
                }
                else content = value.ToString();

                await ch.SendMessageAsync(content);
            }

            var g = SettingData.DiscordClient.GetGuild(755356617318596659);
            var chan = g.GetTextChannel(805006518911107093);
            await chan.SendMessageAsync((value as IDiscordContent).GetDiscordContent());
        }

        public static bool AddNotifyList(LiverDetail liver, DiscordChannel channel)
        {
            var list = new List<DiscordChannel>(NotifyChannelList[liver]);
            if (list.Contains(channel)) return false;
            list.Add(channel);
            NotifyChannelList = new Dictionary<LiverDetail, IReadOnlyList<DiscordChannel>>(NotifyChannelList) { [liver] = list };
            return true;
        }
    }

    [Serializable]
    [JsonConverter(typeof(DiscordChannelConverter))]
    public class DiscordChannel : IEquatable<DiscordChannel>
    {
        public ulong GuildId { get; }
        public ulong ChannelId { get; }
        public IReadOnlyDictionary<Type, string> MsgContentList { get; private set; }

        public DiscordChannel(ulong guild, ulong ch) : this (guild, ch, new()) { }
        private DiscordChannel(ulong guild, ulong ch, Dictionary<Type, string> dic)
        {
            GuildId = guild;
            ChannelId = ch;
            MsgContentList = dic;
        }

        public void SetContent(Type type, string content)
        {
            MsgContentList = new Dictionary<Type, string>(MsgContentList) { [type] = content };
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

        public class DiscordChannelConverter : JsonConverter<DiscordChannel>
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
                reader.Read();

                if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();
                var dic = new Dictionary<Type, string>();
                while (true)
                {
                    reader.Read();
                    if (reader.TokenType == JsonTokenType.EndObject) break;
                    var t = Type.GetType(reader.GetString());
                    reader.Read();
                    dic.Add(t, reader.GetString());
                }

                reader.Read();
                if (reader.TokenType == JsonTokenType.EndObject) return new(guild, channel, dic);
                throw new JsonException();
            }

            public override void Write(Utf8JsonWriter writer, DiscordChannel value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                writer.WriteNumber("Guild", value.GuildId);
                writer.WriteNumber("Channel", value.ChannelId);

                writer.WriteStartObject("Content");
                foreach(var (type, content) in value.MsgContentList) writer.WriteString(type.FullName, content);
                writer.WriteEndObject();

                writer.WriteEndObject();
            }
        }
    }
}

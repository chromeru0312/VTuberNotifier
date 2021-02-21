using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VTuberNotifier.Liver;
using VTuberNotifier.Watcher.Event;

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

            if (DataManager.Instance.TryDataLoad("NotifyChannelList", out IEnumerable<KeyValuePair<int, List<DiscordChannel>>> dic))
                NotifyChannelList = new Dictionary<LiverDetail, IReadOnlyList<DiscordChannel>>(
                        dic.Select(p => new KeyValuePair<LiverDetail, IReadOnlyList<DiscordChannel>>(
                            LiverData.GetAllLiversList().First(l => l.Id == p.Key), new List<DiscordChannel>(p.Value))));
            else NotifyChannelList = new Dictionary<LiverDetail, IReadOnlyList<DiscordChannel>>(LiverData.GetAllLiversList().Select(func));
        }

        public static async Task NotifyInformation(LiverDetail liver, IEventBase value)
        {
            if (!NotifyChannelList.ContainsKey(liver)) return;
            foreach(var dc in NotifyChannelList[liver])
            {
                if (!dc.GetContent(value.GetType(), out var only, out var content)) continue;
                var guild = SettingData.DiscordClient.GetGuild(dc.GuildId);
                var ch = guild.GetTextChannel(dc.ChannelId);

                var l = only ? liver : null;
                content = content != "" ? value.ConvertContent(content, l) : value.GetDiscordContent(l);

                await ch.SendMessageAsync(content);
            }
        }

        public static bool AddNotifyList(LiverDetail liver, DiscordChannel channel)
        {
            if (NotifyChannelList[liver].Contains(channel)) return false;
            NotifyChannelList = new Dictionary<LiverDetail, IReadOnlyList<DiscordChannel>>(NotifyChannelList)
            {
                [liver] = new List<DiscordChannel>(NotifyChannelList[liver]) { channel }
            };
            var data = NotifyChannelList.Select(p => new KeyValuePair<int, List<DiscordChannel>>(p.Key.Id, new(p.Value)));
            DataManager.Instance.DataSave("NotifyChannelList", data, true);
            return true;
        }
        public static bool UpdateNotifyList(LiverDetail liver, DiscordChannel channel)
        {
            var list = new List<DiscordChannel>(NotifyChannelList[liver]);
            if (!list.Contains(channel)) return false;
            var ch = list.FirstOrDefault(c => channel.Equals(c));
            foreach(var type in channel.MsgContentList.Keys) 
                if(ch.GetContent(type, out var b, out var c)) ch.SetContent(type, b, c);
            list.Remove(channel);
            list.Add(ch);
            NotifyChannelList = new Dictionary<LiverDetail, IReadOnlyList<DiscordChannel>>(NotifyChannelList) { [liver] = list };
            var data = NotifyChannelList.Select(p => new KeyValuePair<int, List<DiscordChannel>>(p.Key.Id, new(p.Value)));
            DataManager.Instance.DataSave("NotifyChannelList", data, true);
            return true;
        }
        public static bool RemoveNotifyList(LiverDetail liver, DiscordChannel channel)
        {
            var list = new List<DiscordChannel>(NotifyChannelList[liver]);
            if (!list.Contains(channel)) return false;
            list.Remove(channel);
            NotifyChannelList = new Dictionary<LiverDetail, IReadOnlyList<DiscordChannel>>(NotifyChannelList) { [liver] = list };
            var data = NotifyChannelList.Select(p => new KeyValuePair<int, List<DiscordChannel>>(p.Key.Id, new(p.Value)));
            DataManager.Instance.DataSave("NotifyChannelList", data, true);
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

        public bool GetContent(Type type, out bool only, out string content)
        {
            only = false;
            content = null;
            if (!MsgContentList.ContainsKey(type)) return false;
            var str = MsgContentList[type];
            if (str.StartsWith("@F"))
            {
                only = false;
                content = str[2..];
            }
            else
            {
                only = true;
                if (str.StartsWith("@T")) content = str[2..];
                else content = str;
            }
            return true;
        }
        public void SetContent(Type type, bool only, string content)
        {
            var s = only ? "@T" : "@F";
            MsgContentList = new Dictionary<Type, string>(MsgContentList) { [type] = s + content };
        }
        public void RemoveContent(Type type)
        {
            var dic = new Dictionary<Type, string>(MsgContentList);
            dic.Remove(type);
            MsgContentList = dic;
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

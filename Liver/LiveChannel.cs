using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace VTuberNotifier.Liver
{
    public static class LiveChannel
    {
        private static HashSet<LiveChannelDetail> LiverChannels = null;

        private static Task LoadLiveChannels()
        {
            LiverChannels = null;
            if (DataManager.Instance.TryDataLoad("youtube/LiveChannelList", out HashSet<LiveChannelDetail> livers))
                LiverChannels = livers;
            else LiverChannels = new();
            return Task.CompletedTask;
        }

        internal static async Task<int> AddLiveChannel(string name, string youtube, string twitter)
        {
            if (LiverChannels.FirstOrDefault(l => l.YouTubeId == youtube) == null)
            {
                var id = LiverChannels.Max(l => l.Id) + 1;
                LiverChannels.Add(new(id == 1 ? 10000001 : id, name, youtube, twitter));
                await DataManager.Instance.DataSaveAsync("youtube/LiveChannelList", LiverChannels, true);
                return 201;
            }
            return 400;
        }
        internal static async Task<int> UpdateLiveChannel(int id, string name = null, string youtube = null, string twitter = null)
        {
            var set = GetLiveChannelList();
            var ch = set.FirstOrDefault(c => c.Id == id);
            if (ch == null) return 404;
            set.Remove(ch);
            var b = set.Add(new(id, name ?? ch.Name, youtube ?? ch.YouTubeId, twitter ?? ch.TwitterId));
            if (b)
            {
                LiverChannels = set;
                await DataManager.Instance.DataSaveAsync("youtube/LiveChannelList", LiverChannels, true);
                return 200;
            }
            return 400;
        }
        internal static async Task<int> DeleteLiveChannel(int id)
        {
            var set = GetLiveChannelList();
            var ch = set.FirstOrDefault(c => c.Id == id);
            if (ch == null) return 404;
            set.Remove(ch);
            LiverChannels = set;
            await DataManager.Instance.DataSaveAsync("youtube/LiveChannelList", LiverChannels, true);
            return 200;
        }

        public static HashSet<LiveChannelDetail> GetLiveChannelList()
        {
            if (LiverChannels == null) LoadLiveChannels().Wait();
            return LiverChannels;
        }
    }

    [Serializable]
    [JsonConverter(typeof(LiveChannelDetailConverter))]
    public class LiveChannelDetail : Address
    {
        public LiveChannelDetail(int id, string name, string youtube, string twitter)
            : base(id, name, youtube, twitter) { }

        public class LiveChannelDetailConverter : JsonConverter<LiveChannelDetail>
        {
            public override LiveChannelDetail Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                reader.CheckStartToken();

                var id = reader.GetNextValue<int>(options);
                var name = reader.GetNextValue<string>(options);
                var ytid = reader.GetNextValue<string>(options);
                var twid = reader.GetNextValue<string>(options);

                reader.CheckEndToken();
                return new(id, name, ytid, twid);
            }

            public override void Write(Utf8JsonWriter writer, LiveChannelDetail value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                writer.WriteNumber("Id", value.Id);
                writer.WriteString("Name", value.Name);
                writer.WriteString("YouTubeId", value.YouTubeId);
                writer.WriteString("TwitterId", value.TwitterId);

                writer.WriteEndObject();
            }
        }
    }
}
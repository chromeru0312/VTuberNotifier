using Discord;
using Google.Apis.YouTube.v3.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web;
using VTuberNotifier.Notification;
using VTuberNotifier.Liver;
using VTuberNotifier.Watcher.Event;

namespace VTuberNotifier.Watcher.Feed
{
    public class YouTubeFeed
    {
        public enum EventType
        {
            Create = 0, Change = 1, Remove = 2, Unknown = -1
        }
        public static YouTubeFeed Instance { get; private set; }
        public IReadOnlyDictionary<Address, IReadOnlyList<YouTubeItem>> FoundLiveList { get; private set; }
        public IReadOnlyList<YouTubeItem> FutureLiveList { get; private set; }

        private YouTubeFeed()
        {
            var dic = new Dictionary<Address, IReadOnlyList<YouTubeItem>>();
            foreach (var liver in LiverData.GetAllLiversList())
            {
                var id = liver.YouTubeId;
                if (!DataManager.Instance.TryDataLoad($"youtube/{id}", out List<YouTubeItem> list))
                    list = new();
                dic.Add(liver, list);
            }
            foreach (var group in LiverGroup.GroupList)
            {
                var id = group.YouTubeId;
                if (id == null) continue;
                if (!DataManager.Instance.TryDataLoad($"youtube/{id}", out List<YouTubeItem> list))
                    list = new();
                dic.Add(group, list);
            }
            FoundLiveList = dic;

            if (!DataManager.Instance.TryDataLoad("youtube/FutureLiveList", out List<YouTubeItem> future))
                future = new();
            FutureLiveList = future;
        }
        public static void CreateInstance()
        {
            if (Instance != null) return;
            Instance = new YouTubeFeed();
        }

        public async Task<bool> RegisterNotification(string id)
        {
            try
            {
                var enc = Encoding.UTF8;
                var topic = HttpUtility.UrlEncode($"https://www.youtube.com/xml/feeds/videos.xml?channel_id={id}", enc);
                var callback = HttpUtility.UrlEncode(SettingData.NotificationCallback, enc);
                var url = "https://pubsubhubbub.appspot.com/subscribe";
                string data = $"hub.mode=subscribe&hub.verify=async&hub.callback={callback}&hub.topic={topic}";

                using var wc = SettingData.GetWebClient();
                wc.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                await wc.UploadStringTaskAsync(url, data);

                await LocalConsole.Log(this, new(LogSeverity.Info, "NotificationRegister", $"Registerd: {id}"));
                return true;
            }
            catch (Exception ex)
            {
                await LocalConsole.Log(this, new(LogSeverity.Error, "NotificationRegister", $"Missing Register: {id}", ex));
                return false;
            }
        }

        public EventType CheckNewLive(string id, out YouTubeItem item)
        {
            item = null;
            var req = SettingData.YouTubeService.Videos.List("contentDetails, liveStreamingDetails, snippet");
            req.Id = id;
            req.MaxResults = 1;

            var res = req.Execute();
            if (res.Items.Count == 0) return EventType.Remove;
            var video = res.Items[0];
            Address ch = LiverData.GetLiverFromYouTubeId(video.Snippet.ChannelId);
            if (ch == null) ch = LiverGroup.GroupList.FirstOrDefault(g => g.YouTubeId == id);
            if (ch == null || !FoundLiveList.ContainsKey(ch)) return EventType.Unknown;

            item = new(video);
            var old = FoundLiveList[ch].FirstOrDefault(v => v.VideoId == id);
            var list = new List<YouTubeItem>(FoundLiveList[ch]) { item };
            EventType type;
            bool add;
            if (old != null)
            {
                list.Remove(old);
                var future = new List<YouTubeItem>(FutureLiveList);
                if(future.Contains(old))
                {
                    future.Remove(old);
                    FutureLiveList = future;
                }
                add = true;
                type = EventType.Change;
            }
            else
            {
                add = item.Mode != YouTubeItem.YouTubeMode.Video && video.LiveStreamingDetails.ActualStartTime == null
                    && item.LiveStartDate > DateTime.Now && video.Snippet.LiveBroadcastContent == "upcoming";
                type = EventType.Create;
            }
            FoundLiveList = new Dictionary<Address, IReadOnlyList<YouTubeItem>>(FoundLiveList) { [ch] = list };
            DataManager.Instance.DataSaveAsync($"youtube/{ch.YouTubeId}", list, true).Wait();

            if (add)
            {
                FutureLiveList = new List<YouTubeItem>(FutureLiveList) { item };
                DataManager.Instance.DataSaveAsync("youtube/FutureLiveList", FutureLiveList, true).Wait();
            }
            return type;
        }

        public List<YouTubeEvent> CheckLiveChanged()
        {
            var list = new List<YouTubeItem>(FutureLiveList);
            var evts = new List<YouTubeEvent>();
            var tasks = new Task<YouTubeEvent>[list.Count];
            var delc = 0;
            for (int i = 0; i < list.Count; i++) tasks[i] = CheckFutureLive(list[i]);
            for (int i = 0; i < tasks.Length; i++)
            {
                var e = tasks[i].Result;
                if (e != null)
                {
                    if (e is YouTubeChangeInfoEvent ce)
                    {
                        list.Remove(ce.OldItem);
                        list.Insert(i - delc, ce.Item);
                        if (ce.EventChangeType != YouTubeChangeInfoEvent.ChangeType.Other)
                            evts.Add(e);
                    }
                    else
                    {
                        list.Remove(e.Item);
                        evts.Add(e);
                    }
                    delc++;
                }
            }
            if(evts.Count != 0)
            {
                FutureLiveList = list;
                DataManager.Instance.DataSaveAsync("youtube/FutureLiveList", list, true).Wait();
            }
            return evts;

            static async Task<YouTubeEvent> CheckFutureLive(YouTubeItem item)
            {
                var req = SettingData.YouTubeService.Videos.List("contentDetails, liveStreamingDetails, snippet");
                req.Id = item.VideoId;
                req.MaxResults = 1;

                var res = await req.ExecuteAsync();
                if (res.Items.Count == 0) return new YouTubeDeleteLiveEvent(item);
                var video = res.Items[0];
                if (video.Snippet.LiveBroadcastContent == "live") return new YouTubeStartLiveEvent(item);
                else if (!item.Equals(video)) return new YouTubeChangeInfoEvent(item, new(video));
                else return null;
            }
        }
    }

    [Serializable]
    [JsonConverter(typeof(YouTubeItemConverter))]
    public class YouTubeItem : IEquatable<YouTubeItem>, INotificationContent
    {
        public enum YouTubeMode { Video, Premire, Live }

        public YouTubeMode Mode { get; }
        public string VideoId { get; }
        public string VideoTitle { get; }
        public string VideoDescription { get; }
        public bool IsOfficialChannel { get; }
        public Address VideoChannel { get; }
        public string VideoChannelName { get; }
        public DateTime PublishedDate { get; }
        public string LiveChatId { get; }
        public DateTime LiveStartDate { get; }
        public bool IsCollaboration { get; }
        public IReadOnlyList<LiverDetail> Livers { get; }

        [JsonIgnore]
        public IReadOnlyDictionary<string, string> ContentFormat => new Dictionary<string, string>()
            {
                { "Date", (this as INotificationContent).ConvertDuringDateTime(LiveStartDate) },
                { "Title", VideoTitle }, { "VideoId", VideoId }, { "ChannelId", VideoChannel.YouTubeId },
                { "ChannelName", VideoChannelName }, { "URL", $"https://www.youtube.com/watch/{VideoId}" }
            };
        [JsonIgnore]
        public IReadOnlyDictionary<string, IEnumerable<object>> ContentFormatEnumerator
            => new Dictionary<string, IEnumerable<object>>()
            {
                { "Livers", Livers },
            };
        [JsonIgnore]
        public IReadOnlyDictionary<string, Func<LiverDetail, IEnumerable<string>>> ContentFormatEnumeratorFunc
            => new Dictionary<string, Func<LiverDetail, IEnumerable<string>>>();

        public YouTubeItem(Video video)
        {
            VideoId = video.Id;
            VideoTitle = video.Snippet.Title;
            VideoDescription = video.Snippet.Description;
            VideoChannelName = video.Snippet.ChannelTitle;
            PublishedDate = video.Snippet.PublishedAt != null ?
                (DateTime)video.Snippet.PublishedAt : DateTime.MinValue;

            if (video.ContentDetails != null)
            {
                if (video.LiveStreamingDetails != null)
                {
                    if(video.ContentDetails.Duration == "P0D") Mode = YouTubeMode.Live;
                    else Mode = YouTubeMode.Premire;
                    LiveChatId = video.LiveStreamingDetails.ActiveLiveChatId;
                    LiveStartDate = video.LiveStreamingDetails.ScheduledStartTime != null ?
                        (DateTime)video.LiveStreamingDetails.ScheduledStartTime : PublishedDate;
                }
                else Mode = YouTubeMode.Video;
            }

            var chid = video.Snippet.ChannelId;
            var channel = LiverData.GetAllLiversList().FirstOrDefault(l => l.YouTubeId == chid);
            List<LiverDetail> livers = new(LiverData.GetAllLiversList()), res = channel == null ? new() : new() { channel };
            foreach (var liver in livers)
            {
                if (liver == channel) continue;

                if (VideoDescription.Contains(liver.YouTubeId) || VideoDescription.Contains('@' + liver.ChannelName) ||
                    VideoTitle.Contains(liver.Name))
                    res.Add(liver);
            }
            Livers = res;

            var group = LiverGroup.GroupList.FirstOrDefault(g => g.YouTubeId == chid);
            if (group != null)
            {
                VideoChannel = group;
                IsOfficialChannel = true;
                IsCollaboration = true;
            }
            else
            {
                VideoChannel = channel ?? throw new NullReferenceException();
                IsOfficialChannel = false;
                IsCollaboration = Livers.Count == 1;
            }
        }
        private YouTubeItem(YouTubeMode mode, string id, string title, string description, bool official, Address ch, string ch_name,
            DateTime publish, string chat_id, DateTime start_date, List<LiverDetail> livers)
        {
            Mode = mode;
            VideoId = id;
            VideoTitle = title;
            VideoDescription = description;
            IsOfficialChannel = official;
            VideoChannel = ch;
            VideoChannelName = ch_name;
            PublishedDate = publish;
            LiveChatId = chat_id;
            LiveStartDate = start_date;
            Livers = livers;
            IsCollaboration = official || Livers.Count == 1;
        }

        public override bool Equals(object obj)
        {
            return (obj is YouTubeItem item && Equals(item)) || (obj is Video video && Equals(video));
        }
        public bool Equals(YouTubeItem other)
        {
            return VideoId == other.VideoId && Livers == other.Livers && VideoTitle == other.VideoTitle &&
                VideoDescription == other.VideoDescription && LiveStartDate == other.LiveStartDate;
        }
        public bool Equals(Video other)
        {
            return other.Snippet != null && VideoId == other.Id && VideoTitle == other.Snippet.Title &&
                VideoDescription == other.Snippet.Description && other.LiveStreamingDetails != null &&
                LiveStartDate == other.LiveStreamingDetails.ScheduledStartTime;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(VideoId, VideoTitle, VideoDescription, LiveStartDate);
        }

        public class VideoIdComparer : IEqualityComparer<YouTubeItem>
        {
            internal VideoIdComparer() { }

            public bool Equals(YouTubeItem x, YouTubeItem y)
            {
                return x.VideoId == y.VideoId;
            }

            public int GetHashCode(YouTubeItem obj)
            {
                return obj.VideoId.GetHashCode();
            }
        }

        public class YouTubeItemConverter : JsonConverter<YouTubeItem>
        {
            public override YouTubeItem Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

                reader.Read();
                reader.Read();
                var mode = (YouTubeMode)reader.GetInt32();
                reader.Read();
                reader.Read();
                var id = reader.GetString();
                reader.Read();
                reader.Read();
                var title = reader.GetString();
                reader.Read();
                reader.Read();
                var description = reader.GetString();
                reader.Read();
                reader.Read();
                var official = reader.GetBoolean();
                reader.Read();
                reader.Read();
                Address channel;
                if (official)
                {
                    var gid = JsonSerializer.Deserialize<Address>(ref reader, options).Id;
                    channel = LiverGroup.GroupList.FirstOrDefault(g => g.Id == gid);
                }
                else channel = JsonSerializer.Deserialize<LiverDetail>(ref reader, options);
                reader.Read();
                reader.Read();
                var ch_name = reader.GetString();
                reader.Read();
                reader.Read();
                var publish = DateTime.Parse(reader.GetString());
                reader.Read();
                reader.Read();
                var chat_id = reader.GetString();
                reader.Read();
                reader.Read();
                var livestart = DateTime.Parse(reader.GetString());
                reader.Read();
                reader.Read();
                var livers = JsonSerializer.Deserialize<List<LiverDetail>>(ref reader, options);

                reader.Read();
                if (reader.TokenType == JsonTokenType.EndObject)
                    return new(mode, id, title, description, official, channel, ch_name, publish, chat_id, livestart, livers);
                throw new JsonException();
            }

            public override void Write(Utf8JsonWriter writer, YouTubeItem value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                writer.WriteNumber("Mode", (int)value.Mode);
                writer.WriteString("VideoId", value.VideoId);
                writer.WriteString("VideoTitle", value.VideoTitle);
                writer.WriteString("VideoDescription", value.VideoDescription);

                writer.WriteBoolean("IsOfficialChannel", value.IsOfficialChannel);
                writer.WritePropertyName("VideoChannel");
                if (value.IsOfficialChannel) JsonSerializer.Serialize(writer, value.VideoChannel, options);
                else JsonSerializer.Serialize(writer, (LiverDetail)value.VideoChannel, options);
                writer.WriteString("VideoChannelName", value.VideoChannelName);

                writer.WriteString("PublishedDate", value.PublishedDate.ToString("G"));
                writer.WriteString("LiveChatId", value.LiveChatId);
                writer.WriteString("LiveStartDate", value.LiveStartDate.ToString("G"));
                writer.WritePropertyName("Livers");
                JsonSerializer.Serialize(writer, value.Livers, options);

                writer.WriteEndObject();
            }
        }
    }
}

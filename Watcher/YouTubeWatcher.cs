using Google.Apis.YouTube.v3.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VTuberNotifier.Notification;
using VTuberNotifier.Liver;
using VTuberNotifier.Watcher.Event;

namespace VTuberNotifier.Watcher
{
    public class YouTubeWatcher
    {
        public static YouTubeWatcher Instance { get; private set; }
        public IReadOnlyDictionary<Address, IReadOnlyList<YouTubeItem>> FoundLiveList { get; private set; }
        public IReadOnlyList<YouTubeItem> FutureLiveList { get; private set; }

        private YouTubeWatcher()
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
            foreach (var ch in LiveChannel.GetLiveChannelList())
            {
                if (!DataManager.Instance.TryDataLoad($"youtube/{ch.YouTubeId}", out List<YouTubeItem> list))
                    list = new();
                dic.Add(ch, list);
            }
            FoundLiveList = dic;

            if (!DataManager.Instance.TryDataLoad("youtube/FutureLiveList", out List<YouTubeItem> future))
                future = new();
            FutureLiveList = future;
        }
        public static void CreateInstance()
        {
            if (Instance != null) return;
            Instance = new YouTubeWatcher();
        }

        public async Task<bool> RegisterNotification(string id)
        {
            var data = new FormUrlEncodedContent(new Dictionary<string, string>()
                {
                    { "hub.callback", Settings.Data.NotificationCallback },
                    { "hub.topic", $"https://www.youtube.com/xml/feeds/videos.xml?channel_id={id}" },
                    { "hub.verify", "async" }, { "hub.mode", "subscribe" },
                    { "hub.verify_token", "" }, { "hub.secret", "" }, { "hub.lease_seconds", "" }
                });
            var res = await Settings.Data.HttpClient.PostAsync("https://pubsubhubbub.appspot.com/subscribe", data);
            return res.IsSuccessStatusCode;
        }

        public bool CheckNewLive(string id, out YouTubeItem item)
        {
            item = null;
            var req = Settings.Data.YouTubeService.Videos.List("contentDetails, liveStreamingDetails, snippet");
            req.Id = id;
            req.MaxResults = 1;

            var res = req.Execute();
            if (res.Items.Count == 0) return false;
            var video = res.Items[0];

            item = new(res.Items[0]);
            if (item.Channel == null || item.Livers.Count == 0 || FoundLiveList[item.Channel].FirstOrDefault(v => v.Id == id) != null)
                return false;

            var add = item.Mode != YouTubeItem.YouTubeMode.Video && video.LiveStreamingDetails.ActualStartTime == null 
                && item.LiveStartDate > DateTime.Now && video.Snippet.LiveBroadcastContent == "upcoming";
            var list = new List<YouTubeItem>(FoundLiveList[item.Channel]) { item };
            FoundLiveList = new Dictionary<Address, IReadOnlyList<YouTubeItem>>(FoundLiveList) { [item.Channel] = list };
            DataManager.Instance.DataSave($"youtube/{item.Channel.YouTubeId}", list, true);

            if (add)
            {
                FutureLiveList = new List<YouTubeItem>(FutureLiveList) { item };
                DataManager.Instance.DataSave("youtube/FutureLiveList", FutureLiveList, true);
            }
            return item.Mode == YouTubeItem.YouTubeMode.Video || add;
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
                    list.Remove(e.OldItem);
                    if (e is YouTubeChangeEvent)
                        list.Insert(i - delc, e.Item);
                    evts.Add(e);
                    delc++;
                }
            }
            if(delc != 0)
            {
                FutureLiveList = new List<YouTubeItem>(list.Distinct());
                DataManager.Instance.DataSave("youtube/FutureLiveList", list, true);
            }
            return evts;

            static async Task<YouTubeEvent> CheckFutureLive(YouTubeItem item)
            {
                if (item.Mode == YouTubeItem.YouTubeMode.Video) return null;
                var req = Settings.Data.YouTubeService.Videos.List("contentDetails, liveStreamingDetails, snippet");
                req.Id = item.Id;
                req.MaxResults = 1;

                var res = await req.ExecuteAsync();
                if (res.Items.Count == 0)
                    return new YouTubeDeleteLiveEvent(item);

                var video = res.Items[0];
                if (video.Snippet.LiveBroadcastContent == "live")
                    return new YouTubeStartLiveEvent(item);
                else if (video.LiveStreamingDetails?.ActualStartTime != null && video.Snippet.LiveBroadcastContent == "none")
                    return new YouTubeAlradyLivedEvent(item);
                else if (!item.Equals(video) || item.UpdatedDate < DateTime.Now.AddDays(-7))
                    return YouTubeChangeEvent.CreateEvent(new(video), item);
                else return null;
            }
        }
    }

    [Serializable]
    [JsonConverter(typeof(YouTubeItemConverter))]
    public class YouTubeItem : IEquatable<YouTubeItem>, INotificationContent
    {
        public enum YouTubeMode { Video, Premire, Live, Archive }

        public YouTubeMode Mode { get; }
        public string Id { get; }
        public string Title { get; }
        public TextContent Description { get; }
        public bool IsOfficialChannel { get; }
        public Address Channel { get; }
        public string ChannelName { get; }
        public DateTime PublishedDate { get; }
        public DateTime UpdatedDate { get; }
        public string LiveChatId { get; }
        public DateTime LiveStartDate { get; }
        public bool IsEndedLive { get; }
        public bool IsCollaboration { get; }
        public IReadOnlyList<LiverDetail> Livers { get; }

        [JsonIgnore]
        public IReadOnlyDictionary<string, string> ContentFormat => new Dictionary<string, string>()
            {
                { "Date", (this as INotificationContent).ConvertDuringDateTime(LiveStartDate) },
                { "Title", Title }, { "VideoId", Id }, { "ChannelId", Channel.YouTubeId },
                { "ChannelName", ChannelName }, { "URL", $"https://www.youtube.com/watch/{Id}" }
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

        public YouTubeItem(Video video, DateTime? update = null)
        {
            Id = video.Id;
            Title = video.Snippet.Title;
            Description = new(video.Snippet.Description);
            ChannelName = video.Snippet.ChannelTitle;
            PublishedDate = video.Snippet.PublishedAt != null ? (DateTime)video.Snippet.PublishedAt : DateTime.Now;
            UpdatedDate = update ?? DateTime.Now;

            if (video.LiveStreamingDetails != null)
            {
                IsEndedLive = video.LiveStreamingDetails.ActualEndTime != null;
                if (IsEndedLive)
                {
                    Mode = YouTubeMode.Archive;
                    LiveChatId = null;
                }
                else
                {
                    Mode = video.ContentDetails?.Duration != "P0D" ? YouTubeMode.Premire : YouTubeMode.Live;
                    LiveChatId = video.LiveStreamingDetails.ActiveLiveChatId;
                }
                
                LiveStartDate = video.LiveStreamingDetails.ScheduledStartTime ?? PublishedDate;
            }
            else
            {
                Mode = YouTubeMode.Video;
                LiveChatId = null;
                LiveStartDate = PublishedDate;
            }

            var chid = video.Snippet.ChannelId;
            Address channel = LiverData.GetLiverFromYouTubeId(chid);
            List<LiverDetail> livers = new(LiverData.GetAllLiversList()),
                 res = channel == null ? new() : new() { (LiverDetail)channel };
            foreach (var liver in livers)
            {
                if (liver == channel) continue;

                if (Description.Urls.FirstOrDefault(u => u.Liver == liver) != null ||
                    Description.Content.Contains('@' + liver.ChannelName))
                    res.Add(liver);
            }
            Livers = res;

            var group = LiverGroup.GroupList.FirstOrDefault(g => g.YouTubeId == chid);
            if (group != null)
            {
                Channel = group;
                IsOfficialChannel = true;
                IsCollaboration = true;
            }
            else
            {
                Channel = channel ?? LiveChannel.GetLiveChannelList().FirstOrDefault(c => c.YouTubeId == chid);
                IsOfficialChannel = false;
                IsCollaboration = Livers.Count == 1;
            }
        }
        private YouTubeItem(YouTubeMode mode, string id, string title, TextContent description, bool official, Address ch,
            string ch_name, DateTime publish, DateTime update, string chat_id, DateTime start_date, List<LiverDetail> livers)
        {
            Mode = mode;
            Id = id;
            Title = title;
            Description = description;
            IsOfficialChannel = official;
            Channel = ch;
            ChannelName = ch_name;
            PublishedDate = publish;
            UpdatedDate = update;
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
            return Id == other.Id && Title == other.Title && Description == other.Description && LiveStartDate == other.LiveStartDate;
        }
        public bool Equals(Video other)
        {
            return Id == other.Id && Title == other.Snippet?.Title && Description.Content == other.Snippet?.Description &&
                ((other.LiveStreamingDetails == null && LiveStartDate == other.Snippet?.PublishedAt) ||
                (other.LiveStreamingDetails != null && LiveStartDate == other.LiveStreamingDetails.ScheduledStartTime));
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Title, Description, LiveStartDate);
        }

        public class YouTubeItemConverter : JsonConverter<YouTubeItem>
        {
            public override YouTubeItem Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                reader.CheckStartToken();

                var mode = (YouTubeMode)reader.GetNextValue<int>(options);
                var id = reader.GetNextValue<string>(options);
                var title = reader.GetNextValue<string>(options);
                var description = reader.GetNextValue<TextContent>(options);
                var official = reader.GetNextValue<bool>(options);
                Address channel;
                if (official)
                {
                    var gid = reader.GetNextValue<Address>(options).Id;
                    channel = LiverGroup.GroupList.FirstOrDefault(g => g.Id == gid);
                }
                else channel = reader.GetNextValue<LiverDetail>(options);
                var ch_name = reader.GetNextValue<string>(options);
                var publish = reader.GetNextValue<DateTime>(options);
                var chat_id = reader.GetNextValue<string>(options);
                var livestart = reader.GetNextValue<DateTime>(options);
                var livers = reader.GetNextValue<List<LiverDetail>>(options);

                reader.CheckEndToken();
                return new(mode, id, title, description, official, channel, ch_name, publish, DateTime.Now,
                    chat_id, livestart, livers);
            }

            public override void Write(Utf8JsonWriter writer, YouTubeItem value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                writer.WriteNumber("Mode", (int)value.Mode);
                writer.WriteString("VideoId", value.Id);
                writer.WriteString("VideoTitle", value.Title);
                writer.WriteValue("VideoDescription", value.Description, options);

                writer.WriteBoolean("IsOfficialChannel", value.IsOfficialChannel);
                writer.WritePropertyName("VideoChannel");
                if (value.IsOfficialChannel) JsonSerializer.Serialize(writer, value.Channel, options);
                else JsonSerializer.Serialize(writer, (LiverDetail)value.Channel, options);
                writer.WriteString("VideoChannelName", value.ChannelName);

                writer.WriteString("PublishedDate", value.PublishedDate.ToString("G"));
                writer.WriteString("LiveChatId", value.LiveChatId);
                writer.WriteString("LiveStartDate", value.LiveStartDate.ToString("G"));
                writer.WriteValue("Livers", value.Livers, options);

                writer.WriteEndObject();
            }
        }
    }
}
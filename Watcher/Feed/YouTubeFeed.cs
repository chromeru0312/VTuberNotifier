using Discord;
using Google.Apis.YouTube.v3.Data;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public YouTubeEvent CheckNewLive(string id, out YouTubeItem item)
        {
            item = null;
            var req = SettingData.YouTubeService.Videos.List("contentDetails, liveStreamingDetails, snippet");
            req.Id = id;
            req.MaxResults = 1;

            var res = req.Execute();
            if (res.Items.Count == 0)
                return new YouTubeDeleteLiveEvent(FutureLiveList.FirstOrDefault(v => v.Id == id));
            var video = res.Items[0];
            var liver = true;
            Address ch = LiverData.GetLiverFromYouTubeId(video.Snippet.ChannelId);
            if (ch == null) ch = LiverGroup.GroupList.FirstOrDefault(g => g.YouTubeId == id);
            if (ch == null)
            {
                ch = LiveChannel.GetLiveChannelList().FirstOrDefault(c => c.YouTubeId == id);
                liver = false;
            }
            if (ch == null) return null;

            item = new(video);
            var old = FoundLiveList[ch].FirstOrDefault(v => v.Id == id);
            var list = new List<YouTubeItem>(FoundLiveList[ch]) { item };
            YouTubeEvent evt = null;
            bool add = false;
            if (old != null)
            {
                list.Remove(old);
                var future = new List<YouTubeItem>(FutureLiveList);
                if (future.Contains(old))
                {
                    future.Remove(old);
                    FutureLiveList = future;
                    if (!item.Equals(old))
                    {
                        if (item.Livers.Count != 0)
                        {
                            evt = new YouTubeChangeInfoEvent(old, item);
                            add = true;
                        }
                        else
                        {
                            evt = new YouTubeDeleteLiveEvent(item);
                            add = false;
                        }
                    }
                    else add = true;
                }
                else if (!liver && item.Livers.Count != old.Livers.Count && item.Livers.Count != 0)
                {
                    if (item.Mode == YouTubeItem.YouTubeMode.Live)
                    {
                        evt = new YouTubeNewLiveEvent(item);
                        add = true;
                    }
                    else if (item.Mode == YouTubeItem.YouTubeMode.Premire)
                    {
                        evt = new YouTubeNewPremireEvent(item);
                        add = true;
                    }
                    else evt = new YouTubeNewVideoEvent(item);
                }
            }
            else
            {
                if (item.Livers.Count == 0)
                {
                    add = false;
                    evt = null;
                }
                else
                {
                    if (item.Mode == YouTubeItem.YouTubeMode.Video)
                    {
                        add = false;
                        evt = new YouTubeNewVideoEvent(item);
                    }
                    else
                    {
                        add = video.LiveStreamingDetails.ActualStartTime == null && item.LiveStartDate > DateTime.Now &&
                            video.Snippet.LiveBroadcastContent == "upcoming";
                        if (add)
                        {
                            if (item.Mode == YouTubeItem.YouTubeMode.Live)
                                evt = new YouTubeNewLiveEvent(item);
                            else if (item.Mode == YouTubeItem.YouTubeMode.Premire)
                                evt = new YouTubeNewPremireEvent(item);
                        }
                    }
                }
            }
            FoundLiveList = new Dictionary<Address, IReadOnlyList<YouTubeItem>>(FoundLiveList) { [ch] = list };
            DataManager.Instance.DataSave($"youtube/{ch.YouTubeId}", list, true);

            if (add)
            {
                FutureLiveList = new List<YouTubeItem>(FutureLiveList) { item };
                DataManager.Instance.DataSave("youtube/FutureLiveList", FutureLiveList, true);
            }
            return evt;
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
                        if (e is not YouTubeAlradyLivedEvent) evts.Add(e);
                    }
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
                if (item.Mode == YouTubeItem.YouTubeMode.Video) return new YouTubeAlradyLivedEvent(item);
                var req = SettingData.YouTubeService.Videos.List("contentDetails, liveStreamingDetails, snippet");
                req.Id = item.Id;
                req.MaxResults = 1;

                var res = await req.ExecuteAsync();
                if (res.Items.Count == 0) return new YouTubeDeleteLiveEvent(item);
                var video = res.Items[0];
                if (video.Snippet.LiveBroadcastContent == "live")
                    return new YouTubeStartLiveEvent(item);
                else if (video.LiveStreamingDetails?.ActualStartTime != null && video.Snippet.LiveBroadcastContent == "none")
                    return new YouTubeAlradyLivedEvent(item);
                else if (!item.Equals(video) || item.UpdatedDate < DateTime.Now.AddDays(-7))
                    return new YouTubeChangeInfoEvent(item, new(video));
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
        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public bool IsOfficialChannel { get; }
        public Address Channel { get; }
        public string ChannelName { get; }
        public DateTime PublishedDate { get; }
        public DateTime UpdatedDate { get; }
        public string LiveChatId { get; }
        public DateTime LiveStartDate { get; }
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
            Description = video.Snippet.Description;
            ChannelName = video.Snippet.ChannelTitle;
            PublishedDate = video.Snippet.PublishedAt != null ? (DateTime)video.Snippet.PublishedAt : DateTime.Now;
            UpdatedDate = update ?? DateTime.Now;

            if (video.LiveStreamingDetails != null)
            {
                if (video.ContentDetails?.Duration != "P0D") Mode = YouTubeMode.Premire;
                else Mode = YouTubeMode.Live;
                LiveChatId = video.LiveStreamingDetails.ActiveLiveChatId;
                LiveStartDate = video.LiveStreamingDetails.ScheduledStartTime ?? PublishedDate;
            }
            else Mode = YouTubeMode.Video;

            var chid = video.Snippet.ChannelId;
            Address channel = LiverData.GetLiverFromYouTubeId(chid);
            List<LiverDetail> livers = new(LiverData.GetAllLiversList()),
                 res = channel == null ? new() : new() { (LiverDetail)channel };
            foreach (var liver in livers)
            {
                if (liver == channel) continue;

                if (Description.Contains(liver.YouTubeId) || Description.Contains('@' + liver.ChannelName) ||
                    Title.Contains(liver.Name))
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
                Channel = channel ?? LiveChannel.GetLiveChannelList().FirstOrDefault(c => c.YouTubeId == chid)
                        ?? throw new NullReferenceException();
                IsOfficialChannel = false;
                IsCollaboration = Livers.Count == 1;
            }
        }
        private YouTubeItem(YouTubeMode mode, string id, string title, string description,
            bool official, Address ch, string ch_name, DateTime publish, DateTime update,
            string chat_id, DateTime start_date, List<LiverDetail> livers)
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
            return Id == other.Id && Livers == other.Livers && Title == other.Title &&
                Description == other.Description && LiveStartDate == other.LiveStartDate;
        }
        public bool Equals(Video other)
        {
            return other.Snippet != null && Id == other.Id && Title == other.Snippet.Title &&
                Description == other.Snippet.Description && other.LiveStreamingDetails != null &&
                LiveStartDate == other.LiveStreamingDetails.ScheduledStartTime;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Title, Description, LiveStartDate);
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
                    return new(mode, id, title, description, official, channel, ch_name,
                        publish, DateTime.Now, chat_id, livestart, livers);
                throw new JsonException();
            }

            public override void Write(Utf8JsonWriter writer, YouTubeItem value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                writer.WriteNumber("Mode", (int)value.Mode);
                writer.WriteString("VideoId", value.Id);
                writer.WriteString("VideoTitle", value.Title);
                writer.WriteString("VideoDescription", value.Description);

                writer.WriteBoolean("IsOfficialChannel", value.IsOfficialChannel);
                writer.WritePropertyName("VideoChannel");
                if (value.IsOfficialChannel) JsonSerializer.Serialize(writer, value.Channel, options);
                else JsonSerializer.Serialize(writer, (LiverDetail)value.Channel, options);
                writer.WriteString("VideoChannelName", value.ChannelName);

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

using Google.Apis.YouTube.v3.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;
using VTuberNotifier.Discord;
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
                if (!DataManager.Instance.TryDataLoad($"youtube/{liver.YouTubeId}", out List<YouTubeItem> list))
                    list = new();
                dic.Add(liver, list);
            }
            foreach (var group in LiverGroup.GroupList)
            {
                if (group.YouTubeId == null) continue;
                if (!DataManager.Instance.TryDataLoad($"youtube/{group.YouTubeId}", out List<YouTubeItem> list))
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

        public async Task<List<YouTubeItem>> ReadFeed(Address address)
        {
            var list = new List<YouTubeItem>();
            if (address.YouTubeId == null) return list;
            if (!FoundLiveList.ContainsKey(address))
                FoundLiveList = new Dictionary<Address, IReadOnlyList<YouTubeItem>>(FoundLiveList) { { address, new List<YouTubeItem>() } };
            var doc = XDocument.Load($"https://www.youtube.com/feeds/videos.xml?channel_id={address.YouTubeId}");
            XNamespace ns = doc.Root.Attribute("xmlns").Value;
            var lives = new List<XElement>(doc.Root.Elements(ns + "entry"));
            for (int i = 0; i < lives.Count; i++)
            {
                if (i == 3) break;
                var live = lives[i];
                var id = live.Element(ns + "id").Value.Split(':')[^1].Trim();
                if (FoundLiveList[address].FirstOrDefault(v => v.VideoId == id) != null) break;

                var req = SettingData.YouTubeService.Videos.List("contentDetails, liveStreamingDetails, snippet");
                req.Id = id;
                req.MaxResults = 1;

                var item = new YouTubeItem((await req.ExecuteAsync()).Items[0]);
                if (item.Mode != YouTubeItem.YouTubeMode.Video && item.LiveStartDate > DateTime.Now)
                {
                    FutureLiveList = new List<YouTubeItem>(FutureLiveList) { item };
                    await DataManager.Instance.DataSaveAsync("youtube/FutureLiveList", FutureLiveList, true);
                }
                list.Add(item);
            }
            if (list.Count > 0)
            {
                FoundLiveList = new Dictionary<Address, IReadOnlyList<YouTubeItem>>(FoundLiveList)
                { [address] = new List<YouTubeItem>(FoundLiveList[address].Concat(list)) };
                await DataManager.Instance.DataSaveAsync($"youtube/{address.YouTubeId}", FoundLiveList[address], true);
            }
            return list;
        }

        public bool CheckNewLive(string id, out YouTubeItem item)
        {
            item = null;
            var req = SettingData.YouTubeService.Videos.List("contentDetails, liveStreamingDetails, snippet");
            req.Id = id;
            req.MaxResults = 1;

            var res = req.Execute();
            if (res.Items.Count == 0) return false;
            var video = res.Items[0];
            var liver = LiverData.GetLiverFromYouTubeId(video.Snippet.ChannelId);
            if (liver == null || (FoundLiveList.ContainsKey(liver) && FoundLiveList[liver].Contains(new(video)))) return false;
            item = new(video, true);
            return true;
        }

        public void CheckLiveChanged()
        {
            var list = new List<YouTubeItem>(FutureLiveList);
            var tasks = new List<Task<YouTubeEvent>>();
            foreach (var y in list) tasks.Add(CheckFutureLive(y));
            for(int i = 0;i < tasks.Count; i++)
            {
                var e = tasks[i].Result;
                if (e != null)
                {
                    list.RemoveAt(i);
                }
            }
            FutureLiveList = list;

            static async Task<YouTubeEvent> CheckFutureLive(YouTubeItem item)
            {
                var req = SettingData.YouTubeService.Videos.List("liveStreamingDetails, snippet");
                req.Id = item.VideoId;
                req.MaxResults = 1;

                var res = await req.ExecuteAsync();
                if (res.Items.Count == 0) return new YouTubeDeleteLiveEvent(item);
                var video = res.Items[0];
                if (video.Snippet.LiveBroadcastContent == "") return new YouTubeStartLiveEvent(item);
                else if (!item.Equals(video)) return new YouTubeChangeInfoEvent(item, new(video));
                else return null;
            }
        }
    }

    [Serializable]
    [JsonConverter(typeof(YouTubeItemConverter))]
    public class YouTubeItem : IEquatable<YouTubeItem>, IDiscordContent
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
        public bool IsTwitterSource { get; }

        [JsonIgnore]
        public IReadOnlyDictionary<string, string> ContentFormat => new Dictionary<string, string>()
            {
                { "Date", (this as IDiscordContent).ConvertDuringDateTime(LiveStartDate) },
                { "Title", VideoTitle }, { "VideoId", VideoId }, { "ChannelId", VideoChannel.YouTubeId },
                { "ChannelName", VideoChannelName }, { "URL", $"https://www.youtube.com/watch/{VideoId}" },
                { "Source", IsTwitterSource ? "Twitter" : "YouTube" }
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

        public YouTubeItem(Video video, bool twitter = false)
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
            Livers = DetectLiver(VideoDescription,
                LiverData.GetAllLiversList().FirstOrDefault(l => l.YouTubeId == chid));
            var group = LiverGroup.GroupList.FirstOrDefault(g => g.YouTubeId == chid);
            if (group != null)
            {
                VideoChannel = group;
                IsOfficialChannel = true;
                IsCollaboration = true;
            }
            else
            {
                var liver = LiverData.GetAllLiversList().FirstOrDefault(l => l.YouTubeId == chid);
                VideoChannel = liver ?? throw new NullReferenceException();
                IsOfficialChannel = false;
                IsCollaboration = Livers.Count == 1;
            }

            IsTwitterSource = twitter;
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

        private static List<LiverDetail> DetectLiver(string content, LiverDetail channel)
        {
            List<LiverDetail> livers = new(LiverData.GetAllLiversList()),
                res = channel == null ? new() : new() { channel };
            foreach (var liver in livers)
            {
                if (liver == channel) continue;

                if (content.Contains(liver.YouTubeId)) res.Add(liver);
                else if (content.Contains('@'+ liver.ChannelName)) res.Add(liver);
                //else if (content.Contains(liver.Name)) res.Add(liver);
            }
            return res;
        }

        public override bool Equals(object obj)
        {
            return (obj is YouTubeItem item && Equals(item)) || (obj is Video video && Equals(video));
        }
        public bool Equals(YouTubeItem other)
        {
            return VideoId == other.VideoId && VideoTitle == other.VideoTitle &&
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
                if (official) channel = JsonSerializer.Deserialize<LiverGroupDetail>(ref reader, options);
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
                if (value.IsOfficialChannel) JsonSerializer.Serialize(writer, (LiverGroupDetail)value.VideoChannel, options);
                else JsonSerializer.Serialize(writer, (LiverDetail)value.VideoChannel, options);
                writer.WriteString("VideoChannelName", value.VideoChannelName);

                writer.WriteString("PublishedDate", value.PublishedDate.ToString("g"));
                writer.WriteString("LiveChatId", value.LiveChatId);
                writer.WriteString("LiveStartDate", value.LiveStartDate.ToString("g"));
                writer.WritePropertyName("Livers");
                JsonSerializer.Serialize(writer, value.Livers, options);

                writer.WriteEndObject();
            }
        }
    }
}

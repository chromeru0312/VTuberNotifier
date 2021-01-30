using Google.Apis.YouTube.v3.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;
using VTuberNotifier.Discord;
using VTuberNotifier.Liver;

namespace VTuberNotifier.Watcher.Feed
{
    public class YouTubeFeed
    {
        public static YouTubeFeed Instance { get; private set; }
        public IReadOnlyDictionary<Address, IReadOnlyList<YouTubeItem>> FoundLiveList { get; private set; }

        private YouTubeFeed()
        {
            var dic = new Dictionary<Address, IReadOnlyList<YouTubeItem>>();
            foreach (var liver in LiverData.GetAllLiversList())
            {
                if (DataManager.Instance.TryDataLoad($"youtube/{liver.YouTubeId}", out List<YouTubeItemJson> list))
                    dic.Add(liver, new List<YouTubeItem>(list.Select(j => new YouTubeItem(j))));
                else dic.Add(liver, new List<YouTubeItem>());
            }
            foreach (var group in LiverGroup.GroupList)
            {
                if (group.YouTubeId == null) continue;
                if (DataManager.Instance.TryDataLoad($"youtube/{group.YouTubeId}", out List<YouTubeItemJson> list))
                    dic.Add(group, new List<YouTubeItem>(list.Select(j => new YouTubeItem(j))));
                else dic.Add(group, new List<YouTubeItem>());
            }
            FoundLiveList = dic;
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
            for (int i = 0;i < lives.Count;i++)
            {
                if (i == 3) break;
                var live = lives[i];
                var id = live.Element(ns + "id").Value.Split(':')[^1].Trim();
                if (FoundLiveList[address].FirstOrDefault(v => v.VideoId == id) != null) break;

                var req = SettingData.YouTubeService.Videos.List("contentDetails, liveStreamingDetails, snippet");
                req.Id = id;
                req.MaxResults = 1;

                list.Add(new((await req.ExecuteAsync()).Items[0]));
            }
            if(list.Count > 0)
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
    }

    [Serializable]
    public class YouTubeItem : IEquatable<YouTubeItem>, IDiscordContent
    {
        public enum YouTubeMode { Video, Premire, Live }

        public YouTubeMode Mode { get; }
        public string VideoId { get; }
        public string VideoTitle { get; }
        public string VideoDescription { get; }
        public string VideoChannelId { get; }
        public string VideoChannelName { get; }
        public DateTime PublishedDate { get; }
        public string LiveChatId { get; }
        public DateTime LiveStartDate { get; }
        public IReadOnlyList<LiverDetail> Livers { get; }
        public bool IsTwitterSource { get; }

        [JsonIgnore]
        public IReadOnlyDictionary<string, string> ContentFormat => new Dictionary<string, string>()
            {
                { "Date", (this as IDiscordContent).ConvertDuringDateTime(LiveStartDate) },
                { "Title", VideoTitle }, { "VideoId", VideoId }, { "ChannelId", VideoChannelId },
                { "ChannelName", VideoChannelName }, { "URL", $"https://www.youtube.com/watch/{VideoId}" },
                { "Source", IsTwitterSource ? "Twitter" : "YouTube" }
            };
        [JsonIgnore]
        public IReadOnlyDictionary<string, IEnumerable<object>> ContentFormatEnumerator
            => new Dictionary<string, IEnumerable<object>>()
            {
                { "Livers", Livers },
            };

        public YouTubeItem(Video video, bool twitter = false)
        {
            VideoId = video.Id;
            VideoTitle = video.Snippet.Title;
            VideoDescription = video.Snippet.Description;
            VideoChannelId = video.Snippet.ChannelId;
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
            Livers = DetectLiver(VideoDescription,
                LiverData.GetAllLiversList().FirstOrDefault(l => VideoChannelId == l.YouTubeId));
            IsTwitterSource = twitter;
        }
        public YouTubeItem(YouTubeItemJson json)
        {
            Mode = json.Mode;
            VideoId = json.VideoId;
            VideoTitle = json.VideoTitle;
            VideoDescription = json.VideoDescription;
            VideoChannelId = json.VideoChannelId;
            VideoChannelName = json.VideoChannelName;
            PublishedDate = json.PublishedDate;
            LiveChatId = json.LiveChatId;
            LiveStartDate = json.LiveStartDate;
            Livers = json.Livers;
            IsTwitterSource = json.IsTwitterSource;
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

        public string GetDiscordContent()
        {
            var format = "配信待機所が作成されました\n{Title}\n参加ライバー:{Livers: / }\n{Date}\n{URL}";
            if (IsTwitterSource) format += "\n※この通知はTwitterからの情報のため、信憑性が薄い場合があります。";
            return (this as IDiscordContent).ConvertContent(format);
        }

        public override bool Equals(object obj)
        {
            return obj is YouTubeItem item && Equals(item);
        }
        public bool Equals(YouTubeItem other)
        {
            return VideoId == other.VideoId && VideoTitle == other.VideoTitle &&
                VideoDescription == other.VideoDescription && LiveStartDate == other.LiveStartDate;
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
    }

    public class YouTubeItemJson
    {
        public YouTubeItem.YouTubeMode Mode { get; set; }
        public string VideoId { get; set; }
        public string VideoTitle { get; set; }
        public string VideoDescription { get; set; }
        public string VideoChannelId { get; set; }
        public string VideoChannelName { get; set; }
        public DateTime PublishedDate { get; set; }
        public string LiveChatId { get; set; }
        public DateTime LiveStartDate { get; set; }
        public IReadOnlyList<LiverDetail> Livers { get; set; }
        public bool IsTwitterSource { get; set; }
    }
}

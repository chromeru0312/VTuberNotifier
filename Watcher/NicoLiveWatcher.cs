using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web;
using VTuberNotifier.Liver;
using VTuberNotifier.Notification;
using VTuberNotifier.Watcher.Event;

namespace VTuberNotifier.Watcher
{
    public class NicoLiveWatcher
    {
        public static NicoLiveWatcher Instance { get; private set; }
        private Dictionary<LiverDetail, List<NicoLiveItem>> FoundLiveList { get; set; }
        private List<NicoLiveItem> FutureLiveList { get; set; }
        private DateTime NextSearch { get; set; }

        private NicoLiveWatcher()
        {
            var dic = new Dictionary<LiverDetail, List<NicoLiveItem>>();
            var list = LiverData.GetAllLiversList();
            foreach (var address in list)
            {
                var id = address.Id;
                if (!DataManager.Instance.TryDataLoad($"nicolive/{id}", out List<NicoLiveItem> load))
                    load = new();
                dic.Add(address, load);
            }
            FoundLiveList = dic;

            if (!DataManager.Instance.TryDataLoad("nicolive/FutureLiveList", out List<NicoLiveItem> future))
                future = new();
            FutureLiveList = future;
            NextSearch = DateTime.MinValue;
        }
        public static void CreateInstance()
        {
            if (Instance == null) Instance = new();
        }

        public async Task<List<NicoLiveItem>> SearchList(LiverDetail liver)
        {
            Stopwatch sw = new(), web_sw = new();
            sw.Start();
            List<NicoLiveItem> list = new(), found = FoundLiveList[liver];
            var surl = $"https://live.nicovideo.jp/search?keyword={HttpUtility.UrlEncode(liver.NicoTag)}&page=1" +
                "&status=reserved&sortOrder=recentDesc&providerTypes=official&providerTypes=channel";

            var now = DateTime.Now;
            if (NextSearch > now)
            {
                await Task.Delay((int)(NextSearch - now).TotalMilliseconds);
            }
            web_sw.Start();
            var html = await Settings.Data.HttpClient.GetStringAsync(surl);
            web_sw.Stop();
            NextSearch = now.AddMilliseconds(web_sw.ElapsedMilliseconds);
            web_sw.Reset();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var node = doc.DocumentNode.SelectSingleNode("//html/body/main/div");

            var l_str = "./div[@class='searchPage-Layout']/main/div[1]/ul/li";
            var lives = new List<HtmlNode>(node.SelectNodes(l_str));
            foreach (var live in lives)
            {
                var id = live.SelectSingleNode("./div/h1").SelectSingleNode("./a").Attributes["href"].Value.Trim().Split('/')[^1];
                var item = await GetItemDetail(id);
                if (item == null) continue;
                else if (!found.Contains(item))
                {
                    if (found.FirstOrDefault(l => l.Id == id) != null) found.Remove(item);
                    else FutureLiveList.Add(item);
                    list.Add(item);
                    found.Add(item);
                }
            }
            FoundLiveList = new(FoundLiveList) { [liver] = found };
            sw.Stop();
            return list;
        }

        public async Task CheckOnair(string id)
        {
            var item = await GetItemDetail(id);
            if (item.Status == NicoLiveItem.LiveStatus.ON_AIR)
            {
                await EventNotifier.Instance.Notify(new NicoStartLiveEvent(item));
                FutureLiveList.RemoveAll(l => id == l.Id);
            }
            else if (item.Status == NicoLiveItem.LiveStatus.ENDED)
            {
                FutureLiveList.RemoveAll(l => id == l.Id);
            }
            else
            {
                TimerManager.Instance.AddAlarm(DateTime.Now.AddSeconds(50), () => CheckOnair(id));
            }
        }

        private static async Task<NicoLiveItem> GetItemDetail(string id)
        {
            var purl = "https://api.live2.nicovideo.jp/api/v1/watch/programs" +
                       $"?nicoliveProgramId={id}&fields=features,socialGroup,program,taxonomy";
            var req = new HttpRequestMessage(HttpMethod.Get, purl);
            req.Headers.UserAgent.Add(Settings.Data.UserAgent);
            req.Headers.Authorization = new($"Bearer {Settings.Data.NicoLiveToken}");
            var res = await Settings.Data.HttpClient.SendAsync(req);
            if (!res.IsSuccessStatusCode) return null;

            var json = await res.Content.ReadAsStringAsync();
            var live_json = JsonSerializer.Deserialize<NicoLiveJson>(json);
            if (live_json.Data.Program.Provider == "COMMUNITY") return null;
            return new(id, live_json.Data);
        }
    }

    public class NicoLiveItem : INotificationContent, IEquatable<NicoLiveItem>
    {
        public enum LiveStatus
        {
            BEFORE_RELEASE, RELEASED, ON_AIR, ENDED
        }

        public string Id { get; }
        public string Title { get; }
        public TextContent Description { get; }
        public bool IsOfficialChannel { get; }
        public bool IsPaidLive { get; }
        public string ChannelId { get; }
        public string ChannelName { get; }
        public NicoTag MainCategory { get; }
        public IReadOnlyList<NicoTag> SubCategory { get; }
        public IReadOnlyList<NicoTag> Tags { get; }
        public LiveStatus Status { get; }
        public DateTime LiveStartDate { get; }
        public DateTime LiveScheduledEndDate { get; }
        public IReadOnlyList<LiverDetail> Livers { get; }

        [JsonIgnore]
        public IReadOnlyDictionary<string, string> ContentFormat => new Dictionary<string, string>()
            {
                { "Date", (this as INotificationContent).ConvertDuringDateTime(LiveStartDate) },
                { "Title", Title }, { "LiveId", Id }, { "ChannelId", ChannelId },
                { "ChannelName", ChannelName }, { "URL", $"https://live.nicovideo.jp/watch/{Id}" }
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

        public NicoLiveItem(string id, NicoLiveJson.JsonData json)
        {
            Id = id;
            Title = json.Program.Title;
            Description = new(json.Program.Description);
            IsPaidLive = json.Features.Enabled.Contains("PAY_PROGRAM");
            IsOfficialChannel = json.Program.Provider == "OFFICIAL";
            ChannelId = json.SocialGroup.SocialGroupId;
            ChannelName = json.SocialGroup.Name;
            MainCategory = json.Taxonomy.Categories.Main[0];
            SubCategory = new List<NicoTag>(json.Taxonomy.Categories.Sub?.Select(t => (NicoTag)t) ?? Array.Empty<NicoTag>());
            Tags = new List<NicoTag>(json.Taxonomy.Tags.Items.Select(t => (NicoTag)t));
            Status = (LiveStatus)Enum.Parse(typeof(LiveStatus), json.Program.Schedule.Status);
            LiveStartDate = json.Program.Schedule.OpenTime;
            LiveScheduledEndDate = json.Program.Schedule.ScheduledEndTime;

            List<LiverDetail> livers = new();
            var all = LiverData.GetAllLiversList();
            foreach (var tag in Tags)
            {
                var liver = all.FirstOrDefault(l => l.NicoTag == tag);
                if (liver != null) livers.Add(liver);
            }
            Livers = livers;
        }

        public override bool Equals(object obj)
        {
            return obj is NicoLiveItem item && Equals(item);
        }
        public bool Equals(NicoLiveItem other)
        {
            return Id == other.Id && ChannelId == other.ChannelId && Tags.SequenceEqual(other.Tags) &&
                MainCategory == other.MainCategory && LiveStartDate == other.LiveStartDate &&
                LiveScheduledEndDate == other.LiveScheduledEndDate;
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(Id, ChannelId, Tags, MainCategory, LiveStartDate, LiveScheduledEndDate);
        }
    }

    public struct NicoTag
    {
        public string Name { get; }
        public bool IsExistsNicopedia { get; }

        public NicoTag(string name, bool npedia)
        {
            Name = name;
            IsExistsNicopedia = npedia;
        }
        public NicoTag(NicoLiveJson.Tag tag)
            : this(tag.Text, string.IsNullOrEmpty(tag.NicopediaArticleUrl)) { }

        public static implicit operator NicoTag(NicoLiveJson.Tag tag) => new(tag);
        public static implicit operator string(NicoTag tag) => tag.Name;
    }

    public class NicoLiveJson
    {
        public MetaData Meta { get; set; }
        public JsonData Data { get; set; }

        public class MetaData
        {
            public int Status { get; set; }
            public string ErrorCode { get; set; }
        }

        public class JsonData
        {
            public FeaturesData Features { get; set; }
            public ProgramData Program { get; set; }
            public Socialgroup SocialGroup { get; set; }
            public Taxonomy Taxonomy { get; set; }
        }

        public class FeaturesData
        {
            public string[] Enabled { get; set; }
        }

        public class ProgramData
        {
            public string Title { get; set; }
            public string Description { get; set; }
            public string Provider { get; set; }
            public Schedule Schedule { get; set; }
        }

        public class Schedule
        {
            public string Status { get; set; }
            public DateTime OpenTime { get; set; }
            public DateTime BeginTime { get; set; }
            public DateTime ScheduledEndTime { get; set; }
        }

        public class Socialgroup
        {
            public string SocialGroupId { get; set; }
            public string Type { get; set; }
            public string Name { get; set; }
        }

        public class Taxonomy
        {
            public Categories Categories { get; set; }
            public Tags Tags { get; set; }
        }

        public class Categories
        {
            public Tag[] Main { get; set; }
            public Tag[] Sub { get; set; }
        }

        public class Tag
        {
            public string Text { get; set; }
            public string NicopediaArticleUrl { get; set; }
        }

        public class Tags
        {
            public Tag[] Items { get; set; }
        }
    }
}
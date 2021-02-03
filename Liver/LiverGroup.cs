using Discord;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using VTuberNotifier.Watcher.Store;
using static VTuberNotifier.Liver.ProducedCompany;

namespace VTuberNotifier.Liver
{
    public static class LiverGroup
    {
        public static LiverGroupDetail Nijiasnji { get; }
            = new(10000, "nijisanji", "にじさんじ", Ichikara,
                new("https://nijisanji.ichikara.co.jp/", "https://nijisanji.ichikara.co.jp/member/"),
                NijisanjiMembers, "UCX7YkU9nEeaoZbkVLVajcMg", "nijisanji_app",
                new("https://wikiwiki.jp/nijisanji/"), true,
                new("https://shop.nijisanji.jp/", NijisanjiWatcher.Instance.GetNewProduct));
        public static LiverGroupDetail Hololive { get; }
            = new(20000, "hololive", "ホロライブ", Cover,
                new("https://www.hololive.tv", "https://www.hololive.tv/member"),
                HololiveMembers, "UCJFZiqLMntJufDCHc6bQixg", "hololivetv",
                new("https://seesaawiki.jp/hololivetv/", "d/", true), true);
        public static LiverGroupDetail DotLive { get; }
            = new(30000, "dotlive", ".Live", AppLand,
                new("https://dotlive.jp/", "https://dotlive.jp/member/"),
                DotliveMembers, "UCAZ_LA7f0sjuZ1Ni8L2uITw", "dotLIVEyoutuber",
                new("https://seesaawiki.jp/siroyoutuber/", "d/", true), true,
                new("https://4693.live/", null));
        public static LiverGroupDetail VLive { get; }
            = new(40000, "vlive", "VLive", BitStar,
                new("http://vlive.love/", "http://vlive.love/"), VliveMembers, null, "vlive_japan",
                new("https://wikiwiki.jp/vlive/"), true);
        public static LiverGroupDetail V774inc { get; }
            = new(50000, "774inc", "774inc", null,
                new("https://www.774.ai/", "https://www.774.ai/member"), V774incMembers,
                wiki: new("https://wikiwiki.jp/774inc/"));
        public static LiverGroupDetail VOMS { get; }
            = new(60000, "voms", "VOMS", null,
                new("https://voms.net/", "https://voms.net/monsters/"), VomsMembers,
                wiki: new("https://wikiwiki.jp/voms_project/"));
        public static LiverGroupDetail None { get; } = new(990000, "none", "None", null, new(), null);
        public static IReadOnlyList<LiverGroupDetail> GroupList { get; }
            = new List<LiverGroupDetail> { Nijiasnji, Hololive, DotLive, VLive, V774inc, VOMS, None };

        private static async Task<HashSet<LiverDetail>> NijisanjiMembers(WebClient wc, int count)
        {
            string htmll = await wc.DownloadStringTaskAsync($"https://nijisanji.ichikara.co.jp/member/");
            var doc = new HtmlDocument();
            doc.LoadHtml(htmll);

            var txt = "/html/body/div/div/div/div/section/div/div/div/div/div/div/div/div/" +
                "div[@class='elementor-tabs-content-wrapper']/div[@id='elementor-tab-content-7551']/div[@id='liver_list']/div";
            var livers = doc.DocumentNode.SelectNodes(txt);
            if (livers.Count == count) return null;
            var dic = new SortedDictionary<int, LiverDetail>();
            for(int i = 0;i < livers.Count;i++)
            {
                var liver = livers[i];
                int no = int.Parse(liver.Attributes["data-debut"].Value, SettingData.Culture) / 10;

                var url = liver.SelectSingleNode("./div/div/a").Attributes["href"].Value.Trim();
                var name = liver.SelectSingleNode("./div/div/a/span").InnerText.Trim();
                var htmlm = wc.DownloadString(url);
                var doc1 = new HtmlDocument();
                doc1.LoadHtml(htmlm);

                var txtm = "//html/body/div/div/div/div/section/div/div/div/div/div/div/div/div/a";
                var media = doc1.DocumentNode.SelectNodes(txtm);
                string twitter = null, youtube = null;
                foreach(var m in media)
                {
                    var link = m.Attributes["href"].Value.Trim();
                    if (link.Contains("https://twitter.com/")) twitter = link;
                    else if (link.Contains("https://www.youtube.com/")) youtube = link;
                }
                var detail = new LiverDetail(10000 + no, Nijiasnji, name, youtube, twitter);

                var req = SettingData.YouTubeService.Channels.List("snippet");
                req.Id = detail.YouTubeId;
                var res = await req.ExecuteAsync();
                detail.SetChannelName(res.Items[0].Snippet.Title);

                dic.Add(no, detail);
                await LocalConsole.Log("MemberLoader",
                    new(LogSeverity.Info, "Nijisanji", $"Complete inspect liver {i + 1}/{livers.Count}[{no}] : {name}"));
            }
            return new(dic.Values);
        }
        private static async Task<HashSet<LiverDetail>> HololiveMembers(WebClient wc, int count)
        {
            return new();
        }
        private static async Task<HashSet<LiverDetail>> DotliveMembers(WebClient wc, int count)
        {
            return new();
        }
        private static async Task<HashSet<LiverDetail>> VliveMembers(WebClient wc, int count)
        {
            return new();
        }
        private static async Task<HashSet<LiverDetail>> V774incMembers(WebClient wc, int count)
        {
            return new();
        }
        private static async Task<HashSet<LiverDetail>> VomsMembers(WebClient wc, int count)
        {
            return new();
        }
    }

    [Serializable]
    public class LiverGroupDetail : Address
    {
        public string GroupId { get; }
        public VWebPage HomePage { get; }
        public CompanyDetail ProducedCompany { get; }
        internal MemberLoad MemberLoadAction { get; }
        public VWebPage UnofficialWiki { get; }
        public bool IsExistBooth { get; }
        public bool IsExistStore { get { return StorePage.Url != null; } }
        public VStorePage StorePage { get; }

        internal delegate Task<HashSet<LiverDetail>> MemberLoad(WebClient wc, int count);

        internal LiverGroupDetail(int id, string groupid, string name, CompanyDetail corp, VWebPage hp, MemberLoad action,
            string youtube = null, string twitter = null, VWebPage? wiki = null, bool booth = false, VStorePage? store = null)
            : base(id, name, youtube, twitter)
        {
            GroupId = groupid;
            HomePage = hp;
            ProducedCompany = corp;
            MemberLoadAction = action;
            UnofficialWiki = wiki == null ? new(null) : (VWebPage)wiki;
            IsExistBooth = booth;
            StorePage = store == null ? new(null, null) : (VStorePage)store;
        }
    }

    public struct VWebPage
    {
        public string HomePage { get; }
        public string MemberPage { get; }
        private bool IsEncode { get; }

        public VWebPage(string home, string member = "", bool encode = false)
        {
            HomePage = home;
            if (member == null || member == "") member = home;
            else if (!member.Contains(home)) member = home + member;
            MemberPage = member;
            IsEncode = encode;
        }

        public string WikiMemberUrl(string liver_name)
        {
            if (IsEncode)
            {
                var bs = Encoding.GetEncoding("euc-jp").GetBytes(liver_name);
                liver_name = "";
                foreach (var b in bs)
                {
                    liver_name += "%" + Convert.ToString(b, 16);
                }
            }
            return MemberPage + liver_name;
        }
    }

    public struct VStorePage
    {
        public string Url { get; }
        internal ProductLoad ProductLoadAction { get; }

        internal delegate Task<List<ProductBase>> ProductLoad();

        internal VStorePage(string address, ProductLoad product)
        {
            Url = address;
            ProductLoadAction = product;
        }
    }
}

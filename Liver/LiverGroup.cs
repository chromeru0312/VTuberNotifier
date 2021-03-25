using Discord;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VTuberNotifier.Watcher.Event;
using VTuberNotifier.Watcher.Store;
using static VTuberNotifier.Liver.ProducedCompany;

namespace VTuberNotifier.Liver
{
    public static class LiverGroup
    {
        public static LiverGroupDetail Nijiasnji { get; }
            = new(10000, "nijisanji", "にじさんじ", Ichikara,
                new("https://nijisanji.ichikara.co.jp/", "https://nijisanji.ichikara.co.jp/member/", false),
                NijisanjiMembers, "UCX7YkU9nEeaoZbkVLVajcMg", "nijisanji_app",
                new("https://wikiwiki.jp/nijisanji/"), true,
                new("https://shop.nijisanji.jp/", typeof(NijisanjiNewProductEvent),
                    typeof(NijisanjiStartSellEvent),NijisanjiWatcher.Instance.GetNewProduct));
        public static LiverGroupDetail Hololive { get; }
            = new(20000, "hololive", "ホロライブ", Cover,
                new("https://www.hololive.tv", "https://www.hololive.tv/member", false),
                HololiveMembers, "UCJFZiqLMntJufDCHc6bQixg", "hololivetv",
                new("https://seesaawiki.jp/hololivetv/", "d/", encode: true), true);
        public static LiverGroupDetail Dotlive { get; }
            = new(30000, "dotlive", ".Live", AppLand,
                new("https://dotlive.jp/", "https://dotlive.jp/member/"),
                DotliveMembers, "UCAZ_LA7f0sjuZ1Ni8L2uITw", "dotLIVEyoutuber",
                new("https://seesaawiki.jp/siroyoutuber/", "d/", encode: true), true,
                new("https://4693.live/", null, null, null));
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
            = new List<LiverGroupDetail> { Nijiasnji, Hololive, Dotlive, VLive, V774inc, VOMS, None };

        private static async Task<HashSet<LiverDetail>> NijisanjiMembers(WebClient wc, HtmlDocument _, HashSet<LiverDetail> set)
        {
            var str = await wc.DownloadStringTaskAsync(GetUrl("moira"));
            var livers = JObject.Parse(str)["pageProps"]["livers"];
            var contents = livers["contents"].AsEnumerable();
            var count = contents.Count();
            if (count != set.Count)
            {
                set = new();
                var no = 10001;
                foreach (var c in contents)
                {
                    var url = GetUrl(c["slug"].Value<string>());
                    var str_d = await wc.DownloadStringTaskAsync(url);
                    var liver = JObject.Parse(str_d)["pageProps"]["liver"];

                    var name = liver["name"].Value<string>();
                    var links = liver["social_links"];
                    var youtube = links["youtube"].Value<string>();
                    var twitter = links["twitter"].Value<string>();

                    set.Add(new(no, Nijiasnji, name, youtube, twitter));
                    await LocalConsole.Log("MemberLoader",
                        new(LogSeverity.Info, "Nijisanji", $"Complete inspect liver {no - 10000}/{count} : {name}"));
                    no++;
                }
            }
            return set;

            static string GetUrl(string name)
            {
                return $"https://www.nijisanji.jp/_next/data/AssLCig6Qu-ghaLHbaxgh/members/{name}.json" +
                    "?filter=%E3%81%AB%E3%81%98%E3%81%95%E3%82%93%E3%81%98&order=debut_at&asc=true";
            }
        }
        private static async Task<HashSet<LiverDetail>> HololiveMembers(WebClient wc, HtmlDocument _, HashSet<LiverDetail> set)
        {
            wc = SettingData.GetWebClient();
            var str = await wc.DownloadStringTaskAsync("https://www.hololive.tv/r/v1/sites/11822129/portfolio/categories/47579/products?per=1000");
            var json = JObject.Parse(str);
            var data = json["data"];
            var count = data["paginationMeta"]["totalCount"].Value<int>();
            if (count != set.Count)
            {
                set = new();
                var no = 20001;
                foreach (var liver in data["products"].AsEnumerable())
                {
                    var name = liver["name"].Value<string>();
                    var youtube = liver["button"]["url"].Value<string>();

                    var url = "https://www.hololive.tv" + liver["slugPath"].Value<string>();
                    wc = SettingData.GetWebClient();
                    var html = await wc.DownloadStringTaskAsync(url);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    string twitter = null;
                    var txt = "//html/body/div[@id='s-page-container']/div/div/div/div[@class='page-wrapper']/div" +
                        "/div[@class='s-section s-page-section s-block-section s-layout-center']/div/div" +
                        "/div[@class='s-repeatable s-block s-component s-mh s-block-row-wrapper']/div/div/div/div/div/div/div/p/a";
                    var col = doc.DocumentNode.SelectNodes(txt);
                    foreach (var n in col)
                    {
                        var link = n.Attributes["href"].Value;
                        if (link.StartsWith("https://twitter.com/"))
                        {
                            twitter = link;
                            break;
                        }
                    }
                    set.Add(new(no, Hololive, name, youtube, twitter));
                    await LocalConsole.Log("MemberLoader",
                        new(LogSeverity.Info, "Hololive", $"Complete inspect liver {no - 20000}/{count} : {name}"));
                    no++;
                }
            }
            return set;
        }
        private static async Task<HashSet<LiverDetail>> DotliveMembers(WebClient _, HtmlDocument doc, HashSet<LiverDetail> set)
        {
            var livers = doc.DocumentNode.SelectNodes("/html/body/main/section/section[@class='profile']");
            if (livers.Count != set.Count)
            {
                var nset = new HashSet<LiverDetail>();
                for (int i = 0; i < livers.Count; i++)
                {
                    var liver = livers[i];

                    var name = liver.SelectSingleNode("./div[@class='name']/h2").InnerText;
                    var index = name.IndexOf('(');
                    if (index != -1) name = name[..index];
                    name = name.Trim();

                    var links = liver.SelectNodes("./ul[@class='link']/li/a");
                    DetectLink(links, out var twitter, out var youtube);

                    var old = set.FirstOrDefault(l => l.Name == name);
                    var id = old != null ? old.Id : 30000 + set.Select(l => l.Id - 30000).Concat(nset.Select(l => l.Id - 30000)).Append(0).Max() + 1;
                    nset.Add(new(id, Dotlive, name, youtube, twitter));
                    await LocalConsole.Log("MemberLoader",
                        new(LogSeverity.Info, "Dotlive", $"Complete inspect liver {i + 1}/{livers.Count} : {name}"));
                }
                set = nset;
            }
            return set;
        }
        private static async Task<HashSet<LiverDetail>> VliveMembers(WebClient wc, HtmlDocument doc, HashSet<LiverDetail> set)
        {
            var txt = "/html/body/div[@id='wrapper']/div[@class='animate']/div[@id='contents']/main/section/article/div/div" +
                "/div[@class='jin-3column']";
            var nodes = doc.DocumentNode.SelectNodes(txt);
            var livers = new List<HtmlNode>();
            foreach (var n in nodes)
            {
                livers.Add(n.SelectSingleNode("./div[@class='jin-3column-left']"));
                livers.Add(n.SelectSingleNode("./div[@class='jin-3column-center']"));
                livers.Add(n.SelectSingleNode("./div[@class='jin-3column-right']"));
            }
            livers.RemoveAll(n => n == null);
            if (livers.Count != set.Count)
            {
                var nset = new HashSet<LiverDetail>();
                for (int i = 0; i < livers.Count; i++)
                {
                    var liver = livers[i];
                    var nnode = liver.SelectSingleNode("./div[@class='jincol-h3 jincolumn-h3style1']");
                    if (nnode == null) continue;
                    var name = nnode.InnerText.Split('/')[0].Trim();
                    var links = liver.SelectNodes("./p/span/span/a");
                    DetectLink(links, out var twitter, out var youtube);

                    var old = set.FirstOrDefault(l => l.Name == name);
                    var id = old != null ? old.Id : 40000 + set.Select(l => l.Id - 40000).Concat(nset.Select(l => l.Id - 40000)).Append(0).Max() + 1;
                    nset.Add(new(id, VLive, name, youtube, twitter));
                    await LocalConsole.Log("MemberLoader",
                        new(LogSeverity.Info, "Vlive", $"Complete inspect liver {i + 1}/{livers.Count} : {name}"));
                }
                set = nset;
            }
            return set;
        }
        private static async Task<HashSet<LiverDetail>> V774incMembers(WebClient wc, HtmlDocument doc, HashSet<LiverDetail> set)
        {
            var txt = "/html/body/div/div/div[@id='site-root']/div/main/div/div/div/div[@class='_2S9ms']/div/div/div/section" +
                "/div[@class='_3BQmz']/div[@class='_1HpZ_']/div[@data-testid='inline-content']/div/div/div/div";
            var livers = doc.DocumentNode.SelectNodes(txt);
            if (livers.Count != set.Count)
            {
                var nset = new HashSet<LiverDetail>();
                for (int i = 0; i < livers.Count; i++)
                {
                    var liver = livers[i];
                    var name = liver.SelectSingleNode("./div[@class='_1Z_nJ']/h1").InnerText.Trim();
                    var links = liver.SelectNodes("./div[@class='_2qI_L']/ul/li/a");
                    DetectLink(links, out var twitter, out var youtube);

                    var old = set.FirstOrDefault(l => l.Name == name);
                    var id = old != null ? old.Id : 50000 + set.Select(l => l.Id - 50000).Concat(nset.Select(l => l.Id - 50000)).Append(0).Max() + 1;
                    nset.Add(new(id, V774inc, name, youtube, twitter));
                    await LocalConsole.Log("MemberLoader",
                        new(LogSeverity.Info, "774inc", $"Complete inspect liver {i + 1}/{livers.Count} : {name}"));
                }
                set = nset;
            }
            return set;
        }
        private static async Task<HashSet<LiverDetail>> VomsMembers(WebClient wc, HtmlDocument doc, HashSet<LiverDetail> set)
        {
            var livers = doc.DocumentNode.SelectNodes("/html/body/div[@id='wrapper']/main/section/div/div/ul/li/a");
            if (livers.Count != set.Count)
            {
                var nset = new HashSet<LiverDetail>();
                for (int i = 0; i < livers.Count; i++)
                {
                    var liver = livers[i];
                    var url = "https://voms.net" + liver.Attributes["href"].Value.Trim();

                    var htmlm = await wc.DownloadStringTaskAsync(url);
                    var doc1 = new HtmlDocument();
                    doc1.LoadHtml(htmlm);

                    var profile = doc1.DocumentNode.SelectSingleNode("/html/body/div[@id='wrapper']/main/div/div/div");
                    var name = profile.SelectSingleNode("./div[@class='monsters__iconArea']/div/h3").InnerText.Trim();
                    var links = profile.SelectNodes("./ul/li/a");
                    DetectLink(links, out var twitter, out var youtube);

                    var old = set.FirstOrDefault(l => l.Name == name);
                    var id = old != null ? old.Id : 60000 + set.Select(l => l.Id - 60000).Concat(nset.Select(l => l.Id - 60000)).Append(0).Max() + 1;
                    nset.Add(new(id, VOMS, name, youtube, twitter));
                    await LocalConsole.Log("MemberLoader",
                        new(LogSeverity.Info, "Voms", $"Complete inspect liver {i + 1}/{livers.Count} : {name}"));
                }
                set = nset;
            }
            return set;
        }
        private static void DetectLink(HtmlNodeCollection nodes, out string twitter, out string youtube)
        {
            twitter = null;
            youtube = null;
            foreach (var l in nodes)
            {
                var link = l.Attributes["href"].Value.Trim();
                var url = link;
                while (true)
                {
                    if (url.StartsWith("https://twitter.com/")) twitter = url;
                    else if (url.StartsWith("https://www.youtube.com/channel")) youtube = url;
                    else if (url.StartsWith("https://www.youtube.com/watch"))
                    {
                        var i1 = url.IndexOf("v=");
                        if (i1 == -1) break;
                        var i2 = url.IndexOf('&', i1);
                        var id = i2 == -1 ? url[(i1 + 2)..] : url[(i1 + 2)..i2];

                        youtube = "https://www.youtube.com/channel/" + GetChannelIdFromVideo(id);
                    }
                    else if (url.StartsWith("https://youtu.be/"))
                    {
                        var i = url.IndexOf('?');
                        var id = i == -1 ? url[17..] : url[17..i];

                        youtube = "https://www.youtube.com/channel/" + GetChannelIdFromVideo(id);
                    }
                    else
                    {
                        try
                        {
                            var req = (HttpWebRequest)WebRequest.Create(url);
                            req.AllowAutoRedirect = false;
                            var res = (HttpWebResponse)req.GetResponse();
                            if (res.StatusCode == HttpStatusCode.Moved || res.StatusCode == HttpStatusCode.Redirect)
                            {
                                url = res.Headers["Location"];
                                continue;
                            }
                        }
                        catch (Exception) { }
                    }
                    break;

                    static string GetChannelIdFromVideo(string id)
                    {
                        var req = SettingData.YouTubeService.Videos.List("snippet");
                        req.Id = id;
                        req.MaxResults = 1;

                        var res = req.Execute();
                        if (res.Items.Count == 0) return null;
                        return res.Items[0].Snippet.ChannelId;
                    }
                }
                if (youtube != null && twitter != null) break;
            }
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
        public bool IsExistStore { get { return StoreInfo.Url != null; } }
        public VStoreInfo StoreInfo { get; }

        internal delegate Task<HashSet<LiverDetail>> MemberLoad(WebClient wc, HtmlDocument doc, HashSet<LiverDetail> set);

        internal LiverGroupDetail(int id, string groupid, string name, CompanyDetail corp, VWebPage hp, MemberLoad action,
            string youtube = null, string twitter = null, VWebPage? wiki = null, bool booth = false, VStoreInfo? store = null)
            : base(id, name, youtube, twitter)
        {
            GroupId = groupid;
            HomePage = hp;
            ProducedCompany = corp;
            MemberLoadAction = action;
            UnofficialWiki = wiki == null ? new(null) : (VWebPage)wiki;
            IsExistBooth = booth;
            StoreInfo = store == null ? new(null, null, null, null) : (VStoreInfo)store;
        }

        public async Task<HashSet<LiverDetail>> LoadMembers(HashSet<LiverDetail> set)
        {
            if (HomePage.MemberPage == null) return new();
            using var wc = SettingData.GetWebClient();
            HashSet<LiverDetail> s = set;
            try
            {
                var doc = new HtmlDocument();
                if (HomePage.IsLoad)
                {
                    string html = await wc.DownloadStringTaskAsync(HomePage.MemberPage);
                    doc.LoadHtml(html);
                }

                if (set == null) set = new();
                if (MemberLoadAction != null) s = await MemberLoadAction.Invoke(wc, doc, set);
                foreach (var liver in s)
                {
                    if (liver.YouTubeId == null) continue;
                    var req = SettingData.YouTubeService.Channels.List("snippet");
                    req.Id = liver.YouTubeId;
                    var res = await req.ExecuteAsync();
                    liver.SetChannelName(res.Items[0].Snippet.Title);
                }
                await LocalConsole.Log("MemberLoader", new(LogSeverity.Info, GroupId, $"Finish Loading Members."));
            }
            catch (Exception e)
            {
                await LocalConsole.Log("MemberLoader", new(LogSeverity.Error, GroupId, $"An error has occured.", e));
            }
            return s;
        }
    }

    public struct VWebPage
    {
        public string HomePage { get; }
        public string MemberPage { get; }
        public bool IsLoad { get; }
        private bool IsEncode { get; }

        public VWebPage(string home, string member = "", bool load = true, bool encode = false)
        {
            HomePage = home;
            if (member == null || member == "") member = home;
            else if (!member.Contains(home)) member = home + member;
            MemberPage = member;
            IsLoad = load;
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

    public struct VStoreInfo
    {
        public string Url { get; }
        [field: JsonIgnore]
        public Type NewProductEventType { get; }
        [field: JsonIgnore]
        public Type StartSaleEventType { get; }
        internal ProductLoad ProductLoadAction { get; }

        internal delegate Task<List<ProductBase>> ProductLoad();

        internal VStoreInfo(string address, Type npe, Type sse, ProductLoad product)
        {
            Url = address;
            NewProductEventType = npe;
            StartSaleEventType = sse;
            ProductLoadAction = product;
        }
    }
}

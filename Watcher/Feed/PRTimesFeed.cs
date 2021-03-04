using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;
using VTuberNotifier.Notification;
using VTuberNotifier.Liver;

namespace VTuberNotifier.Watcher.Feed
{
    public class PRTimesFeed
    {
        public static PRTimesFeed Instance { get; private set; }
        public IReadOnlyDictionary<LiverGroupDetail, IReadOnlyList<PRTimesArticle>> FoundArticles { get; private set; }

        private PRTimesFeed()
        {
            var dic = new Dictionary<LiverGroupDetail, IReadOnlyList<PRTimesArticle>>();
            foreach (var group in LiverGroup.GroupList)
            {
                if (!group.IsExistBooth) continue;
                if (DataManager.Instance.TryDataLoad($"article/{group.Name}", out List<PRTimesArticle> list))
                    dic.Add(group, list);
                else dic.Add(group, new List<PRTimesArticle>());
            }
            FoundArticles = dic;
        }
        public static void CreateInstance()
        {
            if (Instance != null) return;
            Instance = new PRTimesFeed();
        }

        public async Task<List<PRTimesArticle>> ReadFeed(LiverGroupDetail group)
        {
            var list = new List<PRTimesArticle>();
            using var wc = new WebClient() { Encoding = Encoding.UTF8 };
            var cid = group.ProducedCompany.Id;
            XDocument xml = XDocument.Load($"https://prtimes.jp/companyrdf.php?company_id={cid}");
            XNamespace ns = xml.Root.Attribute("xmlns").Value;
            var articles = new List<XElement>(xml.Root.Elements(ns + "item"));
            for (int i = 0; i < articles.Count; i++)
            {
                if (i == 5) break;
                var article = articles[i];
                var link = article.Element(ns + "link").Value.Trim();
                var title = article.Element(ns + "title").Value.Trim();
                var aid = uint.Parse(link.Split('/')[^1].Split('.')[0], SettingData.Culture) + (uint)cid * 10000;
                Console.WriteLine($"(PRTimes-{group.Name}) ID:{aid} / Title:{title}");

                var doc = new HtmlDocument();
                string html = await wc.DownloadStringTaskAsync(link);
                doc.LoadHtml(html);
                var text = "//html/body/div[@class='container container-content']/main/div[@class='content']/article/div";
                var content = doc.DocumentNode.SelectSingleNode(text + "/div").InnerText.Trim();
                var datetxt = text + "/header/div[@class='information-release']/time";
                var date = DateTime.Parse(doc.DocumentNode.SelectSingleNode(datetxt).Attributes["datetime"].Value.Trim(), SettingData.Culture);
                list.Add(new(aid, group, title, link, date, content));
            }
            if (list.Count > 0)
            {
                FoundArticles = new Dictionary<LiverGroupDetail, IReadOnlyList<PRTimesArticle>>(FoundArticles)
                { [group] = new List<PRTimesArticle>(FoundArticles[group].Concat(list)) };
                await DataManager.Instance.DataSaveAsync($"article/{group.Name}", FoundArticles[group], true);
            }
            return list;
        }
    }

    [Serializable]
    public class PRTimesArticle : IEquatable<PRTimesArticle>, INotificationContent
    {
        public uint Id { get; }
        public LiverGroupDetail Group { get; }
        public string Title { get; }
        public string Url { get; }
        public DateTime Update { get; }
        public IReadOnlyList<LiverDetail> Livers { get; }

        [JsonIgnore]
        public IReadOnlyDictionary<string, string> ContentFormat => new Dictionary<string, string>()
            {
                { "Date", (this as INotificationContent).ConvertDateTime(Update) },
                { "Id", Id.ToString() }, { "Title", Title }, { "URL", Url }
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

        public PRTimesArticle(uint id, LiverGroupDetail group, string title, string url, DateTime update, string content)
        {
            Id = id;
            Title = title;
            Url = url;
            Update = update;
            Livers = DetectLiver(group, content);
        }

        private static List<LiverDetail> DetectLiver(LiverGroupDetail group, string content)
        {
            List<LiverDetail> livers = new(LiverData.GetLiversList(group)), res = new();
            foreach (var liver in livers)
            {
                if (content.Contains(liver.Name)) res.Add(liver);
            }
            return res;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Update);
        }

        public override bool Equals(object obj)
        {
            return obj is PRTimesArticle a && Equals(a);
        }
        public bool Equals(PRTimesArticle other)
        {
            return Id == other.Id && Update == other.Update;
        }
    }
}

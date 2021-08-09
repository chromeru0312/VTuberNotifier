using Discord;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;
using VTuberNotifier.Notification;
using VTuberNotifier.Liver;
using System.Net.Http;
using System.Net;

namespace VTuberNotifier.Watcher
{
    public class PRTimesFeed
    {
        public static PRTimesFeed Instance { get; private set; }
        public IReadOnlyDictionary<LiverGroupDetail, IReadOnlyList<PRTimesArticle>> FoundArticles { get; private set; }
        private DateTime SkippingDate { get; set; }

        private PRTimesFeed()
        {
            var dic = new Dictionary<LiverGroupDetail, IReadOnlyList<PRTimesArticle>>();
            foreach (var group in LiverGroup.GroupList)
            {
                if (group.ProducedCompany == null) continue;
                if (DataManager.Instance.TryDataLoad($"article/{group.GroupId}", out List<PRTimesArticle> list))
                    dic.Add(group, list);
                else dic.Add(group, new List<PRTimesArticle>());
            }
            FoundArticles = dic;
            SkippingDate = DateTime.MinValue;
        }
        public static void CreateInstance()
        {
            if (Instance != null) return;
            Instance = new PRTimesFeed();
        }

        public async Task<List<PRTimesArticle>> ReadFeed(LiverGroupDetail group)
        {
            var list = new List<PRTimesArticle>();
            if (group.ProducedCompany == null || SkippingDate > DateTime.Now)
                return list;
            var id = group.ProducedCompany.Id;
            LocalConsole.Log(this, new LogMessage(LogSeverity.Debug, "NewArticle", $"Start task. [company:{group.GroupId}]"));

            XDocument xml;
            try
            {
                xml = XDocument.Load($"https://prtimes.jp/companyrdf.php?company_id={id}");
            }
            catch (HttpRequestException e)
            {
                if (e.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    SkippingDate = DateTime.Now.AddMinutes(55);
                    LocalConsole.Log(this, new LogMessage(LogSeverity.Warning, "NewArticle",
                        "PRTimes service is currently temporarily unavailable."));
                    return list;
                }
                throw;
            }
            catch { throw; }
            XNamespace ns = xml.Root.Attribute("xmlns").Value;
            var articles = new List<XElement>(xml.Root.Elements(ns + "item"));
            for (int i = 0; i < articles.Count; i++)
            {
                var article = articles[i];
                var link = article.Element(ns + "link").Value.Trim();
                var title = article.Element(ns + "title").Value.Trim();
                var aid = uint.Parse(link.Split('/')[^1].Split('.')[0], Settings.Data.Culture) + (uint)id * 10000;
                if (FoundArticles[group].FirstOrDefault(a => a.Id == aid) != null) break;

                var doc = new HtmlDocument();
                string html = await Settings.Data.HttpClient.GetStringAsync(link);
                doc.LoadHtml(html);
                var text = "//html/body/div[@class='container container-content']/main/div[@class='content']/article/div";
                var cnode = doc.DocumentNode.SelectSingleNode(text + "/div");
                var content = cnode.InnerText.Trim();
                var links = cnode.SelectNodes("./div/div/a")?.Select(n => n.Attributes["href"].Value);
                var datetxt = text + "/header/div[@class='information-release']/time";
                var date = DateTime.Parse(doc.DocumentNode.SelectSingleNode(datetxt).Attributes["datetime"].Value.Trim(), Settings.Data.Culture);
                list.Add(new(aid, group, link, title, content, links ?? new List<string>(), date));
            }
            if (list.Count > 0)
            {
                FoundArticles = new Dictionary<LiverGroupDetail, IReadOnlyList<PRTimesArticle>>(FoundArticles)
                { [group] = new List<PRTimesArticle>(FoundArticles[group].Concat(list)) };
                await DataManager.Instance.DataSaveAsync($"article/{group.GroupId}", FoundArticles[group], true);
            }
            LocalConsole.Log(this, new (LogSeverity.Debug, "NewArticle", $"End task. [company:{group.GroupId}]"));
            return list;
        }
    }

    [Serializable]
    [JsonConverter(typeof(PRTimesArticleConverter))]
    public class PRTimesArticle : IEquatable<PRTimesArticle>, INotificationContent
    {
        public uint Id { get; }
        string INotificationContent.Id => Id.ToString();
        public LiverGroupDetail Group { get; }
        public string Title { get; }
        public string Url { get; }
        public TextContent Content { get; }
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

        public PRTimesArticle(uint id, LiverGroupDetail group, string url, string title, string content,
            IEnumerable<string> links, DateTime? update = null)
            : this(id, group, url, title, new(content, links), update ?? DateTime.Now, DetectLiver(group, content)) { }
        private PRTimesArticle(uint id, LiverGroupDetail group,
            string url, string title, TextContent content, DateTime update, List<LiverDetail> livers)
        {
            Id = id;
            Group = group;
            Url = url;
            Title = title;
            Content = content;
            Update = update;
            Livers = livers;
        }

        private static List<LiverDetail> DetectLiver(LiverGroupDetail group, string content)
        {
            List<LiverDetail> livers = new(LiverData.GetLiversList(group)), res = new();
            List<char> chars = new() { ',', '/', ' ', '\n', '、', '・' },
                scs = new(chars) { '(', '（', '「' }, ecs = new(chars) { ')', '）', '」' };
            content = content.ToLower();
            foreach (var liver in livers)
            {
                var name = liver.Name;
                var pos = content.IndexOf(name);
                while (pos != -1)
                {
                    if (ecs.Contains(content[pos + name.Length]) || scs.Contains(content[pos - 1]))
                    {
                        res.Add(liver);
                        break;
                    }
                    else pos = content.IndexOf(name, pos + name.Length);
                }
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

        public class PRTimesArticleConverter : JsonConverter<PRTimesArticle>
        {
            public override PRTimesArticle Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                reader.CheckStartToken();

                var id = reader.GetNextValue<uint>(options);
                var gid = reader.GetNextValue<string>(options);
                var group = LiverGroup.GroupList.FirstOrDefault(g => g.GroupId == gid);
                var url = reader.GetNextValue<string>(options);
                var title = reader.GetNextValue<string>(options);
                var content = reader.GetNextValue<TextContent>(options);
                var update = reader.GetNextValue<DateTime>(options);
                var livers = reader.GetNextValue<List<LiverDetail>>(options);

                reader.CheckEndToken();
                return new(id, group, title, url, content, update, livers);
            }

            public override void Write(Utf8JsonWriter writer, PRTimesArticle value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                writer.WriteNumber("Id", value.Id);
                writer.WriteString("Group", value.Group.GroupId);
                writer.WriteString("Url", value.Url);
                writer.WriteString("Title", value.Title);
                writer.WriteValue("Content", value.Content, options);
                writer.WriteString("Update", value.Update.ToString("G"));
                writer.WriteValue("Livers", value.Livers, options);

                writer.WriteEndObject();
            }
        }
    }
}
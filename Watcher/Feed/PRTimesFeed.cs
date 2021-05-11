using Discord;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
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
                if (group.ProducedCompany == null) continue;
                if (DataManager.Instance.TryDataLoad($"article/{group.GroupId}", out List<PRTimesArticle> list))
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
            if (group.ProducedCompany == null) return list;
            var id = group.ProducedCompany.Id;
            await LocalConsole.Log(this, new LogMessage(LogSeverity.Debug, "NewArticle", $"Start task. [company:{group.GroupId}]"));

            using var wc = SettingData.GetWebClient();
            XDocument xml = XDocument.Load($"https://prtimes.jp/companyrdf.php?company_id={id}");
            XNamespace ns = xml.Root.Attribute("xmlns").Value;
            var articles = new List<XElement>(xml.Root.Elements(ns + "item"));
            for (int i = 0; i < articles.Count; i++)
            {
                var article = articles[i];
                var link = article.Element(ns + "link").Value.Trim();
                var title = article.Element(ns + "title").Value.Trim();
                var aid = uint.Parse(link.Split('/')[^1].Split('.')[0], SettingData.Culture) + (uint)id * 10000;
                if (FoundArticles[group].FirstOrDefault(a => a.Id == aid) != null) break;

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
                await DataManager.Instance.DataSaveAsync($"article/{group.GroupId}", FoundArticles[group], true);
            }
            await LocalConsole.Log(this, new LogMessage(LogSeverity.Debug, "NewArticle", $"End task. [company:{group.GroupId}]"));
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
            : this(id, group, title, url, update, DetectLiver(group, content)) { }
        private PRTimesArticle(uint id, LiverGroupDetail group, string title, string url, DateTime update, List<LiverDetail> livers)
        {
            Id = id;
            Group = group;
            Title = title;
            Url = url;
            Update = update;
            Livers = livers;
        }

        private static List<LiverDetail> DetectLiver(LiverGroupDetail group, string content)
        {
            List<LiverDetail> livers = new(LiverData.GetLiversList(group)), res = new();
            List<char> chars = new() { ',', '/', ' ', '\n', '、', '・' },
                scs = new(chars) { '(', '（', '「' }, ecs = new(chars) { ')', '）', '」' };
            foreach (var liver in livers)
            {
                var name = liver.Name;
                if (content.Contains(name))
                {
                    var pos = content.IndexOf(name);
                    if (ecs.Contains(content.ToLower()[pos + name.Length]) || scs.Contains(content.ToLower()[pos - 1]))
                        res.Add(liver);
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
                if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

                reader.Read();
                reader.Read();
                var id = reader.GetUInt32();
                reader.Read();
                reader.Read();
                var gid = reader.GetString();
                var group = LiverGroup.GroupList.FirstOrDefault(g => g.GroupId == gid);
                reader.Read();
                reader.Read();
                var title = reader.GetString();
                reader.Read();
                reader.Read();
                var url = reader.GetString();
                reader.Read();
                reader.Read();
                var update = DateTime.Parse(reader.GetString());
                reader.Read();
                reader.Read();
                var livers = JsonSerializer.Deserialize<List<LiverDetail>>(ref reader, options);

                reader.Read();
                if (reader.TokenType == JsonTokenType.EndObject) return new(id, group, title, url, update, livers);
                throw new JsonException();
            }

            public override void Write(Utf8JsonWriter writer, PRTimesArticle value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                writer.WriteNumber("Id", value.Id);
                writer.WriteString("Group", value.Group.GroupId);
                writer.WriteString("Title", value.Title);
                writer.WriteString("Url", value.Url);
                writer.WriteString("Update", value.Update.ToString("G"));
                writer.WritePropertyName("Livers");
                JsonSerializer.Serialize(writer, value.Livers, options);

                writer.WriteEndObject();
            }
        }
    }
}

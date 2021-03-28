using Discord;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VTuberNotifier.Liver;

namespace VTuberNotifier.Watcher.Store
{
    public class DotliveWatcher
    {
        public static DotliveWatcher Instance { get; private set; }
        public IReadOnlyList<DotliveProduct> FoundProducts { get; private set; }

        private DotliveWatcher() { }
        public static void CreateInstance()
        {
            if (Instance != null) return;
            Instance = new DotliveWatcher();
            LoadList();
        }
        private static void LoadList()
        {
            if (!DataManager.Instance.TryDataLoad("store/dotlive", out List<DotliveProduct> list))
                list = new();
            Instance.FoundProducts = list;
        }

        public async Task<List<ProductBase>> GetNewProduct()
        {
            await LocalConsole.Log(this, new LogMessage(LogSeverity.Debug, "NewProduct", "Start task."));
            using var wc = SettingData.GetWebClient();
            var list = new List<ProductBase>();
            var end = false;
            var page = 1;

            while (!end && page <= 3)
            {
                var htmll = await wc.DownloadStringTaskAsync($"https://4693.live/?page={page}");
                var doc = new HtmlDocument();
                doc.LoadHtml(htmll);

                var text = "//html/body/div[@class='menu-layout is-layout-1 has-mainVisual']/div[@class='is-width-large ']" +
                    "/main/div/div[@class='column1-main']/st-item-list/div[@class='c-itemList has-sticker-in-first-line']/ul/li/a";
                var nodes = doc.DocumentNode.SelectNodes(text);
                if (nodes == null || nodes.Count == 0) break;
                for (int i = 0; i < nodes.Count; i++)
                {
                    var url = "https://4693.live" + nodes[i].Attributes["href"].Value.Trim();
                    var htmlp = await wc.DownloadStringTaskAsync(url);
                    var doc1 = new HtmlDocument();
                    doc1.LoadHtml(htmlp);

                    var txt = "//html/body/div[@class='wrap']/div/div/main/div[@class='content cf']/section[@class='item_detail_content']";
                    var n = doc1.DocumentNode.SelectSingleNode(txt);
                    var caten = n.SelectSingleNode("./div[@class='content_breadcrumb']/div[@class='content_breadcrumb_category']/a");
                    var cate = caten?.InnerText.Trim();

                    var n1 = n.SelectSingleNode("./div[@class='main_content cf']/div[@class='main_content_result']");
                    var explain = n1.SelectSingleNode("./div[@class='main_content_result_item_list_detail']").InnerText.Trim();
                    var tagns = n1.SelectNodes("./ul[@class='hashtag_list_container']/li/a");
                    var tags = new List<string>();
                    if (tagns != null) tags.AddRange(tagns.Select(n => n.InnerText.Replace("#", "")));

                    var inner = n1.SelectSingleNode("./div[@class='main_content_result_inner']");
                    var title = inner.SelectSingleNode("./h1[@class='item_name']").InnerText.Trim();
                    var pn = inner.SelectSingleNode("./p[@class='item_price']");
                    var ps = pn.InnerText.Replace(pn.SelectSingleNode("./span").InnerText, "").Replace("&yen;", "").Replace("\\", "").Trim();
                    var price = int.Parse(ps, NumberStyles.Currency, SettingData.Culture);
                    var plist = new List<(string, int)>() { (title, price) };

                    DateTime? s = null, e = null;
                    var daten = inner.SelectSingleNode("./div[@class='sales_period_container']/p/span[@class='setting_in_sales_period']");
                    if (daten != null)
                    {
                        var strs = daten.InnerText.Replace('〜', '～').Split('～');
                        s = DateTime.Parse(strs[0]);
                        e = DateTime.Parse(strs[1]);
                    }

                    var dp = new DotliveProduct(title, url, cate, tags, explain, plist, s, e);
                    if (!FoundProducts.Contains(dp)) list.Add(dp);
                    else
                    {
                        end = true;
                        break;
                    }
                }
                page++;
            }

            if (list.Count > 0)
            {
                FoundProducts = new List<DotliveProduct>(FoundProducts.Concat(list.Select(p => (DotliveProduct)p)));
                await DataManager.Instance.DataSaveAsync("DotliveStoreProduct", FoundProducts, true);
            }
            await LocalConsole.Log(this, new LogMessage(LogSeverity.Debug, "NewProduct", "End task."));
            return list;
        }
    }

    [Serializable]
    [JsonConverter(typeof(DotliveProductConverter))]
    public class DotliveProduct : ProductBase, IProductTag
    {
        public IReadOnlyList<string> Tags { get; }
        private protected override string ExceptUrl { get; } = "https://4693.live/items/";

        public DotliveProduct(string title, string url, string category, List<string> tags, string explain,
            List<(string, int)> items, DateTime? start = null, DateTime? end = null)
            : base(title, url, LiverGroup.Nijiasnji, category, items,
                  new(IProductTag.LiverTag(LiverGroup.Dotlive, tags).Union(DetectLiver(LiverGroup.Dotlive, explain))), start, end)
        {
            Tags = tags ?? new();
        }
        protected private DotliveProduct(string id, string title, string url, string category, List<string> tags,
            List<ProductItem> items, List<LiverDetail> livers, DateTime? start, DateTime? end)
            : base(id, title, url, LiverGroup.Dotlive, category, items, livers, start, end)
        {
            Tags = tags;
        }

        public class DotliveProductConverter : ProductConverter<DotliveProduct>
        {
            public override DotliveProduct Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

                var (id, title, url, _, category, items, livers, start, end) = ReadBase(ref reader, type, options);
                var tags = IProductTag.ReadTags(ref reader, type, options);

                reader.Read();
                if (reader.TokenType == JsonTokenType.EndObject)
                    return new(id, title, url, category, tags, items, livers, start, end);
                throw new JsonException();
            }

            public override void Write(Utf8JsonWriter writer, DotliveProduct value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                WriteBase(writer, value, options);
                IProductTag.WriteTags(writer, value, options);

                writer.WriteEndObject();
            }
        }
    }
}
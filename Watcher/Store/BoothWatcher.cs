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
    public class BoothWatcher
    {
        public static BoothWatcher Instance { get; private set; }
        public IReadOnlyDictionary<LiverGroupDetail, IReadOnlyList<BoothProduct>> FoundProducts { get; private set; }

        private BoothWatcher()
        {
            var dic = new Dictionary<LiverGroupDetail, IReadOnlyList<BoothProduct>>();
            foreach (var group in LiverGroup.GroupList)
            {
                if (!group.IsExistBooth) continue;
                if (DataManager.Instance.TryDataLoad($"store/booth_{group.GroupId}", out List<BoothProduct> list))
                    dic.Add(group, list);
                else dic.Add(group, new List<BoothProduct>());
            }
            FoundProducts = dic;
        }
        public static void CreateInstance()
        {
            if (Instance != null) return;
            Instance = new BoothWatcher();
        }

        public async Task<List<BoothProduct>> GetNewProduct(LiverGroupDetail shop)
        {
            var list = new List<BoothProduct>();
            if (!shop.IsExistBooth) return list;
            LocalConsole.Log(this, new (LogSeverity.Debug, "NewProduct", $"Start task. [shop:{shop.GroupId}]"));
            var shop_name = shop.GroupId;

            string htmll = await Settings.Data.HttpClient.GetStringAsync($"https://{shop_name}.booth.pm/");
            var doc = new HtmlDocument();
            doc.LoadHtml(htmll);

            var text = "//html/body/div[@id='shop_default']/div/div[@class='layout-wrap']/main/div/section" +
                "/div[@class='container new-arrivals']/ul/shop-item-component/li";
            var nodes = doc.DocumentNode.SelectNodes(text);
            if (nodes == null) return list;
            for (int i = 0; i < nodes.Count; i++)
            {
                var id = long.Parse(nodes[i].Attributes["data-product-id"].Value.Trim(), Settings.Data.Culture);
                if (FoundProducts[shop].Count != 0 && FoundProducts[shop].FirstOrDefault(p => p.Id == id) != null) break;

                var htmlp = await Settings.Data.HttpClient.GetStringAsync($"https://{shop.GroupId}.booth.pm/items/{id}");
                var doc1 = new HtmlDocument();
                doc1.LoadHtml(htmlp);

                var stock = nodes[i].SelectSingleNode("./div/div[@class='item-card__thumbnail js-thumbnail']" +
                    "/div[@class='item-card__badges l-item-card-badge']/div[@class='badge empty-stock']") == null;

                var ntxt = "//html/body/div[@id='shop_default']/div/div[@class='layout-wrap']/main/div[@id='js-item-info-detail']";
                var n = doc1.DocumentNode.SelectSingleNode(ntxt);
                var explain = n.SelectSingleNode("./div[@class='main-info-column']/div[@class='description']/span").InnerText.Trim();

                var summary = n.SelectSingleNode("./div[@class='summary']");
                var title = summary.SelectSingleNode("./h1[@class='item-name bind-item-name']").InnerText.Trim();
                var cate = summary.SelectSingleNode("./div[@class='category']/a/span").InnerText.Trim();
                List<string> tags = summary.SelectSingleNode("./div[@class='tags']") == null ? new() :
                    new(summary.SelectNodes("./div[@class='tags']/ul[@id='tags']/li/a/div").Select(n => n.InnerText));

                var nidiv = summary.SelectSingleNode("./div[@class='cart-btns']/div");
                var items = new List<(string, int)>();
                if (nidiv.FirstChild.Name == "ul")
                {
                    var nitems = nidiv.SelectNodes("./ul[@id='variations']/li");
                    foreach (var ni in nitems)
                    {
                        var nname = ni.SelectSingleNode("./div[@class='variation-name']/div");
                        var name = nname == null ? title : nname.InnerText.Trim();
                        var price = int.Parse(ni.SelectSingleNode("./div[@class='variation-price']").InnerText[1..].Trim(),
                            NumberStyles.Currency, Settings.Data.Culture);
                        items.Add((name, price));
                    }
                }
                else if (nidiv.FirstChild.Name == "factory-items-component")
                {
                    var nitems = nidiv.SelectNodes("./factory-items-component/div/ul[@id='variations']/li");
                    foreach (var ni in nitems)
                    {
                        string name;
                        var label = ni.SelectSingleNode("./label");
                        if (label != null) name = label.InnerText.Trim();
                        else name = ni.SelectSingleNode("./div/span").InnerText.Trim();
                        var price = int.Parse(ni.SelectSingleNode("./input").Attributes["data-product-price"].Value.Trim(),
                            NumberStyles.Currency, Settings.Data.Culture);
                        items.Add((name, price));
                    }
                }
                else throw new NotImplementedException();

                var ndate = summary.SelectSingleNode("./div[@class='sale-period-wrapper on-sale']/div[@class='sale-period']");
                DateTime? s = null, e = null;
                if (ndate != null)
                {
                    var s_node = ndate.SelectSingleNode("./div[@class='start_at']");
                    if (s_node != null) s = DateTime.Parse(s_node.InnerText.Trim().Replace("から", ""), Settings.Data.Culture);
                    var e_node = ndate.SelectSingleNode("./div[@class='end_at']");
                    if (e_node != null) e = DateTime.Parse(e_node.InnerText.Trim().Replace("まで", ""), Settings.Data.Culture);
                }
                var bp = new BoothProduct(id, title, new(explain), shop, cate, tags, items, s, e);
                if (!FoundProducts[shop].Contains(bp)) list.Add(bp);
            }
            if (list.Count > 0)
            {
                FoundProducts = new Dictionary<LiverGroupDetail, IReadOnlyList<BoothProduct>>(FoundProducts)
                { [shop] = new List<BoothProduct>(FoundProducts[shop].Concat(list)) };
                await DataManager.Instance.DataSaveAsync($"store/booth/{shop.GroupId}", FoundProducts[shop], true);
            }
            LocalConsole.Log(this, new (LogSeverity.Debug, "NewProduct", $"End task. [shop:{shop.GroupId}]"));
            return list;
        }
    }

    [Serializable]
    [JsonConverter(typeof(BoothProductConverter))]
    public class BoothProduct : ProductBase, IProductTag
    {
        public new long Id { get; }
        public IReadOnlyList<string> Tags { get; }
        private protected override string ExceptUrl { get; } = "https://booth.pm/ja/items/";

        public BoothProduct(long id, string title, TextContent description, LiverGroupDetail shop, string category, List<string> tags,
            List<(string, int)> items, DateTime? start = null, DateTime? end = null)
            : base(id.ToString(), $"https://{shop.GroupId}.booth.pm/items/{id}", title, description, shop, category, items,
                  new(IProductTag.LiverTag(shop, tags).Union(DetectLiver(shop, description.Content))), start, end)
        {
            Id = id;
            Tags = tags;
        }
        protected private BoothProduct(long id, string url, string title, TextContent description, LiverGroupDetail shop, string category,
            List<string> tags, List<ProductItem> items, List<LiverDetail> livers, DateTime? start, DateTime? end)
            : base(id.ToString(), url, title, description, shop, category, items, livers, start, end)
        {
            Id = id;
            Tags = tags;
        }

        public class BoothProductConverter : ProductConverter<BoothProduct>
        {
            public override BoothProduct Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                reader.CheckStartToken();

                var (id, url, title, desc, shop, category, items, livers, start, end) = ReadBase(ref reader, options);
                var tags = IProductTag.ReadTags(ref reader, options);

                reader.CheckEndToken();
                return new(long.Parse(id, Settings.Data.Culture), url, title, desc, shop, category, tags, items, livers, start, end);
            }

            public override void Write(Utf8JsonWriter writer, BoothProduct value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                WriteBase(writer, value, options);
                IProductTag.WriteTags(writer, value, options);

                writer.WriteEndObject();
            }
        }
    }
}
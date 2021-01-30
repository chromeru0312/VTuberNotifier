using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
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
                if (DataManager.Instance.TryDataLoad($"booth/{group.Name}", out List<BoothProduct> list))
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
            var shop_name = shop.GroupId;

            using WebClient wc = new WebClient { Encoding = Encoding.UTF8 };
            string htmll = await wc.DownloadStringTaskAsync($"https://{shop_name}.booth.pm/");
            var doc = new HtmlDocument();
            doc.LoadHtml(htmll);

            var text = "//html/body/div[@id='shop_default']/div/div[@class='layout-wrap']/main/div/section" +
                "/div[@class='container new-arrivals']/ul/shop-item-component/li";
            var nodes = doc.DocumentNode.SelectNodes(text);
            for (int i = 0; i < nodes.Count; i++)
            {
                var id = long.Parse(nodes[i].Attributes["data-product-id"].Value.Trim());
                if (FoundProducts[shop].FirstOrDefault(p => p.Id == id) != null) break;

                var htmlp = await wc.DownloadStringTaskAsync($"https://{shop.GroupId}.booth.pm/items/{id}");
                var doc1 = new HtmlDocument();
                doc1.LoadHtml(htmlp);

                var stock = nodes[i].SelectSingleNode("./div/div[@class='item-card__thumbnail js-thumbnail']" +
                    "/div[@class='item-card__badges l-item-card-badge']/div[@class='badge empty-stock']") == null;

                var ntxt = "//html/body/div[@id='shop_default']/div/div[@class='layout-wrap']/main/div[@id='js-item-info-detail']";
                var n = doc1.DocumentNode.SelectSingleNode(ntxt);

                var title = n.SelectSingleNode("./h1[@class='item-name bind-item-name']").InnerText.Trim();
                var explain = n.SelectSingleNode("./div[@class='main-info-column']/div[@class='description']/span").InnerText.Trim();

                var summary = n.SelectSingleNode("./div[@class='summary']");
                var cate = summary.SelectSingleNode("./div[@class='category']/a/span").InnerText.Trim();
                var tags = new List<string>(summary.SelectNodes("./div[@class='tags']/ul[@id='tags']/li/a/div").Select(n => n.InnerText));

                var nidiv = summary.SelectSingleNode("./div[@class='cart-btns']/div");
                var items = new List<(string, int)>();
                if (nidiv.FirstChild.Name == "ul")
                {
                    var nitems = nidiv.SelectNodes("./ul[@id='variations']/li");
                    foreach (var ni in nitems)
                    {
                        var name = ni.SelectSingleNode("./div[@class='variation-name']/div").InnerText.Trim();
                        var price = int.Parse(ni.SelectSingleNode("./div[@class='variation-price']").InnerText, NumberStyles.Currency);
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
                        var price = int.Parse(ni.SelectSingleNode("./input").Attributes["data-product-price"].Value.Trim());
                        items.Add((name, price));
                    }
                }
                else throw new NotImplementedException();

                var ndate = summary.SelectSingleNode("./div[@class='sale-period-wrapper on-sale']/div[@class='sale-period']");
                DateTime? s = null, e = null;
                if (ndate != null)
                {
                    var s_node = ndate.SelectSingleNode("./div[@class='start_at']");
                    if (s_node != null) s = DateTime.Parse(s_node.InnerText.Trim().Replace("から", ""));
                    var e_node = ndate.SelectSingleNode("./div[@class='end_at']");
                    if (e_node != null) e = DateTime.Parse(e_node.InnerText.Trim().Replace("まで", ""));
                }
                list.Add(new(title, shop, id, cate, tags, explain, items, s, e));
                Console.WriteLine($"({shop.Name}-Booth) {i + 1}/{nodes.Count} の解析完了: {title}");
            }
            if (list.Count > 0)
            {
                FoundProducts = new Dictionary<LiverGroupDetail, IReadOnlyList<BoothProduct>>(FoundProducts)
                { [shop] = new List<BoothProduct>(FoundProducts[shop].Concat(list)) };
                await DataManager.Instance.DataSaveAsync($"booth/{shop.Name}", FoundProducts[shop], true);
            }
            return list;
        }
    }

    [Serializable]
    public class BoothProduct : ProductBase
    {
        public new long Id { get; }
        public IReadOnlyList<string> Tags { get; }
        private protected override string ExceptUrl { get; } = "https://booth.pm/ja/items/";

        public BoothProduct(string title, LiverGroupDetail shop, long id, string category, List<string> tags,
            string explain, List<(string, int)> items, DateTime? start = null, DateTime? end = null)
            : base(title, $"https://{shop.GroupId}.booth.pm/items/{id}", shop, category, items,
                  new(LiverTag(shop, tags).Union(DetectLiver(shop, explain))), start, end)
        {
            Id = id;
            Tags = tags;
        }

        private static List<LiverDetail> LiverTag(LiverGroupDetail shop, List<string> tags)
        {
            var list = LiverData.GetLiversList(shop);
            var res = new List<LiverDetail>();
            foreach (var t in tags)
            {
                var liver = list.FirstOrDefault(l => l.Name == t);
                if (liver == null) continue;
                res.Add(liver);
            }
            return res;
        }
    }
}
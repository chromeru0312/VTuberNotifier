using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VTuberNotifier.Liver;

namespace VTuberNotifier.Watcher.Store
{
    public class NijisanjiWatcher
    {
        public static NijisanjiWatcher Instance { get; private set; }
        public IReadOnlyList<NijisanjiProduct> FoundProducts { get; private set; }

        private NijisanjiWatcher()
        {
            var b = DataManager.Instance.TryDataLoad($"NijiasnjiStoreProduct", out List<NijisanjiProduct> list);
            if(!b) list = new List<NijisanjiProduct>();
            FoundProducts = list;
        }
        public static void CreateInstance()
        {
            if (Instance != null) return;
            Instance = new NijisanjiWatcher();
        }

        public async Task<List<ProductBase>> GetNewProduct()
        {
            using WebClient wc = new WebClient { Encoding = Encoding.UTF8 };
            var htmll = await wc.DownloadStringTaskAsync("https://shop.nijisanji.jp/s/niji/item/list?so=NW");

            var list = new List<ProductBase>();
            var doc = new HtmlDocument();
            doc.LoadHtml(htmll);

            var text = "//html/body/div/main/div/div/div[@id='itemList']/div/div[@class='first-area-item']";
            var nodes = doc.DocumentNode.SelectNodes(text);
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i].SelectSingleNode("./div/div[@class='itemname']");
                var title = n.InnerText.Trim();
                var url = "https://shop.nijisanji.jp" + n.SelectSingleNode("./a").Attributes["href"].Value.Trim();
                var labels = nodes[i].SelectSingleNode("./div/div[@class='tag-area']/div");

                var htmlp = await wc.DownloadStringTaskAsync(url);
                var doc1 = new HtmlDocument();
                doc1.LoadHtml(htmlp);
                var n1 = doc1.DocumentNode.SelectSingleNode("//html/body/div/main/div/div/div[@class='col-sm-12 col-md-6 item-main detail_right']");
                string cate = null, genre = null;
                var caten = n1.SelectSingleNode("./div[@class='item_category-wrap']");
                if (caten != null) cate = caten.InnerText.Trim().Replace("カテゴリー :", "");
                var genren = n1.SelectSingleNode("./div[@class='item_genre-wrap']");
                if (genren != null) genre = genren.InnerText.Trim().Replace("ジャンル : ", "");

                var ps = n1.SelectNodes("./div[@class='variation']/div[@class='variation-item']/div[@class='variation-wrap1']");
                var plist = new List<(string, int)>();
                foreach (var p in ps)
                {
                    var name = p.SelectSingleNode("./div[@class='variation-name']/span").InnerText.Trim();
                    var price = int.Parse(p.SelectSingleNode("./div[@class='variation-price']/div").InnerText.Trim(), NumberStyles.Currency);
                    plist.Add((name, price));
                }

                DateTime? s = null, e = null;
                var explain = doc1.DocumentNode.SelectSingleNode("//html/body/div/main/div/div/div[@id='detail-text']/div/div").InnerText;
                int si, ei;
                if (cate == "#デジタルグッズ" && genre == "#季節ボイス")
                {
                    if (title.Contains("コンプリートセット")) continue;
                    string search = "【販売期間】<br />\n";
                    si = explain.IndexOf(search) + search.Length;
                    ei = explain.IndexOf("の間、期間限定で販売！");
                }
                else
                {
                    string search = "期間限定販売】<br />\n";
                    si = explain.IndexOf(search) + search.Length;
                    ei = si + 39;
                }
                var date = explain[si..ei];
                var di = date.IndexOf('【');
                if (di != -1) date = date[..di];
                var datea = date.Split('～');
                s = JPDateConvert(datea[0]);
                e = JPDateConvert(datea[1]);

                list.Add(new NijisanjiProduct(title, url, cate, genre, explain, plist, s, e));
            }
            if (list.Count > 0)
            {
                FoundProducts = new List<NijisanjiProduct>(FoundProducts.Concat(list.Select(p => (NijisanjiProduct)p)));
                await DataManager.Instance.DataSaveAsync($"NijiasnjiStoreProduct", FoundProducts, true);
            }
            return list;
        }
        private static DateTime JPDateConvert(string date)
        {
            date = date.Replace('年', '/');
            date = date.Replace('月', '/');
            date = date.Replace('日', ' ');
            date = date.Remove(date.Length - 8, date.Length - 5);
            return DateTime.Parse(date);
        }
    }

    [Serializable]
    public class NijisanjiProduct : ProductBase
    {
        public string Genre { get; }
        private protected override string ExceptUrl { get; } = "https://shop.nijisanji.jp/s/niji/item/detail/";

        [JsonIgnore]
        public override IReadOnlyDictionary<string, string> ContentFormat
            => new Dictionary<string, string>(base.ContentFormat) { { "Genre", Genre } };

        public NijisanjiProduct(string title, string url, string category, string genre, string explain,
            List<(string, int)> items, DateTime? start = null, DateTime? end = null)
            : base(title, url, LiverGroup.Nijiasnji, category, items, DetectLiver(LiverGroup.Nijiasnji, explain), start, end)
        {
            Genre = genre;
        }
    }
}
using Discord;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VTuberNotifier.Liver;

namespace VTuberNotifier.Watcher.Store
{
    public class NijisanjiWatcher
    {
        public static NijisanjiWatcher Instance { get; private set; }
        public IReadOnlyList<NijisanjiProduct> FoundProducts { get; private set; }

        private NijisanjiWatcher() { }
        public static void CreateInstance()
        {
            if (Instance != null) return;
            Instance = new NijisanjiWatcher();
        }
        internal static void LoadList()
        {
            if (!DataManager.Instance.TryDataLoad("store/nijisanji", out List<NijisanjiProduct> list))
                list = new();
            Instance.FoundProducts = list;
        }

        public async Task<List<ProductBase>> GetNewProduct()
        {
            LocalConsole.Log(this, new (LogSeverity.Debug, "NewProduct", "Start task."));
            var list = new List<ProductBase>();
            var end = false;
            var page = 0;

            while (!end && page < 3)
            {
                var htmll = await Settings.Data.HttpClient.GetStringAsync($"https://shop.nijisanji.jp/s/niji/item/list?so=NW&page={page}");
                var doc = new HtmlDocument();
                doc.LoadHtml(htmll);

                var text = "//html/body/div/main/div/div/div[@id='itemList']/div/div[@class='first-area-item']";
                var nodes = doc.DocumentNode.SelectNodes(text);
                if (nodes == null || nodes.Count == 0) break;
                for (int i = 0; i < nodes.Count; i++)
                {
                    var n = nodes[i].SelectSingleNode("./div/div[@class='itemname']");
                    var title = n.InnerText.Trim();
                    var url = "https://shop.nijisanji.jp" + n.SelectSingleNode("./a").Attributes["href"].Value.Trim();
                    var labels = nodes[i].SelectSingleNode("./div/div[@class='tag-area']/div");
                    var coming = labels.SelectSingleNode("./span[@class='itemlabel itemlabel--coming']").Attributes["style"].Value.Trim() == "display: none;";

                    var htmlp = await Settings.Data.HttpClient.GetStringAsync(url);
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
                        var str = p.SelectSingleNode("./div[@class='variation-price']/div").InnerText.Replace("&yen;", "").Replace("\\", "").Trim();
                        var price = int.Parse(str, NumberStyles.Currency, Settings.Data.Culture);
                        plist.Add((name, price));
                    }

                    DateTime? s = null, e = null;
                    var explain = doc1.DocumentNode.SelectSingleNode("//html/body/div/main/div/div/div[@id='detail-text']/div/div").InnerText.Trim();
                    explain = explain.Replace('〜', '～');

                    var datestr = explain.Replace('年', '/');
                    datestr = datestr.Replace('月', '/');
                    datestr = datestr.Replace('日', ' ');
                    var m1 = Regex.Match(datestr, "\\d\\d?/\\d\\d?.*\\d\\d:\\d\\d～\\d\\d?/\\d\\d?.*\\d\\d:\\d\\d");
                    var m2 = Regex.Match(datestr, "\\d{4}/\\d\\d?/\\d\\d?.*\\d\\d:\\d\\d～\\d{4}/\\d\\d?/\\d\\d?.*\\d\\d:\\d\\d");
                    if (m1.Success)
                    {
                        var dates = m1.Value.Split('～');
                        var str_s = $"{Regex.Match(dates[0], "\\d\\d?/\\d\\d?").Value} {Regex.Match(dates[0], "\\d\\d:\\d\\d").Value}";
                        s = DateTime.ParseExact(str_s, "M/d HH:mm", Settings.Data.Culture);
                        var str_e = $"{Regex.Match(dates[1], "\\d\\d?/\\d\\d?").Value} {Regex.Match(dates[1], "\\d\\d:\\d\\d").Value}";
                        e = DateTime.ParseExact(str_e, "M/d HH:mm", Settings.Data.Culture);
                    }
                    else if (m2.Success)
                    {
                        var dates = m2.Value.Split('～');
                        var str_s = $"{Regex.Match(dates[0], "\\d{4}/\\d\\d?/\\d\\d?").Value} {Regex.Match(dates[0], "\\d\\d:\\d\\d").Value}";
                        s = DateTime.ParseExact(str_s, "yyyy/M/d HH:mm", Settings.Data.Culture);
                        var str_e = $"{Regex.Match(dates[1], "\\d{4}/\\d\\d?/\\d\\d?").Value} {Regex.Match(dates[1], "\\d\\d:\\d\\d").Value}";
                        e = DateTime.ParseExact(str_e, "yyyy/M/d HH:mm", Settings.Data.Culture);
                    }

                    var np = new NijisanjiProduct(url, title, new(explain), cate, genre, plist, s, e, coming);
                    if (!FoundProducts.Contains(np)) list.Add(np);
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
                FoundProducts = new List<NijisanjiProduct>(FoundProducts.Concat(list.Select(p => (NijisanjiProduct)p)));
                await DataManager.Instance.DataSaveAsync("store/nijisanji", FoundProducts, true);
            }
            LocalConsole.Log(this, new (LogSeverity.Debug, "NewProduct", "End task."));
            return list;
        }

        public async Task<bool> CheckOnSale(NijisanjiProduct product)
        {
            var htmll = await Settings.Data.HttpClient.GetStringAsync(product.Url);
            var doc = new HtmlDocument();
            doc.LoadHtml(htmll);

            var txt = "//html/body/div/main/div/div/div[@class='col-sm-12 col-md-6 item-main detail_right']" +
                "/div[@class='tag-area']/div/span[@class='itemlabel itemlabel--coming']";
            return doc.DocumentNode.SelectSingleNode(txt).Attributes["style"].Value.Trim() == "display: none;";
        }
    }

    [Serializable]
    [JsonConverter(typeof(NijisanjiProductConverter))]
    public class NijisanjiProduct : ProductBase
    {
        public string Genre { get; }
        private protected override string ExceptUrl { get; } = "https://shop.nijisanji.jp/s/niji/item/detail/";

        [JsonIgnore]
        public override IReadOnlyDictionary<string, string> ContentFormat
            => new Dictionary<string, string>(base.ContentFormat) { { "Genre", Genre } };

        public NijisanjiProduct(string url, string title,TextContent description, string category, string genre,
            List<(string, int)> items, DateTime? start = null, DateTime? end = null, bool issale = false)
            : base(null, url, title, description, LiverGroup.Nijiasnji, category, items,
                  DetectLiver(LiverGroup.Nijiasnji, description.Content), start, end)
        {
            Genre = genre;
            if (start == null) IsOnSale = issale;
        }
        private NijisanjiProduct(string id, string url, string title, TextContent description, string category, string genre,
            List<ProductItem> items, List<LiverDetail> livers, DateTime? start, DateTime? end)
            : base(id, url, title, description, LiverGroup.Nijiasnji, category, items, livers, start, end)
        {
            Genre = genre;
        }

        public class NijisanjiProductConverter : ProductConverter<NijisanjiProduct>
        {
            public override NijisanjiProduct Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                reader.CheckStartToken();

                var (id, url, title, desc, _, category, items, livers, start, end) = ReadBase(ref reader, options);
                var genre = reader.GetNextValue<string>(options);

                reader.CheckEndToken();
                return new(id, url, title, desc, category, genre, items, livers, start, end);
            }

            public override void Write(Utf8JsonWriter writer, NijisanjiProduct value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                WriteBase(writer, value, options);
                writer.WriteString("Genre", value.Genre);

                writer.WriteEndObject();
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using VTuberNotifier.Discord;
using VTuberNotifier.Liver;

namespace VTuberNotifier.Watcher.Store
{
    [Serializable]
    public abstract class ProductBase : IDiscordContent
    {
        public string Id { get; }
        public string Title { get; }
        public string Url { get; }
        public LiverGroupDetail Shop { get; }
        public string Category { get; }
        public IReadOnlyList<ProductItem> Items { get; }
        public IReadOnlyList<LiverDetail> Livers { get; }
        public bool IsExistedStart { get; }
        public DateTime StartDate { get; }
        public bool IsExistedEnd { get; }
        public DateTime EndDate { get; }
        public bool IsOnSale
        { get { return StartDate <= DateTime.Now && (!IsExistedEnd || EndDate > DateTime.Now); } }

        protected private abstract string ExceptUrl { get; }

        [JsonIgnore]
        public virtual IReadOnlyDictionary<string, string> ContentFormat => new Dictionary<string, string>()
            {
                { "Date", IsExistedEnd ? (this as IDiscordContent).ConvertDuringDateTime(StartDate, EndDate)
                    : (this as IDiscordContent).ConvertDuringDateTime(StartDate) },
                { "Id", Id }, { "Title", Title }, { "Category", Category }, { "URL", Url }
            };
        [JsonIgnore]
        public virtual IReadOnlyDictionary<string, IEnumerable<object>> ContentFormatEnumerator
            => new Dictionary<string, IEnumerable<object>>()
            {
                { "Livers", Livers }, { "ItemsNP", Items.Select(i => $"{i.Name} \\{i.Price}") },
                { "ItemsN", Items.Select(i => i.Name) },
            };

        public ProductBase(string title, string url, LiverGroupDetail shop, string category,
            List<(string, int)> items, List<LiverDetail> livers, DateTime? start, DateTime? end)
        {
            Title = title;
            var index = url.IndexOf('?');
            if (index != -1) url = url.Substring(0, index);
            Url = url;
            Id = url.Replace(ExceptUrl, "");
            Shop = shop;
            Category = category;
            (Items, Livers) = InspectItems(shop, items, livers);
            IsExistedStart = start != null;
            if (IsExistedStart) StartDate = (DateTime)start;
            else StartDate = DateTime.Now;
            IsExistedEnd = end != null;
            if (IsExistedEnd) EndDate = (DateTime)end;
        }

        public string SaleDateTime()
        {
            string text = null;
            var now = DateTime.Now;
            if (!IsOnSale)
            {
                if (IsExistedStart && IsExistedEnd)
                {
                    if (StartDate.Year != EndDate.Year)
                        text = $"{StartDate:yyyy/MM/dd HH:mm} ～ {EndDate:yyyy/MM/dd HH:mm}";
                    else text = $"{StartDate:MM/dd HH:mm} ～ {EndDate:MM/dd HH:mm}";
                }
                else if (IsExistedStart)
                {
                    if (StartDate.Year != now.Year) text = $"{StartDate:yyyy/MM/dd HH:mm} ～";
                    else text = $"{StartDate:MM/dd HH:mm} ～";
                }
            }
            else if (IsExistedEnd)
            {
                if (now.Year != EndDate.Year) text = $"～ {EndDate:yyyy/MM/dd HH:mm}";
                else text = $"～ {EndDate:MM/dd HH:mm}";
            }
            return text;
        }

        private protected static (List<ProductItem>, List<LiverDetail>) InspectItems(LiverGroupDetail shop, List<(string, int)> items, List<LiverDetail> livers = null)
        {
            var res = (new List<ProductItem>(), livers ?? new List<LiverDetail>());
            foreach(var item in items)
            {
                var name = item.Item1;
                var price = item.Item2;
                if (livers != null && livers.Count == 1)
                {
                    res.Item1.Add(new(name, price, livers));
                }
                else
                {
                    var list = DetectLiver(shop, name, livers);
                    res.Item1.Add(new(name, price, list));
                    res.Item2.AddRange(list);
                }
            }
            res.Item2 = new(res.Item2.Distinct());
            return res;
        }
        private protected static List<LiverDetail> DetectLiver(LiverGroupDetail shop, string content, List<LiverDetail> livers = null)
        {
            bool b = livers == null || livers.Count == 0;
            if (b) livers = new(LiverData.GetLiversList(shop));
            List<LiverDetail> res = null;
            foreach (var liver in livers)
            {
                if (content.Contains(liver.Name))
                {
                    if (res == null) res = new();
                    res.Add(liver);
                }
            }
            if (res == null && !b) res = livers;
            return res;
        }

        public virtual string GetDiscordContent()
        {
            var format = "新しい商品ページが公開されました\n{Title}\n{Date}\n{URL}\n参加ライバー:{Livers: / }\n{ItemNP:\n}";
            return (this as IDiscordContent).ConvertContent(format);
        }

        public static bool operator ==(ProductBase a, ProductBase b) => a.Equals(b);
        public static bool operator !=(ProductBase a, ProductBase b) => !(a == b);
        public override bool Equals(object obj)
        {
            return obj is ProductBase p && p.Url == Url;
        }
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

    }

    public struct ProductItem
    {
        public string Name { get; }
        public int Price { get; }
        public IReadOnlyList<LiverDetail> Livers { get; }

        public ProductItem(string name, int price, List<LiverDetail> livers)
        {
            Name = name;
            Price = price;
            Livers = livers;
        }
    }
}

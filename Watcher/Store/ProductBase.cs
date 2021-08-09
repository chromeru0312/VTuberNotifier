using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using VTuberNotifier.Notification;
using VTuberNotifier.Liver;

namespace VTuberNotifier.Watcher.Store
{
    [Serializable]
    public abstract class ProductBase : INotificationContent
    {
        public string Id { get; }
        public string Title { get; }
        public string Url { get; }
        public TextContent Description { get; }
        public LiverGroupDetail Shop { get; }
        public string Category { get; }
        public IReadOnlyList<ProductItem> Items { get; }
        public IReadOnlyList<LiverDetail> Livers { get; }
        public DateTime StartDate { get; }
        public bool IsExistedEnd { get; }
        public DateTime EndDate { get; }
        public bool IsOnSale { get; protected private set; }

        protected private abstract string ExceptUrl { get; }

        [JsonIgnore]
        public virtual IReadOnlyDictionary<string, string> ContentFormat => new Dictionary<string, string>()
            {
                { "Date", IsExistedEnd ? (this as INotificationContent).ConvertDuringDateTime(StartDate, EndDate)
                    : (this as INotificationContent).ConvertDuringDateTime(StartDate) },
                { "Id", Id }, { "Title", Title }, { "Category", Category }, { "URL", Url }
            };
        [JsonIgnore]
        public virtual IReadOnlyDictionary<string, IEnumerable<object>> ContentFormatEnumerator
            => new Dictionary<string, IEnumerable<object>>()
            {
                { "Livers", Livers }
            };
        [JsonIgnore]
        public IReadOnlyDictionary<string, Func<LiverDetail, IEnumerable<string>>> ContentFormatEnumeratorFunc
            => new Dictionary<string, Func<LiverDetail, IEnumerable<string>>>()
            {
                { "ItemsN", GetItemsN }, { "ItemsNP", GetItemsNP }
            };
        private IEnumerable<string> GetItems(LiverDetail liver, bool price)
        {
            var list = new List<string>();
            foreach (var i in Items)
            {
                if (!i.Livers.Contains(liver)) continue;
                var str = i.Name;
                if (price) str += $"  \\{i.Price}";
                list.Add(str);
            }
            if (list.Count == 0)
            {
                if (price) list = new(Items.Select(i => $"{i.Name}  \\{i.Price}"));
                else list = new(Items.Select(i => i.Name));
            }
            return list;
        }
        private IEnumerable<string> GetItemsN(LiverDetail liver) => GetItems(liver, false);
        private IEnumerable<string> GetItemsNP(LiverDetail liver) => GetItems(liver, true);

        protected private ProductBase(string id, string url, string title, TextContent description, LiverGroupDetail shop,
            string category, List<ProductItem> items, List<LiverDetail> livers, DateTime? start, DateTime? end)
            : this(id, url, title, description, shop, category, start, end)
        {
            Items = items;
            Livers = livers;
        }
        protected private ProductBase(string id, string url, string title, TextContent description, LiverGroupDetail shop,
            string category, List<(string, int)> items, List<LiverDetail> livers, DateTime? start, DateTime? end)
            : this(id, url, title, description, shop, category, start, end)
        {
            (Items, Livers) = InspectItems(shop, items, livers);
        }
        private ProductBase(string id, string url, string title, TextContent description, LiverGroupDetail shop,
            string category, DateTime? start, DateTime? end)
        {
            var index = url.IndexOf('?');
            if (index != -1) url = url.Substring(0, index);
            Id = id ?? url.Replace(ExceptUrl, "");
            Url = url;
            Title = title;
            Description = description;
            Shop = shop;
            Category = category;
            if (start != null) StartDate = (DateTime)start;
            else StartDate = DateTime.Now;
            IsExistedEnd = end != null;
            if (IsExistedEnd) EndDate = (DateTime)end;
            IsOnSale = StartDate <= DateTime.Now && (!IsExistedEnd || EndDate > DateTime.Now);
        }

        private protected static (List<ProductItem>, List<LiverDetail>) InspectItems
            (LiverGroupDetail shop, List<(string, int)> items, List<LiverDetail> livers = null)
        {
            bool b = false;
            if (livers == null) b = true;
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
                    if(b) res.Item2.AddRange(list);
                    res.Item2 = new(res.Item2.Distinct());
                }
            }
            res.Item2 = new(res.Item2.Distinct());
            return res;
        }
        private protected static List<LiverDetail> DetectLiver(LiverGroupDetail shop, string content, List<LiverDetail> livers = null)
        {
            bool b = livers == null || livers.Count == 0;
            if (b) livers = new(LiverData.GetLiversList(shop));
            var res = new List<LiverDetail>();
            foreach (var liver in livers)
            {
                if (content.Contains(liver.Name)) res.Add(liver);
            }
            if (res.Count == 0 && !b) res = livers;
            return res;
        }
        public string TrimUrl(string url)
        {
            var index = url.IndexOf('?');
            if (index != -1) url = url[0..index];
            return url;
        }

        public override bool Equals(object obj)
        {
            return obj is ProductBase p && p.Id == Id;
        }
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public abstract class ProductConverter<T> : JsonConverter<T> where T : ProductBase
        {
            public (string id, string url, string title, TextContent description, LiverGroupDetail shop, string category,
            List<ProductItem> items, List<LiverDetail> livers, DateTime? start, DateTime? end)
                ReadBase(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                var id = reader.GetNextValue<string>(options);
                var url = reader.GetNextValue<string>(options);
                var title = reader.GetNextValue<string>(options);
                var desc = reader.GetNextValue<TextContent>(options);
                var gid = reader.GetNextValue<int>(options);
                var shop = LiverGroup.GroupList.FirstOrDefault(g => g.Id == gid * 10000);
                var cate = reader.GetNextValue<string>(options);
                var items = reader.GetNextValue<List<ProductItem>>(options);
                var livers = reader.GetNextValue<List<LiverDetail>>(options);
                var sd = reader.GetNextValue<DateTime>(options);
                
                var eds = reader.GetNextValue<string>(options);
                DateTime? ed = null;
                if (eds != null) ed = DateTime.Parse(eds, Settings.Data.Culture);
                return (id, url, title, desc, shop, cate, items, livers, sd, ed);
            }

            public void WriteBase(Utf8JsonWriter writer, T pb, JsonSerializerOptions options)
            {
                writer.WriteString("Id", pb.Id);
                writer.WriteString("Url", pb.Url);
                writer.WriteString("Title", pb.Title);
                writer.WriteValue("Description", pb.Description, options);
                writer.WriteNumber("Shop", pb.Shop.Id / 10000);
                writer.WriteString("Category", pb.Category);
                writer.WriteValue("Items", pb.Items, options);
                writer.WriteValue("Livers", pb.Livers, options);
                writer.WriteString("StartDate", pb.StartDate.ToString("g"));
                if (pb.IsExistedEnd) writer.WriteString("EndDate", pb.EndDate);
                else writer.WriteNull("EndDate");
            }
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

        public class ProductItemConverter : JsonConverter<ProductItem>
        {
            public override ProductItem Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                reader.CheckStartToken();

                var name = reader.GetNextValue<string>(options);
                var price = reader.GetNextValue<int>(options);
                var livers = reader.GetNextValue<List<LiverDetail>>(options);

                reader.CheckEndToken();
                return new(name, price, livers);
            }

            public override void Write(Utf8JsonWriter writer, ProductItem value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                writer.WriteString("Name", value.Name);
                writer.WriteNumber("Price", value.Price);
                writer.WriteValue("Livers", value.Livers, options);

                writer.WriteEndObject();
            }
        }
    }
}
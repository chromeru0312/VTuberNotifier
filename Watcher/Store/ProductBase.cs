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

        public ProductBase(string title, string url, LiverGroupDetail shop, string category,
            List<(string, int)> items, List<LiverDetail> livers, DateTime? start, DateTime? end)
            : this(title, url, shop, category, start, end)
        {
            var index = url.IndexOf('?');
            if (index != -1) url = url.Substring(0, index);
            Id = url.Replace(ExceptUrl, "");
            (Items, Livers) = InspectItems(shop, items, livers);
        }
        protected private ProductBase(string id, string title, string url, LiverGroupDetail shop, string category,
            List<ProductItem> items, List<LiverDetail> livers, DateTime? start, DateTime? end)
            : this(title, url, shop, category, start, end)
        {
            Id = id;
            Items = items;
            Livers = livers;
        }
        protected private ProductBase(string id, string title, string url, LiverGroupDetail shop, string category,
            List<(string, int)> items, List<LiverDetail> livers, DateTime? start, DateTime? end)
            : this(title, url, shop, category, start, end)
        {
            Id = id;
            (Items, Livers) = InspectItems(shop, items, livers);
        }
        private ProductBase(string title, string url, LiverGroupDetail shop, string category, DateTime? start, DateTime? end)
        {
            Title = title;
            var index = url.IndexOf('?');
            if (index != -1) url = url.Substring(0, index);
            Url = url;
            Shop = shop;
            Category = category;
            if (start != null) StartDate = (DateTime)start;
            else StartDate = DateTime.Now;
            IsExistedEnd = end != null;
            if (IsExistedEnd) EndDate = (DateTime)end;
            IsOnSale = StartDate <= DateTime.Now && (!IsExistedEnd || EndDate > DateTime.Now);
        }

        private protected static (List<ProductItem>, List<LiverDetail>) InspectItems(LiverGroupDetail shop, List<(string, int)> items, List<LiverDetail> livers = null)
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
            public (string id, string title, string url, LiverGroupDetail shop, string category,
            List<ProductItem> items, List<LiverDetail> livers, DateTime? start, DateTime? end)
                ReadBase(ref Utf8JsonReader reader, Type _, JsonSerializerOptions options)
            {
                reader.Read();
                reader.Read();
                var id = reader.GetString();
                reader.Read();
                reader.Read();
                var title = reader.GetString();
                reader.Read();
                reader.Read();
                var url = reader.GetString();
                reader.Read();
                reader.Read();
                var gid = reader.GetInt32();
                var shop = LiverGroup.GroupList.FirstOrDefault(g => g.Id == gid * 10000);
                reader.Read();
                reader.Read();
                var cate = reader.GetString();
                reader.Read();
                reader.Read();
                if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException();
                reader.Read();
                if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();
                var items = new List<ProductItem>();
                while (true)
                {
                    reader.Read();
                    reader.Read();
                    var name = reader.GetString();
                    reader.Read();
                    reader.Read();
                    var price = reader.GetInt32();
                    reader.Read();
                    reader.Read();
                    var ilivers = JsonSerializer.Deserialize<List<LiverDetail>>(ref reader, options);
                    items.Add(new(name, price, ilivers));
                    reader.Read();
                    if (reader.TokenType != JsonTokenType.EndObject) throw new JsonException();
                    reader.Read();
                    if (reader.TokenType == JsonTokenType.EndArray) break;
                    else if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();
                }
                reader.Read();
                reader.Read();
                var livers = JsonSerializer.Deserialize<List<LiverDetail>>(ref reader, options);
                reader.Read();
                reader.Read();
                var sd = DateTime.Parse(reader.GetString(), Settings.Data.Culture);
                reader.Read();
                reader.Read();
                var eds = reader.GetString();
                DateTime? ed = null;
                if (eds != null) ed = DateTime.Parse(eds, Settings.Data.Culture);
                return (id, title, url, shop, cate, items, livers, sd, ed);
            }

            public void WriteBase(Utf8JsonWriter writer, T pb, JsonSerializerOptions options)
            {
                writer.WriteString("Id", pb.Id);
                writer.WriteString("Title", pb.Title);
                writer.WriteString("Url", pb.Url);
                writer.WriteNumber("Shop", pb.Shop.Id / 10000);
                writer.WriteString("Category", pb.Category);

                writer.WriteStartArray("Items");
                foreach (var item in pb.Items)
                {
                    writer.WriteStartObject();
                    writer.WriteString("Name", item.Name);
                    writer.WriteNumber("Price", item.Price);
                    writer.WritePropertyName("Livers");
                    JsonSerializer.Serialize(writer, item.Livers, options);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();

                writer.WritePropertyName("Livers");
                JsonSerializer.Serialize(writer, pb.Livers, options);

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
    }
}

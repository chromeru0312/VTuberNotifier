using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using VTuberNotifier.Liver;

namespace VTuberNotifier.Watcher.Store
{
    interface IProductTag
    {
        public IReadOnlyList<string> Tags { get; }

        protected private static List<LiverDetail> LiverTag(LiverGroupDetail shop, List<string> tags)
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

        public static List<string> ReadTags(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            reader.Read();
            reader.Read();
            var tags = JsonSerializer.Deserialize<List<string>>(ref reader, options);
            return tags;
        }
        protected private void WriteTags(Utf8JsonWriter writer, JsonSerializerOptions options)
        {
            WriteTags(writer, options);
        }
        public static void WriteTags(Utf8JsonWriter writer, IProductTag value, JsonSerializerOptions options)
        {
            writer.WritePropertyName("Tags");
            JsonSerializer.Serialize(writer, value.Tags, options);
        }
    }
}

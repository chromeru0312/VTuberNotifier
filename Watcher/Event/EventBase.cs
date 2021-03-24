using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using VTuberNotifier.Notification;
using VTuberNotifier.Liver;

namespace VTuberNotifier.Watcher.Event
{
    public abstract class EventBase<T> : IEventBase where T : INotificationContent
    {
        public string EventTypeName { get; }
        public T Item { get; }
        public DateTime CreatedTime { get; }

        protected private Dictionary<string, string> ContentFormat;
        protected private Dictionary<string, IEnumerable<object>> ContentFormatEnumerator;
        protected private Dictionary<string, Func<LiverDetail, IEnumerable<string>>> ContentFormatEnumeratorFunc;

        public EventBase(string evt_name, T value) : this(evt_name, value, DateTime.Now) { }
        protected private EventBase(string evt_name, T value, DateTime dt)
        {
            EventTypeName = evt_name;
            Item = value;
            CreatedTime = dt;
            ContentFormat = new(Item.ContentFormat);
            ContentFormatEnumerator = new(Item.ContentFormatEnumerator);
            ContentFormatEnumeratorFunc = new(Item.ContentFormatEnumeratorFunc);
        }

        public abstract string GetDiscordContent(LiverDetail liver);
        public string ConvertContent(string format, LiverDetail liver)
        {
            foreach (Match match in Regex.Matches(format, "{.+}"))
            {
                var tag = match.Value[1..^1].Split(':');
                if (tag.Length > 2) for (int i = 2; i < tag.Length; i++) tag[1] += ':' + tag[i];

                if (ContentFormat.ContainsKey(tag[0]) && tag.Length == 1)
                    format = format.Replace(match.Value, ContentFormat[tag[0]]);
                else if (ContentFormatEnumerator.ContainsKey(tag[0]) && tag.Length > 1)
                    format = format.Replace(match.Value, string.Join(tag[1], ContentFormatEnumerator[tag[0]].Select(o => o.ToString())));
                else if (ContentFormatEnumeratorFunc.ContainsKey(tag[0]) && tag.Length > 1)
                    format = format.Replace(match.Value, string.Join(tag[1], ContentFormatEnumeratorFunc[tag[0]].Invoke(liver)));
                else continue;
            }
            format = format.Replace("\\n", "\n");
            return format;
        }


        public abstract class EventConverter : JsonConverter<EventBase<T>>
        {
            public override EventBase<T> Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();
                var (_, item, date) = ReadBase(ref reader, type, options);
                reader.Read();
                if (reader.TokenType == JsonTokenType.EndObject) return ResultEvent(item, date);
                throw new JsonException();
            }
            protected private static (string evt_name, T value, DateTime dt) ReadBase(ref Utf8JsonReader reader, Type _, JsonSerializerOptions options)
            {
                reader.Read();
                reader.Read();
                var type = reader.GetString();
                reader.Read();
                reader.Read();
                var date = DateTime.Parse(reader.GetString());
                reader.Read();
                reader.Read();
                var item = JsonSerializer.Deserialize<T>(ref reader, options);
                return (type, item, date);
            }
            protected private abstract EventBase<T> ResultEvent(T value, DateTime dt);

            public override void Write(Utf8JsonWriter writer, EventBase<T> value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                WriteBase(writer, value, options);
                writer.WriteEndObject();
            }
            protected private static void WriteBase(Utf8JsonWriter writer, EventBase<T> value, JsonSerializerOptions options)
            {
                writer.WriteString("EventType", value.EventTypeName);
                writer.WriteString("CreatedDate", value.CreatedTime.ToString("g"));
                writer.WritePropertyName("Item");
                JsonSerializer.Serialize(writer, value.Item, options);
            }
        }
    }

    public interface IEventBase
    {
        public string GetDiscordContent(LiverDetail liver);
        public string ConvertContent(string format, LiverDetail liver);
    }
}

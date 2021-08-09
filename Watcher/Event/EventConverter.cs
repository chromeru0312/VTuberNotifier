using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using VTuberNotifier.Notification;

namespace VTuberNotifier.Watcher.Event
{
    public class EventConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type type)
        {
            if (!type.IsGenericType) return false;
            var generic = type.GetGenericArguments()[0];
            if (generic.GetInterface("VTuberNotifier.Notification.INotificationContent") == null)
                return false;
            return type.Equals(typeof(EventBase<>).MakeGenericType(generic));
        }

        public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
        {
            var converter = (JsonConverter)Activator.CreateInstance(
                typeof(EventConverterInner<>).MakeGenericType(type.GetGenericArguments()[0]));
            return converter;
        }

        private class EventConverterInner<T> : JsonConverter<EventBase<T>> where T : INotificationContent
        {
            public EventConverterInner() { }

            public override EventBase<T> Read(ref Utf8JsonReader reader, Type _, JsonSerializerOptions options)
            {
                return null;
            }

            public override void Write(Utf8JsonWriter writer, EventBase<T> value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                writer.WriteString("EventType", value.EventTypeName);
                writer.WriteString("CreatedDate", value.CreatedTime);
                writer.WriteValue("Item", value.Item, options);
                writer.WriteValue("OldItem", value.OldItem, options);
                writer.WriteValue("InnerEvents", value.EventsByLiver.Select(
                    p => new KeyValuePair<int, IEnumerable<string>>(p.Key.Id, p.Value.Select(e => e.EventTypeName))), options);

                writer.WriteEndObject();
            }
        }
    }
}
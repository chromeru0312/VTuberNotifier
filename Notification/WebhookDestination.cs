using System;
using System.Collections.Generic;
using System.Text.Json;

namespace VTuberNotifier.Notification
{
    public class WebhookDestination : NotificationAddress, IEquatable<WebhookDestination>
    {
        public string Url { get; }

        public WebhookDestination(string url) : this(url, new()) { }
        private WebhookDestination(string url, Dictionary<Type, string> dic) : base(dic)
        {
            Url = url;
        }

        public override bool Equals(object obj)
        {
            return obj is WebhookDestination wd && Equals(wd);
        }
        public bool Equals(WebhookDestination other)
        {
            return Url == other.Url;
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(Url);
        }

        public class WebhookDestinationConverter : NotificationAddressConverter<WebhookDestination>
        {
            public override WebhookDestination Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

                reader.Read();
                reader.Read();
                var url = reader.GetString();
                var dic = ReadBase(ref reader);

                reader.Read();
                if (reader.TokenType == JsonTokenType.EndObject) return new(url, dic);
                throw new JsonException();
            }

            public override void Write(Utf8JsonWriter writer, WebhookDestination value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                writer.WriteString("Url", value.Url);
                WriteBase(writer, value);

                writer.WriteEndObject();
            }
        }
    }
}

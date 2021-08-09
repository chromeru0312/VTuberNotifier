using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VTuberNotifier.Notification
{
    [Serializable]
    [JsonConverter(typeof(WebhookDestinationConverter))]
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
                reader.CheckStartToken();

                var url = reader.GetNextValue<string>(options);
                var dic = ReadBase(ref reader, options);

                reader.CheckEndToken(); 
                return new(url, dic);
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
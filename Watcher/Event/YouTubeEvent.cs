using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using VTuberNotifier.Liver;
using VTuberNotifier.Watcher.Feed;

namespace VTuberNotifier.Watcher.Event
{
    public abstract class YouTubeEvent : EventBase<YouTubeItem>
    {
        public YouTubeEvent(string evt_name, YouTubeItem value) : base(evt_name, value) { }
        protected private YouTubeEvent(string evt_name, YouTubeItem value, DateTime dt)
             : base(evt_name, value, dt) { }
    }

    [JsonConverter(typeof(YouTubeNewLiveEventConverter))]
    public class YouTubeNewLiveEvent : YouTubeEvent
    {
        public YouTubeNewLiveEvent(YouTubeItem value) : base(nameof(YouTubeNewLiveEvent), value) { }
        protected private YouTubeNewLiveEvent(YouTubeItem value, DateTime dt)
            : base(nameof(YouTubeNewLiveEvent), value, dt) { }

        public override string GetDiscordContent(LiverDetail liver)
        {
            var format = "配信待機所が作成されました\n{Title}\n参加ライバー:{Livers: / }\n{Date}\n{URL}";
            return ConvertContent(format, liver);
        }

        public class YouTubeNewLiveEventConverter : EventConverter
        {
            private protected override EventBase<YouTubeItem> ResultEvent(YouTubeItem value, DateTime dt)
                => new YouTubeNewLiveEvent(value, dt);
        }
    }
    [JsonConverter(typeof(YouTubeNewPremireEventConverter))]
    public class YouTubeNewPremireEvent : YouTubeEvent
    {
        public YouTubeNewPremireEvent(YouTubeItem value) : base(nameof(YouTubeNewPremireEvent), value) { }
        protected private YouTubeNewPremireEvent(YouTubeItem value, DateTime dt)
            : base(nameof(YouTubeNewPremireEvent), value, dt) { }

        public override string GetDiscordContent(LiverDetail liver)
        {
            var format = "プレミア公開待機所が作成されました\n{Title}\n参加ライバー:{Livers: / }\n{Date}\n{URL}";
            return ConvertContent(format, liver);
        }

        public class YouTubeNewPremireEventConverter : EventConverter
        {
            private protected override EventBase<YouTubeItem> ResultEvent(YouTubeItem value, DateTime dt)
                => new YouTubeNewPremireEvent(value, dt);
        }
    }
    [JsonConverter(typeof(YouTubeNewVideoEventConverter))]
    public class YouTubeNewVideoEvent : YouTubeEvent
    {
        public YouTubeNewVideoEvent(YouTubeItem value) : base(nameof(YouTubeNewVideoEvent), value) { }
        protected private YouTubeNewVideoEvent(YouTubeItem value, DateTime dt)
            : base(nameof(YouTubeNewVideoEvent), value, dt) { }

        public override string GetDiscordContent(LiverDetail liver)
        {
            var format = "新規動画が投稿されました\n{Title}\n参加ライバー:{Livers: / }\n{URL}";
            return ConvertContent(format, liver);
        }

        public class YouTubeNewVideoEventConverter : EventConverter
        {
            private protected override EventBase<YouTubeItem> ResultEvent(YouTubeItem value, DateTime dt)
                => new YouTubeNewVideoEvent(value, dt);
        }
    }
    [JsonConverter(typeof(YouTubeStartLiveEventConverter))]
    public class YouTubeStartLiveEvent : YouTubeEvent
    {
        public YouTubeStartLiveEvent(YouTubeItem value) : base(nameof(YouTubeStartLiveEvent), value) { }
        protected private YouTubeStartLiveEvent(YouTubeItem value, DateTime dt)
            : base(nameof(YouTubeStartLiveEvent), value, dt) { }

        public override string GetDiscordContent(LiverDetail liver)
        {
            var format = "配信が開始されました\n{Title}\n参加ライバー:{Livers: / }\n{URL}";
            return ConvertContent(format, liver);
        }

        public class YouTubeStartLiveEventConverter : EventConverter
        {
            private protected override EventBase<YouTubeItem> ResultEvent(YouTubeItem value, DateTime dt)
                => new YouTubeStartLiveEvent(value, dt);
        }
    }
    [JsonConverter(typeof(YouTubeChangeInfoEventConverter))]
    public class YouTubeChangeInfoEvent : YouTubeEvent
    {
        public YouTubeItem OldItem { get; }

        public YouTubeChangeInfoEvent(YouTubeItem old, YouTubeItem latest) : this(old, latest, DateTime.Now) { }
        protected private YouTubeChangeInfoEvent(YouTubeItem old, YouTubeItem latest, DateTime dt)
            : base(nameof(YouTubeChangeInfoEvent), latest, dt)
        {
            OldItem = old;

            string format;
            if (old.LiveStartDate.Year != latest.LiveStartDate.Year) format = "yyyy/MM/dd HH:mm";
            else format = "MM/dd HH:mm";

            var dic = ContentFormat;
            if (ContentFormat.ContainsKey("Date")) dic.Remove("Date");
            dic.Add("Date", latest.LiveStartDate.ToString(format));
            dic.Add("OldDate", old.LiveStartDate.ToString(format));
            dic.Add("ChangeDate", $"{ContentFormat["OldDate"]} → {ContentFormat["Date"]}");
            ContentFormat = dic;
        }

        public override string GetDiscordContent(LiverDetail liver)
        {
            return ConvertContent("配信開始時刻が変更されました\n{Title}\n{ChangeDate}\n{URL}", liver);
        }

        public class YouTubeChangeInfoEventConverter : EventConverter
        {
            public override YouTubeChangeInfoEvent Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();
                var (_, item, date) = ReadBase(ref reader, type, options);
                reader.Read();
                reader.Read();
                var old = JsonSerializer.Deserialize<YouTubeItem>(ref reader, options);

                reader.Read();
                if (reader.TokenType == JsonTokenType.EndObject) return new(old, item, date);
                throw new JsonException();
            }
            public override void Write(Utf8JsonWriter writer, EventBase<YouTubeItem> value, JsonSerializerOptions options)
            {
                YouTubeChangeInfoEvent evt = (YouTubeChangeInfoEvent)value;
                writer.WriteStartObject();
                WriteBase(writer, value, options);
                writer.WritePropertyName("OldItem");
                JsonSerializer.Serialize(writer, evt.OldItem, options);
                writer.WriteEndObject();
            }
            private protected override EventBase<YouTubeItem> ResultEvent(YouTubeItem value, DateTime dt)
               => null;
        }
    }
    [JsonConverter(typeof(YouTubeDeleteLiveEventConverter))]
    public class YouTubeDeleteLiveEvent : YouTubeEvent
    {
        public YouTubeDeleteLiveEvent(YouTubeItem value) : base(nameof(YouTubeDeleteLiveEvent), value) { }
        protected private YouTubeDeleteLiveEvent(YouTubeItem value, DateTime dt)
            : base(nameof(YouTubeDeleteLiveEvent), value, dt) { }

        public override string GetDiscordContent(LiverDetail liver)
        {
            return ConvertContent("配信待機所が削除されました\n{Title}\n{URL}", liver);
        }

        public class YouTubeDeleteLiveEventConverter : EventConverter
        {
            private protected override EventBase<YouTubeItem> ResultEvent(YouTubeItem value, DateTime dt)
                => new YouTubeDeleteLiveEvent(value, dt);
        }
    }
}

using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using VTuberNotifier.Liver;
using VTuberNotifier.Watcher.Feed;
using static VTuberNotifier.Watcher.Feed.YouTubeItem;

namespace VTuberNotifier.Watcher.Event
{
    public abstract class YouTubeEvent : EventBase<YouTubeItem>
    {
        public YouTubeEvent(YouTubeItem value) : this(value, DateTime.Now) { }
        protected private YouTubeEvent(YouTubeItem value, DateTime dt) : base(value, dt) { }
    }

    public abstract class YouTubeNewEvent : YouTubeEvent
    {
        protected private YouTubeNewEvent(YouTubeItem value, DateTime dt) : base(value, dt) { }

        public static YouTubeNewEvent CreateEvent(YouTubeItem item)
        {
            YouTubeNewEvent evt = item.Mode switch
            {
                YouTubeMode.Video => new VideoEvent(item),
                YouTubeMode.Premire => new PremireEvent(item),
                YouTubeMode.Live => new LiveEvent(item),
                _ => throw new InvalidCastException()
            };
            return evt;
        }

        [JsonConverter(typeof(YouTubeNewLiveEventConverter))]
        public class LiveEvent : YouTubeNewEvent
        {
            [JsonIgnore]
            public override string FormatContent => "配信待機所が作成されました\n{Title}\n参加ライバー : {Livers: / }\n{Date}\n{URL}";

            public LiveEvent(YouTubeItem value) : this(value, DateTime.Now) { }
            protected private LiveEvent(YouTubeItem value, DateTime dt) : base(value, dt) { }

            public class YouTubeNewLiveEventConverter : EventConverter
            {
                private protected override EventBase<YouTubeItem> ResultEvent(YouTubeItem value, DateTime dt)
                    => new LiveEvent(value, dt);
            }
        }
        [JsonConverter(typeof(YouTubeNewPremireEventConverter))]
        public class PremireEvent : YouTubeNewEvent
        {
            [JsonIgnore]
            public override string FormatContent => "プレミア公開待機所が作成されました\n{Title}\n参加ライバー : {Livers: / }\n{Date}\n{URL}";

            public PremireEvent(YouTubeItem value) : this(value, DateTime.Now) { }
            protected private PremireEvent(YouTubeItem value, DateTime dt) : base(value, dt) { }

            public class YouTubeNewPremireEventConverter : EventConverter
            {
                private protected override EventBase<YouTubeItem> ResultEvent(YouTubeItem value, DateTime dt)
                    => new PremireEvent(value, dt);
            }
        }
        [JsonConverter(typeof(YouTubeNewVideoEventConverter))]
        public class VideoEvent : YouTubeNewEvent
        {
            [JsonIgnore]
            public override string FormatContent => "新規動画が投稿されました\n{Title}\n参加ライバー : {Livers: / }\n{URL}";

            public VideoEvent(YouTubeItem value) : this(value, DateTime.Now) { }
            protected private VideoEvent(YouTubeItem value, DateTime dt) : base(value, dt) { }

            public class YouTubeNewVideoEventConverter : EventConverter
            {
                private protected override EventBase<YouTubeItem> ResultEvent(YouTubeItem value, DateTime dt)
                    => new VideoEvent(value, dt);
            }
        }
    }
    [JsonConverter(typeof(YouTubeStartLiveEventConverter))]
    public class YouTubeStartLiveEvent : YouTubeEvent
    {
        [JsonIgnore]
        public override string FormatContent => "配信が開始されました\n{Title}\n参加ライバー : {Livers: / }\n{URL}";

        public YouTubeStartLiveEvent(YouTubeItem value) : this(value, DateTime.Now) { }
        protected private YouTubeStartLiveEvent(YouTubeItem value, DateTime dt) : base(value, dt) { }

        public class YouTubeStartLiveEventConverter : EventConverter
        {
            private protected override EventBase<YouTubeItem> ResultEvent(YouTubeItem value, DateTime dt)
                => new YouTubeStartLiveEvent(value, dt);
        }
    }
    [JsonConverter(typeof(YouTubeChangeEventConverter))]
    public abstract class YouTubeChangeEvent : YouTubeEvent
    {
        public YouTubeItem OldItem { get; }

        public YouTubeChangeEvent(YouTubeItem old, YouTubeItem latest) : this(old, latest, DateTime.Now) { }
        protected private YouTubeChangeEvent(YouTubeItem old, YouTubeItem latest, DateTime dt) : base(latest, dt)
        {
            OldItem = old;
        }

        public static YouTubeChangeEvent CreateEvent(YouTubeItem old, YouTubeItem latest)
        {
            YouTubeChangeEvent evt = new OtherEvent(old, latest);
            if (latest.LiveStartDate != old.LiveStartDate && latest.LiveStartDate != DateTime.MinValue)
                evt = new DateEvent(old, latest);
            if (!latest.Livers.SequenceEqual(old.Livers))
                evt = evt is DateEvent ? new LiversAndDateEvent(old, latest)
                    : new LiverEvent(old, latest);
            return evt;
        }

        protected private void AddDateContentFormats()
        {
            string format = OldItem.LiveStartDate.Year != Item.LiveStartDate.Year ? "yyyy/MM/dd HH:mm" : "MM/dd HH:mm";

            var dic = ContentFormat;
            if (ContentFormat.ContainsKey("Date")) dic.Remove("Date");
            dic.Add("Date", Item.LiveStartDate.ToString(format));
            dic.Add("OldDate", OldItem.LiveStartDate.ToString(format));
            dic.Add("ChangeDate", $"{ContentFormat["OldDate"]} → {ContentFormat["Date"]}");
            ContentFormat = dic;
        }
        protected private string GetLiversDiscordContent(LiverDetail liver)
        {
            string str;
            if (Item.Livers.Except(OldItem.Livers).Contains(liver))
                str = YouTubeNewEvent.CreateEvent(Item).FormatContent;
            else if (Item.Livers.Except(OldItem.Livers).Contains(liver))
                str = new YouTubeDeleteLiveEvent(Item).FormatContent;
            else str = FormatContent;

            return ConvertContent(str, liver);
        }

        [JsonConverter(typeof(YouTubeChangeLiversEventConverter))]
        public class LiverEvent : YouTubeChangeEvent
        {
            [JsonIgnore]
            public override string FormatContent => "参加ライバーが変更されました\n{Title}\n参加ライバー : {Livers: / }\n{URL}";
            public LiverEvent(YouTubeItem old, YouTubeItem latest) : this(old, latest, DateTime.Now) { }
            protected private LiverEvent(YouTubeItem old, YouTubeItem latest, DateTime dt) : base(old, latest, dt) { }
            public override string GetDiscordContent(LiverDetail liver) => GetLiversDiscordContent(liver);

            public class YouTubeChangeLiversEventConverter : YouTubeChangeEventConverter
            {
                private protected override YouTubeChangeEvent ResultEvent(YouTubeItem old, YouTubeItem latest, DateTime dt)
                    => new LiverEvent(old, latest, dt);
            }
        }
        [JsonConverter(typeof(YouTubeChangeDateEventConverter))]
        public class DateEvent : YouTubeChangeEvent
        {
            [JsonIgnore]
            public override string FormatContent => "配信開始時刻が変更されました\n{Title}\n{ChangeDate}\n{URL}";
            public DateEvent(YouTubeItem old, YouTubeItem latest) : this(old, latest, DateTime.Now) { }
            protected private DateEvent(YouTubeItem old, YouTubeItem latest, DateTime dt) : base(old, latest, dt)
            {
                AddDateContentFormats();
            }

            public class YouTubeChangeDateEventConverter : YouTubeChangeEventConverter
            {
                private protected override YouTubeChangeEvent ResultEvent(YouTubeItem old, YouTubeItem latest, DateTime dt)
                    => new DateEvent(old, latest, dt);
            }
        }
        [JsonConverter(typeof(YouTubeChangeLiversAndDateEventConverter))]
        public class LiversAndDateEvent : YouTubeChangeEvent
        {
            [JsonIgnore]
            public override string FormatContent
                => "参加ライバー・配信開始時刻が変更されました\n{Title}\n{ChangeDate}\n参加ライバー : {Livers: / }\n{URL}";
            public LiversAndDateEvent(YouTubeItem old, YouTubeItem latest) : this(old, latest, DateTime.Now) { }
            protected private LiversAndDateEvent(YouTubeItem old, YouTubeItem latest, DateTime dt) : base(old, latest, dt)
            {
                AddDateContentFormats();
            }
            public override string GetDiscordContent(LiverDetail liver) => GetLiversDiscordContent(liver);

            public class YouTubeChangeLiversAndDateEventConverter : YouTubeChangeEventConverter
            {
                private protected override YouTubeChangeEvent ResultEvent(YouTubeItem old, YouTubeItem latest, DateTime dt)
                    => new LiversAndDateEvent(old, latest, dt);
            }
        }
        [JsonConverter(typeof(YouTubeChangeOtherEventConverter))]
        public class OtherEvent : YouTubeChangeEvent
        {
            [JsonIgnore]
            public override string FormatContent => "配信情報が変更されました\n{Title}\n{URL}";
            public OtherEvent(YouTubeItem old, YouTubeItem latest) : this(old, latest, DateTime.Now) { }
            protected private OtherEvent(YouTubeItem old, YouTubeItem latest, DateTime dt) : base(old, latest, dt) { }

            public class YouTubeChangeOtherEventConverter : YouTubeChangeEventConverter
            {
                private protected override YouTubeChangeEvent ResultEvent(YouTubeItem old, YouTubeItem latest, DateTime dt)
                    => new OtherEvent(old, latest, dt);
            }
        }

        public abstract class YouTubeChangeEventConverter : EventConverter
        {
            public override YouTubeChangeEvent Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();
                var (_, item, date) = ReadBase(ref reader, type, options);
                reader.Read();
                reader.Read();
                var old = JsonSerializer.Deserialize<YouTubeItem>(ref reader, options);

                reader.Read();
                if (reader.TokenType == JsonTokenType.EndObject) return ResultEvent(old, item, date);
                throw new JsonException();
            }
            public override void Write(Utf8JsonWriter writer, EventBase<YouTubeItem> value, JsonSerializerOptions options)
            {
                YouTubeChangeEvent evt = (YouTubeChangeEvent)value;
                writer.WriteStartObject();
                WriteBase(writer, value, options);
                writer.WritePropertyName("OldItem");
                JsonSerializer.Serialize(writer, evt.OldItem, options);
                writer.WriteEndObject();
            }
            private protected sealed override EventBase<YouTubeItem> ResultEvent(YouTubeItem value, DateTime dt)
               => null;
            private protected abstract YouTubeChangeEvent ResultEvent(YouTubeItem old, YouTubeItem latest, DateTime dt);
        }
    }
    [JsonConverter(typeof(YouTubeDeleteLiveEventConverter))]
    public class YouTubeDeleteLiveEvent : YouTubeEvent
    {
        [JsonIgnore]
        public override string FormatContent => "配信待機所が削除されました\n{Title}\n{URL}";

        public YouTubeDeleteLiveEvent(YouTubeItem value) : this(value, DateTime.Now) { }
        protected private YouTubeDeleteLiveEvent(YouTubeItem value, DateTime dt) : base(value, dt) { }

        public class YouTubeDeleteLiveEventConverter : EventConverter
        {
            private protected override EventBase<YouTubeItem> ResultEvent(YouTubeItem value, DateTime dt)
                => new YouTubeDeleteLiveEvent(value, dt);
        }
    }
    public class YouTubeAlradyLivedEvent : YouTubeEvent
    {
        [JsonIgnore]
        public override string FormatContent => null;

        public YouTubeAlradyLivedEvent(YouTubeItem value) : base(value) { }
    }
}

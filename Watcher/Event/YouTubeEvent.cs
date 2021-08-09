using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using VTuberNotifier.Liver;
using static VTuberNotifier.Watcher.YouTubeItem;

namespace VTuberNotifier.Watcher.Event
{
    public abstract class YouTubeEvent : EventBase<YouTubeItem>
    {
        public YouTubeEvent(YouTubeItem latest, YouTubeItem old) : this(latest, old, DateTime.Now) { }
        protected private YouTubeEvent(YouTubeItem latest, YouTubeItem old, DateTime dt, bool determine = true)
            : base(latest, old, dt, determine) { }
    }

    public abstract class YouTubeNewEvent : YouTubeEvent
    {
        protected private YouTubeNewEvent(YouTubeItem value, DateTime dt) : base(value, null, dt) { }

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

        public class LiveEvent : YouTubeNewEvent
        {
            public override string EventTypeName => "YouTubeNewLive";
            [JsonIgnore]
            public override string FormatContent => "配信待機所が作成されました\n{Title}\n参加ライバー : {Livers: / }\n{Date}\n{URL}";

            public LiveEvent(YouTubeItem value) : this(value, DateTime.Now) { }
            protected private LiveEvent(YouTubeItem value, DateTime dt) : base(value, dt) { }
        }
        public class PremireEvent : YouTubeNewEvent
        {
            public override string EventTypeName => "YouTubeNewPremire";
            [JsonIgnore]
            public override string FormatContent => "プレミア公開待機所が作成されました\n{Title}\n参加ライバー : {Livers: / }\n{Date}\n{URL}";

            public PremireEvent(YouTubeItem value) : this(value, DateTime.Now) { }
            protected private PremireEvent(YouTubeItem value, DateTime dt) : base(value, dt) { }
        }
        public class VideoEvent : YouTubeNewEvent
        {
            public override string EventTypeName => "YouTubeNewVideo";
            [JsonIgnore]
            public override string FormatContent => "新規動画が投稿されました\n{Title}\n参加ライバー : {Livers: / }\n{URL}";

            public VideoEvent(YouTubeItem value) : this(value, DateTime.Now) { }
            protected private VideoEvent(YouTubeItem value, DateTime dt) : base(value, dt) { }
        }
    }
    public class YouTubeChangeEvent : YouTubeEvent
    {
        public override string EventTypeName => "YouTubeChange";
        [JsonIgnore]
        public override string FormatContent => "配信情報が変更されました\n{Title}\n{URL}";

        protected private YouTubeChangeEvent(YouTubeItem latest, YouTubeItem old, DateTime dt, bool determine)
            : base(latest, old, dt, determine) { }

        public static YouTubeChangeEvent CreateEvent(YouTubeItem latest, YouTubeItem old)
        {
            var ce = new YouTubeChangeEvent(latest, old, DateTime.Now, true);

            var dic = new Dictionary<LiverDetail, EventBase<YouTubeItem>[]>();
            var list = new List<EventBase<YouTubeItem>>();
            if (latest.Title != old.Title)
                list.Add(new TitleEvent(latest, old));
            if (latest.LiveStartDate != old.LiveStartDate && latest.LiveStartDate != DateTime.MinValue)
                list.Add(new DateEvent(latest, old));
            if (latest.Description != old.Description)
            {
                list.Add(new DescriptionEvent(latest, old));
                if (!latest.Livers.SequenceEqual(old.Livers))
                    list.Add(new LiverEvent(latest, old));
            }

            var array = list.ToArray();
            foreach (var liver in old.Livers)
                dic.Add(liver, array);
            foreach (var liver in latest.Livers.Except(old.Livers))
                dic.Add(liver, new[] { YouTubeNewEvent.CreateEvent(latest) });
            ce.EventsByLiver = dic;

            return ce;
        }

        public class TitleEvent : YouTubeChangeEvent
        {
            public override string EventTypeName => "YouTubeChangeTitle";
            [JsonIgnore]
            public override string FormatContent => "配信タイトルが変更されました\n{Title}\n{URL}";
            public TitleEvent(YouTubeItem latest, YouTubeItem old) : this(latest, old, DateTime.Now) { }
            protected private TitleEvent(YouTubeItem latest, YouTubeItem old, DateTime dt) : base(latest, old, dt, false) { }
        }
        public class DescriptionEvent : YouTubeChangeEvent
        {
            public override string EventTypeName => "YouTubeChangeDescription";
            [JsonIgnore]
            public override string FormatContent => "配信概要欄が変更されました\n{Title}\n{URL}";
            public DescriptionEvent(YouTubeItem latest, YouTubeItem old) : this(latest, old, DateTime.Now) { }
            protected private DescriptionEvent(YouTubeItem latest, YouTubeItem old, DateTime dt) : base(latest, old, dt, false) { }
        }
        public class LiverEvent : YouTubeChangeEvent
        {
            public override string EventTypeName => "YouTubeChangeLiver";
            [JsonIgnore]
            public override string FormatContent => "参加ライバーが変更されました\n{Title}\n参加ライバー : {Livers: / }\n{URL}";
            public LiverEvent(YouTubeItem latest, YouTubeItem old) : this(latest, old, DateTime.Now) { }
            protected private LiverEvent(YouTubeItem latest, YouTubeItem old, DateTime dt) : base(latest, old, dt, false) { }
            public override string GetDiscordContent(LiverDetail liver)
            {
                string str;
                if (Item.Livers.Except(OldItem.Livers).Contains(liver))
                    str = YouTubeNewEvent.CreateEvent(Item).FormatContent;
                else if (Item.Livers.Except(OldItem.Livers).Contains(liver))
                    str = new YouTubeDeleteLiveEvent(Item).FormatContent;
                else str = FormatContent;

                return ConvertContent(str, liver);
            }
        }
        public class DateEvent : YouTubeChangeEvent
        {
            public override string EventTypeName => "YouTubeChangeDate";
            [JsonIgnore]
            public override string FormatContent => "配信開始時刻が変更されました\n{Title}\n{ChangeDate}\n{URL}";
            public DateEvent(YouTubeItem latest, YouTubeItem old) : this(latest, old, DateTime.Now) { }
            protected private DateEvent(YouTubeItem latest, YouTubeItem old, DateTime dt) : base(latest, old, dt, false)
            {
                string format = OldItem.LiveStartDate.Year != Item.LiveStartDate.Year ? "yyyy/MM/dd HH:mm" : "MM/dd HH:mm";

                var dic = ContentFormat;
                if (ContentFormat.ContainsKey("Date")) dic.Remove("Date");
                dic.Add("Date", Item.LiveStartDate.ToString(format));
                dic.Add("OldDate", OldItem.LiveStartDate.ToString(format));
                dic.Add("ChangeDate", $"{ContentFormat["OldDate"]} → {ContentFormat["Date"]}");
                ContentFormat = dic;
            }
        }
    }
    public class YouTubeDeleteLiveEvent : YouTubeEvent
    {
        public override string EventTypeName => "YouTubeDeleteLive";
        [JsonIgnore]
        public override string FormatContent => "配信待機所が削除されました\n{Title}\n{URL}";

        public YouTubeDeleteLiveEvent(YouTubeItem value) : this(value, DateTime.Now) { }
        protected private YouTubeDeleteLiveEvent(YouTubeItem value, DateTime dt) : base(null, value, dt) { }
    }
    public class YouTubeStartLiveEvent : YouTubeEvent
    {
        public override string EventTypeName => "YouTubeStartLive";
        [JsonIgnore]
        public override string FormatContent => "配信が開始されました\n{Title}\n参加ライバー : {Livers: / }\n{URL}";

        public YouTubeStartLiveEvent(YouTubeItem value) : this(value, DateTime.Now) { }
        protected private YouTubeStartLiveEvent(YouTubeItem value, DateTime dt) : base(null, value, dt) { }
    }
    public class YouTubeAlradyLivedEvent : YouTubeEvent
    {
        public override string EventTypeName => "YouTubeAlreadyLived";
        [JsonIgnore]
        public override string FormatContent => null;

        public YouTubeAlradyLivedEvent(YouTubeItem value) : base(null, value) { }
    }
}
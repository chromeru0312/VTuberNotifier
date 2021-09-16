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
        public override string EventTypeName => GetEventTypeName();
        public abstract string YouTubeEventTypeName { get; }

        protected private YouTubeEvent(YouTubeItem latest, YouTubeItem old, DateTime dt)
            : base(latest, old, dt, false) { }

        private string GetEventTypeName()
        {
            var type = $"YouTube{YouTubeEventTypeName}";
            if (this is ISelfEvent)
                type += "(Self)";
            else if (this is ICollaborationEvent)
                type += "(Collaboration)";
            return type;
        }
    }

    public class YouTubeNewEvent : YouTubeEvent
    {
        public override string YouTubeEventTypeName => "New";
        [JsonIgnore]
        public override string FormatContent => "新規コンテンツが投稿されました\n{Title}\n参加ライバー : {Livers: / }\n{URL}";

        protected private YouTubeNewEvent(YouTubeItem value, DateTime dt) : base(value, null, dt) { }

        public static YouTubeNewEvent CreateEvent(YouTubeItem item)
        {
            var evt = new YouTubeNewEvent(item, DateTime.Now);
            var dic = new Dictionary<LiverDetail, EventBase<YouTubeItem>[]>();
            foreach (var liver in item.Livers)
            {
                var owner = liver.Equals(item.Channel);
                YouTubeNewEvent inner = item.Mode switch
                {
                    YouTubeMode.Video => owner ? new VideoEvent.SelfEvent(item) : new VideoEvent.CollaborationEvent(item),
                    YouTubeMode.Premire => owner ? new PremireEvent.SelfEvent(item) : new PremireEvent.CollaborationEvent(item),
                    YouTubeMode.Live => owner ? new LiveEvent.SelfEvent(item) : new LiveEvent.CollaborationEvent(item),
                    _ => throw new InvalidCastException()
                };
                dic.Add(liver, new[] { inner });
            }
            evt.EventsByLiver = dic;
            return evt;
        }

        public class LiveEvent : YouTubeNewEvent
        {
            public override string YouTubeEventTypeName => "NewLive";
            [JsonIgnore]
            public override string FormatContent => "配信待機所が作成されました\n{Title}\n参加ライバー : {Livers: / }\n{Date}\n{URL}";

            protected private LiveEvent(YouTubeItem value, DateTime dt) : base(value, dt) { }

            public class SelfEvent : LiveEvent, ISelfEvent
            {
                public SelfEvent(YouTubeItem value) : this(value, DateTime.Now) { }
                protected private SelfEvent(YouTubeItem value, DateTime dt) : base(value, dt) { }
            }
            public class CollaborationEvent : LiveEvent, ICollaborationEvent
            {
                public CollaborationEvent(YouTubeItem value) : this(value, DateTime.Now) { }
                protected private CollaborationEvent(YouTubeItem value, DateTime dt) : base(value, dt) { }
            }
        }
        public class PremireEvent : YouTubeNewEvent
        {
            public override string YouTubeEventTypeName => "NewPremire";
            [JsonIgnore]
            public override string FormatContent => "プレミア公開待機所が作成されました\n{Title}\n参加ライバー : {Livers: / }\n{Date}\n{URL}";

            protected private PremireEvent(YouTubeItem value, DateTime dt) : base(value, dt) { }

            public class SelfEvent : PremireEvent, ISelfEvent
            {
                public SelfEvent(YouTubeItem value) : this(value, DateTime.Now) { }
                protected private SelfEvent(YouTubeItem value, DateTime dt) : base(value, dt) { }
            }
            public class CollaborationEvent : PremireEvent, ICollaborationEvent
            {
                public CollaborationEvent(YouTubeItem value) : this(value, DateTime.Now) { }
                protected private CollaborationEvent(YouTubeItem value, DateTime dt) : base(value, dt) { }
            }
        }
        public class VideoEvent : YouTubeNewEvent
        {
            public override string YouTubeEventTypeName => "NewVideo";
            [JsonIgnore]
            public override string FormatContent => "新規動画が投稿されました\n{Title}\n参加ライバー : {Livers: / }\n{URL}";

            protected private VideoEvent(YouTubeItem value, DateTime dt) : base(value, dt) { }

            public static new VideoEvent CreateEvent(YouTubeItem item)
            {
                var evt = new CollaborationEvent(item);
                var dic = new Dictionary<LiverDetail, EventBase<YouTubeItem>[]>(evt.EventsByLiver);
                if (item.Channel is LiverDetail liver && dic.ContainsKey(liver))
                {
                    dic[liver] = new[] { new SelfEvent(item) };
                }
                evt.EventsByLiver = dic;

                return evt;
            }

            public class SelfEvent : VideoEvent, ISelfEvent
            {
                public SelfEvent(YouTubeItem value) : this(value, DateTime.Now) { }
                protected private SelfEvent(YouTubeItem value, DateTime dt) : base(value, dt) { }
            }
            public class CollaborationEvent : VideoEvent, ICollaborationEvent
            {
                public CollaborationEvent(YouTubeItem value) : this(value, DateTime.Now) { }
                protected private CollaborationEvent(YouTubeItem value, DateTime dt) : base(value, dt) { }
            }
        }
    }
    public class YouTubeChangeEvent : YouTubeEvent
    {
        public override string YouTubeEventTypeName => "Change";
        [JsonIgnore]
        public override string FormatContent => "配信情報が変更されました\n{Title}\n{URL}";

        protected private YouTubeChangeEvent(YouTubeItem latest, YouTubeItem old, DateTime dt)
            : base(latest, old, dt) { }

        public static YouTubeChangeEvent CreateEvent(YouTubeItem latest, YouTubeItem old)
        {
            var evt = new YouTubeChangeEvent(latest, old, DateTime.Now);
            var dic = new Dictionary<LiverDetail, EventBase<YouTubeItem>[]>();
            foreach(var liver in old.Livers)
            {
                var list = new List<EventBase<YouTubeItem>>();
                var owner = liver.Equals(latest.Channel);
                if (latest.Title != old.Title)
                    list.Add(owner ? new TitleEvent.SelfEvent(latest, old) : new TitleEvent.CollaborationEvent(latest, old));
                if (latest.LiveStartDate != old.LiveStartDate && latest.LiveStartDate != DateTime.MinValue)
                    list.Add(owner ? new DateEvent.SelfEvent(latest, old) : new DateEvent.CollaborationEvent(latest, old));
                if (latest.Description != old.Description)
                {
                    list.Add(owner ? new DescriptionEvent.SelfEvent(latest, old) : new DescriptionEvent.CollaborationEvent(latest, old));
                    if (!latest.Livers.SequenceEqual(old.Livers))
                        list.Add(owner ? new LiverEvent.SelfEvent(latest, old) : new LiverEvent.CollaborationEvent(latest, old));
                }
                dic.Add(liver, list.ToArray());
            }
            foreach (var liver in latest.Livers.Except(old.Livers))
            {
                dic.Add(liver, new[] { YouTubeNewEvent.CreateEvent(latest) });
            }
            evt.EventsByLiver = dic;
            return evt;
        }

        public class TitleEvent : YouTubeChangeEvent
        {
            public override string YouTubeEventTypeName => "ChangeTitle";
            [JsonIgnore]
            public override string FormatContent => "配信タイトルが変更されました\n{Title}\n{URL}";
            protected private TitleEvent(YouTubeItem latest, YouTubeItem old, DateTime dt) : base(latest, old, dt) { }

            public class SelfEvent : TitleEvent, ISelfEvent
            {
                public SelfEvent(YouTubeItem latest, YouTubeItem old) : this(latest, old, DateTime.Now) { }
                protected private SelfEvent(YouTubeItem latest, YouTubeItem old, DateTime dt) : base(latest, old, dt) { }
            }
            public class CollaborationEvent : TitleEvent, ICollaborationEvent
            {
                public CollaborationEvent(YouTubeItem latest, YouTubeItem old) : this(latest, old, DateTime.Now) { }
                protected private CollaborationEvent(YouTubeItem latest, YouTubeItem old, DateTime dt) : base(latest, old, dt) { }
            }
        }
        public class DescriptionEvent : YouTubeChangeEvent
        {
            public override string YouTubeEventTypeName => "ChangeDescription";
            [JsonIgnore]
            public override string FormatContent => "配信概要欄が変更されました\n{Title}\n{URL}";
            protected private DescriptionEvent(YouTubeItem latest, YouTubeItem old, DateTime dt) : base(latest, old, dt) { }

            public class SelfEvent : DescriptionEvent, ISelfEvent
            {
                public SelfEvent(YouTubeItem latest, YouTubeItem old) : this(latest, old, DateTime.Now) { }
                protected private SelfEvent(YouTubeItem latest, YouTubeItem old, DateTime dt) : base(latest, old, dt) { }
            }
            public class CollaborationEvent : DescriptionEvent, ICollaborationEvent
            {
                public CollaborationEvent(YouTubeItem latest, YouTubeItem old) : this(latest, old, DateTime.Now) { }
                protected private CollaborationEvent(YouTubeItem latest, YouTubeItem old, DateTime dt) : base(latest, old, dt) { }
            }
        }
        public class LiverEvent : YouTubeChangeEvent
        {
            public override string YouTubeEventTypeName => "ChangeLiver";
            [JsonIgnore]
            public override string FormatContent => "参加ライバーが変更されました\n{Title}\n参加ライバー : {Livers: / }\n{URL}";
            protected private LiverEvent(YouTubeItem latest, YouTubeItem old, DateTime dt) : base(latest, old, dt) { }

            public override string GetDiscordContent(LiverDetail liver)
            {
                string str;
                if (Item.Livers.Except(OldItem.Livers).Contains(liver))
                    str = YouTubeNewEvent.CreateEvent(Item).FormatContent;
                else if (Item.Livers.Except(OldItem.Livers).Contains(liver))
                    str = YouTubeDeleteLiveEvent.CreateEvent(Item).FormatContent;
                else str = FormatContent;

                return ConvertContent(str, liver);
            }

            public class SelfEvent : LiverEvent, ISelfEvent
            {
                public SelfEvent(YouTubeItem latest, YouTubeItem old) : this(latest, old, DateTime.Now) { }
                protected private SelfEvent(YouTubeItem latest, YouTubeItem old, DateTime dt) : base(latest, old, dt) { }
            }
            public class CollaborationEvent : LiverEvent, ICollaborationEvent
            {
                public CollaborationEvent(YouTubeItem latest, YouTubeItem old) : this(latest, old, DateTime.Now) { }
                protected private CollaborationEvent(YouTubeItem latest, YouTubeItem old, DateTime dt) : base(latest, old, dt) { }
            }
        }
        public class DateEvent : YouTubeChangeEvent
        {
            public override string YouTubeEventTypeName => "ChangeDate";
            [JsonIgnore]
            public override string FormatContent => "配信開始時刻が変更されました\n{Title}\n{ChangeDate}\n{URL}";
            protected private DateEvent(YouTubeItem latest, YouTubeItem old, DateTime dt) : base(latest, old, dt)
            {
                string format = OldItem.LiveStartDate.Year != Item.LiveStartDate.Year ? "yyyy/MM/dd HH:mm" : "MM/dd HH:mm";

                var dic = ContentFormat;
                if (ContentFormat.ContainsKey("Date")) dic.Remove("Date");
                dic.Add("Date", Item.LiveStartDate.ToString(format));
                dic.Add("OldDate", OldItem.LiveStartDate.ToString(format));
                dic.Add("ChangeDate", $"{ContentFormat["OldDate"]} → {ContentFormat["Date"]}");
                ContentFormat = dic;
            }

            public class SelfEvent : DateEvent, ISelfEvent
            {
                public SelfEvent(YouTubeItem latest, YouTubeItem old) : this(latest, old, DateTime.Now) { }
                protected private SelfEvent(YouTubeItem latest, YouTubeItem old, DateTime dt) : base(latest, old, dt) { }
            }
            public class CollaborationEvent : DateEvent, ICollaborationEvent
            {
                public CollaborationEvent(YouTubeItem latest, YouTubeItem old) : this(latest, old, DateTime.Now) { }
                protected private CollaborationEvent(YouTubeItem latest, YouTubeItem old, DateTime dt) : base(latest, old, dt) { }
            }
        }
    }
    public class YouTubeDeleteLiveEvent : YouTubeEvent
    {
        public override string YouTubeEventTypeName => "DeleteLive";
        [JsonIgnore]
        public override string FormatContent => "配信待機所が削除されました\n{Title}\n{URL}";

        protected private YouTubeDeleteLiveEvent(YouTubeItem value, DateTime dt) : base(null, value, dt) { }

        public static YouTubeDeleteLiveEvent CreateEvent(YouTubeItem item)
        {
            var evt = new YouTubeDeleteLiveEvent(item, DateTime.Now);
            var dic = new Dictionary<LiverDetail, EventBase<YouTubeItem>[]>();
            foreach (var liver in item.Livers)
            {
                if (liver.Equals(item.Channel))
                    dic.Add(liver, new[] { new SelfEvent(item) });
                else
                    dic.Add(liver, new[] { new CollaborationEvent(item) });
            }
            evt.EventsByLiver = dic;

            return evt;
        }

        public class SelfEvent : YouTubeDeleteLiveEvent, ISelfEvent
        {
            public SelfEvent(YouTubeItem value) : this(value, DateTime.Now) { }
            protected private SelfEvent(YouTubeItem value, DateTime dt) : base(value, dt) { }
        }
        public class CollaborationEvent : YouTubeDeleteLiveEvent, ICollaborationEvent
        {
            public CollaborationEvent(YouTubeItem value) : this(value, DateTime.Now) { }
            protected private CollaborationEvent(YouTubeItem value, DateTime dt) : base(value, dt) { }
        }
    }
    public class YouTubeStartLiveEvent : YouTubeEvent
    {
        public override string YouTubeEventTypeName => "StartLive";
        [JsonIgnore]
        public override string FormatContent => "配信が開始されました\n{Title}\n参加ライバー : {Livers: / }\n{URL}";

        protected private YouTubeStartLiveEvent(YouTubeItem value, DateTime dt) : base(null, value, dt) { }

        public static YouTubeStartLiveEvent CreateEvent(YouTubeItem item)
        {
            var evt = new YouTubeStartLiveEvent(item, DateTime.Now);
            var dic = new Dictionary<LiverDetail, EventBase<YouTubeItem>[]>();
            foreach (var liver in item.Livers)
            {
                if (liver.Equals(item.Channel))
                    dic.Add(liver, new[] { new SelfEvent(item) });
                else
                    dic.Add(liver, new[] { new CollaborationEvent(item) });
            }
            evt.EventsByLiver = dic;

            return evt;
        }

        public class SelfEvent : YouTubeStartLiveEvent, ISelfEvent
        {
            public SelfEvent(YouTubeItem value) : this(value, DateTime.Now) { }
            protected private SelfEvent(YouTubeItem value, DateTime dt) : base(value, dt) { }
        }
        public class CollaborationEvent : YouTubeStartLiveEvent, ICollaborationEvent
        {
            public CollaborationEvent(YouTubeItem value) : this(value, DateTime.Now) { }
            protected private CollaborationEvent(YouTubeItem value, DateTime dt) : base(value, dt) { }
        }
    }
    public class YouTubeAlradyLivedEvent : YouTubeEvent
    {
        public override string YouTubeEventTypeName => "AlreadyLived";
        [JsonIgnore]
        public override string FormatContent => null;

        public YouTubeAlradyLivedEvent(YouTubeItem value) : base(null, value, DateTime.Now) { }
    }

    public interface ISelfEvent { }
    public interface ICollaborationEvent { }
}
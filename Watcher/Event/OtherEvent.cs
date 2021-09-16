using System;
using System.Text.Json.Serialization;

namespace VTuberNotifier.Watcher.Event
{
    public class PRTimesNewArticleEvent : EventBase<PRTimesArticle>
    {
        public override string EventTypeName => "NewArticle";
        public PRTimesNewArticleEvent(PRTimesArticle value) : this(value, DateTime.Now) { }
        protected private PRTimesNewArticleEvent(PRTimesArticle value, DateTime dt) : base(value, null, dt) { }

        [JsonIgnore]
        public override string FormatContent => "新しいニュースリリースが配信されました\n{Title}({Date})\n参加ライバー : {Livers: / }\n{URL}";
    }
    public class NicoNewLiveEvent : EventBase<NicoLiveItem>
    {
        public override string EventTypeName => "NicoNewLive";
        [JsonIgnore]
        public override string FormatContent => "新しいライブ配信が作成されました\n{Title}\n参加ライバー : {Livers: / }\n{Date}\n{URL}";

        public NicoNewLiveEvent(NicoLiveItem value) : this(value, DateTime.Now) { }
        protected private NicoNewLiveEvent(NicoLiveItem value, DateTime dt) : base(value, null, dt) { }
    }
    public class NicoStartLiveEvent : EventBase<NicoLiveItem>
    {
        public override string EventTypeName => "NicoStartLive";
        [JsonIgnore]
        public override string FormatContent => "ライブが開始されました\n{Title}\n参加ライバー : {Livers: / }\n{Date}\n{URL}";

        public NicoStartLiveEvent(NicoLiveItem value) : this(value, DateTime.Now) { }
        protected private NicoStartLiveEvent(NicoLiveItem value, DateTime dt) : base(null, value, dt) { }
    }
}
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
}
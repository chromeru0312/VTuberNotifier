using System;
using System.Text.Json.Serialization;
using VTuberNotifier.Watcher.Feed;

namespace VTuberNotifier.Watcher.Event
{
    [JsonConverter(typeof(PRTimesNewArticleEventConverter))]
    public class PRTimesNewArticleEvent : EventBase<PRTimesArticle>
    {
        public PRTimesNewArticleEvent(PRTimesArticle value) : base(nameof(PRTimesNewArticleEvent), value) { }
        protected private PRTimesNewArticleEvent(PRTimesArticle value, DateTime dt)
             : base(nameof(PRTimesNewArticleEvent), value, dt) { }

        public override string FormatContent => "新しいニュースリリースが配信されました\n{Title}({Date})\n参加ライバー : {Livers: / }\n{URL}";

        public class PRTimesNewArticleEventConverter : EventConverter
        {
            private protected override EventBase<PRTimesArticle> ResultEvent(PRTimesArticle value, DateTime dt)
                => new PRTimesNewArticleEvent(value, dt);
        }
    }
}

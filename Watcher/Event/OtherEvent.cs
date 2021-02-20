using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VTuberNotifier.Liver;
using VTuberNotifier.Watcher.Feed;

namespace VTuberNotifier.Watcher.Event
{
    public class PRTimesNewArticleEvent : EventBase<PRTimesArticle>
    {
        public PRTimesNewArticleEvent(PRTimesArticle value) : base(value, new(value.Livers)) { }

        public override string GetDiscordContent(LiverDetail liver)
        {
            var format = "新しいニュースリリースが配信されました\n{Title}({Date})\n参加ライバー:{Livers: / }\n{URL}";
            return ConvertContent(format, liver);
        }
    }
}

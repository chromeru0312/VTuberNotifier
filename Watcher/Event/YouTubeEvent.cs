using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VTuberNotifier.Discord;
using VTuberNotifier.Liver;
using VTuberNotifier.Watcher.Feed;

namespace VTuberNotifier.Watcher.Event
{
    public abstract class YouTubeEvent : EventBase<YouTubeItem>
    {
        public YouTubeEvent(YouTubeItem value) : base(value, new(value.Livers)) { }
    }

    public class YouTubeNewLiveEvent : YouTubeEvent
    {
        public YouTubeNewLiveEvent(YouTubeItem value) : base(value) { }

        public override string GetDiscordContent(LiverDetail liver)
        {
            var format = "配信待機所が作成されました\n{Title}\n参加ライバー:{Livers: / }\n{Date}\n{URL}";
            if (Item.IsTwitterSource) format += "\n※この通知はTwitterからの情報のため、信憑性が薄い場合があります。";
            return ConvertContent(format, liver);
        }
    }
    public class YouTubeStartLiveEvent : YouTubeEvent
    {
        public YouTubeStartLiveEvent(YouTubeItem value) : base(value) { }

        public override string GetDiscordContent(LiverDetail liver)
        {
            var format = "配信が開始されました\n{Title}\n参加ライバー:{Livers: / }\n{URL}";
            return ConvertContent(format, liver);
        }
    }
    public class YouTubeChangeInfoEvent : YouTubeEvent
    {
        public YouTubeChangeInfoEvent(YouTubeItem old, YouTubeItem latest) : base(latest)
        {
            ContentFormat.Remove("Date");
            if (old.LiveStartDate.Year != latest.LiveStartDate.Year)
            {
                ContentFormat.Add("Date", latest.LiveStartDate.ToString("yyyy/MM/dd HH:mm"));
                ContentFormat.Add("OldDate", old.LiveStartDate.ToString("yyyy/MM/dd HH:mm"));
            }
            else
            {
                ContentFormat.Add("Date", latest.LiveStartDate.ToString("MM/dd HH:mm"));
                ContentFormat.Add("OldDate", old.LiveStartDate.ToString("MM/dd HH:mm"));
            }
            ContentFormat.Add("ChangeDate", $"{ContentFormat["OldDate"]} → {ContentFormat["Date"]}");
        }

        public override string GetDiscordContent(LiverDetail liver)
        {
            return ConvertContent("配信開始時刻が変更されました\n{Title}\n{ChangeDate}\n{URL}", liver);
        }
    }
    public class YouTubeDeleteLiveEvent : YouTubeEvent
    {
        public YouTubeDeleteLiveEvent(YouTubeItem value) : base(value) { }

        public override string GetDiscordContent(LiverDetail liver)
        {
            return ConvertContent("配信待機所が削除されました\n{Title}\n{URL}", liver);
        }
    }
}

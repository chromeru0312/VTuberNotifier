using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VTuberNotifier.Notification.Discord;
using VTuberNotifier.Liver;
using VTuberNotifier.Watcher.Event;
using VTuberNotifier.Watcher.Feed;
using VTuberNotifier.Watcher.Store;
using VTuberNotifier.Notification;

namespace VTuberNotifier.Watcher
{
    public class WatcherTask
    {
        public static WatcherTask Instance { get; private set; } = null;

        private WatcherTask()
        {
            TimerManager.CreateInstance();

            var tm = TimerManager.Instance;
            //PRTimes.CreateInstance();
            //TimerTaskManager.Instance.AddAction(20 * 60, PRTimesTask);
            NijisanjiWatcher.CreateInstance();
            tm.AddAction(20 * 60, NijisanjiStoreTask);
            BoothWatcher.CreateInstance();
            tm.AddAction(20 * 60, BoothTask);
            TwitterWatcher.CreateInstance();
            //tm.AddAction(TimerManager.Interval, TwitterTask);
            YouTubeFeed.CreateInstance();
            tm.AddAction(TimerManager.Interval, YouTubeChangeTask);

            DiscordBot.CreateInstance();
            Task.Run(DiscordBot.Instance.BotStart);
            Task.Run(YouTubeNotificationTask);
        }
        public static void CreateInstance()
        {
            if (Instance == null) Instance = new WatcherTask();
        }

        public async Task YouTubeNotificationTask()
        {
            var list = new List<Address>(LiverData.GetAllLiversList()).Concat(LiverGroup.GroupList);
            foreach (var address in list)
            {
                var id = address.YouTubeId;
                if (id == null) continue;
                string type;
                if (address is LiverDetail) type = "liver";
                else type = "group";

                try
                {
                    var b = await YouTubeFeed.CheckSubscribe(id);
                    await LocalConsole.Log(this, new(LogSeverity.Debug, "Notification", $"Load {type}: {id} -> {b}"));
                    if (!b)
                    {
                        YouTubeFeed.Instance.RegisterNotification(id);
                        await LocalConsole.Log(this, new(LogSeverity.Info, "Notification", $"Register {type}: {id}"));
                    }
                }
                catch (Exception e)
                {
                    await LocalConsole.Log(this, new(LogSeverity.Error, "Notification", $"Some Error occured: {id}", e));
                }
            }
        }


        public async Task PRTimesTask()
        {
            var groups = LiverGroup.GroupList;
            List<Task<List<PRTimesArticle>>> list = new(groups.Count);

            for (int i = 0; i < groups.Count; i++)
            {
                list.Add(PRTimesFeed.Instance.ReadFeed(groups[i]));
            }
            for (int i = 0; i < groups.Count; i++)
            {
                var res = await list[i];
                if (res != null && res.Count != 0)
                {
                    foreach (var article in res)
                        await NotifyEvent.Notify(new PRTimesNewArticleEvent(article));
                }
            }
        }

        public async Task BoothTask()
        {
            var groups = LiverGroup.GroupList;
            List<Task<List<BoothProduct>>> list = new(groups.Count);

            for (int i = 0; i < groups.Count; i++)
            {
                list.Add(BoothWatcher.Instance.GetNewProduct(groups[i]));
            }
            for (int i = 0; i < groups.Count; i++)
            {
                var res = await list[i];
                if (res != null && res.Count != 0)
                {
                    foreach (var product in res)
                    {
                        if (!product.IsOnSale) TimerManager.Instance.AddEventAlarm(product.StartDate, new BoothStartSellEvent(product));
                        await NotifyEvent.Notify(new BoothNewProductEvent(product));
                    }
                }
            }
        }

        public async Task NijisanjiStoreTask()
        {
            var res = await NijisanjiWatcher.Instance.GetNewProduct();
            if (res != null && res.Count != 0)
            {
                foreach (NijisanjiProduct product in res)
                {
                    if (!product.IsOnSale)
                    {
                        var now = DateTime.Now;
                        if (product.StartDate <= now)
                        {
                            var dt = DateTime.Today.AddHours(now.Hour + 1);
                            TimerManager.Instance.AddAlarm(dt, new(() => Task(dt, product)));
                        }
                        else TimerManager.Instance.AddEventAlarm(product.StartDate, new NijisanjiStartSellEvent(product));
                    }
                    await NotifyEvent.Notify(new NijisanjiNewProductEvent(product));
                }
            }

            static async Task Task(DateTime check, NijisanjiProduct product)
            {
                if (await NijisanjiWatcher.Instance.CheckOnSale(product))
                    await NotifyEvent.Notify(new NijisanjiStartSellEvent(product));
                else
                {
                    var dt = DateTime.Today.AddHours(DateTime.Now.Hour + 1);
                    TimerManager.Instance.AddAlarm(dt, new(() => Task(dt, product)));
                }
            }
        }

        public async Task TwitterTask()
        {
            var all = new List<Address>(LiverData.GetAllLiversList().Select(l => (Address)l).Concat(LiverGroup.GroupList));
            List<Task<List<Tweet>>> list = new(all.Count);

            for (int i = 0; i < all.Count; i++)
            {
                list.Add(TwitterWatcher.Instance.GetNewTweets(all[i]));
            }
            for (int i = 0; i < all.Count; i++)
            {
                var res = await list[i];
                if (res != null && res.Count != 0)
                {
                    foreach (var tweet in res)
                    {
                        foreach (var url in tweet.Urls)
                        {
                            string id;
                            if (url.ExpandedUrl.StartsWith("https://www.youtube.com/"))
                            {
                                id = url.ExpandedUrl[(url.ExpandedUrl.IndexOf("v=") + 2)..];
                                var index = id.IndexOf('&');
                                if (index != -1) id = id[..index];
                            }
                            else if (url.ExpandedUrl.StartsWith("https://youtu.be/"))
                            {
                                id = url.ExpandedUrl[17..];
                                var index = id.IndexOf('?');
                                if (index != -1) id = id[..index];
                            }
                            else continue;

                            YouTubeFeed.Instance.CheckNewLive(id, out var video);
                            if (video == null) break;
                            var livers = video.Livers;
                            if (all[i] is LiverDetail l && !livers.Contains(l)) livers = new List<LiverDetail>(livers) { l };
                            await NotifyEvent.Notify(new YouTubeNewLiveEvent(video));
                        }
                    }
                }
            }
        }

        public async Task YouTubeChangeTask()
        {
            var list = YouTubeFeed.Instance.CheckLiveChanged();
            foreach (var evt in list)
                await NotifyEvent.Notify(evt);
        }

        public async Task OneDayTask()
        {
            LocalConsole.CreateNewLogFile();
            await LiverData.UpdateLivers();
        }
    }
}

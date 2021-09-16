using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VTuberNotifier.Liver;
using VTuberNotifier.Watcher.Event;
using VTuberNotifier.Watcher.Store;
using VTuberNotifier.Notification;

namespace VTuberNotifier.Watcher
{
    public static class WatcherTask
    {
        public static async Task OnedayTask()
        {
            LocalConsole.CreateNewLogFile();
            await LiverData.UpdateLivers();
            await YouTubeNotificationTask();
        }

        public static async Task PRTimesTask()
        {
            var groups = LiverGroup.GroupList;
            List<Task<List<PRTimesArticle>>> list = new(groups.Count);

            foreach (var group in groups) list.Add(PRTimesFeed.Instance.ReadFeed(group));
            foreach (var task in list)
            {
                var res = await task;
                if (res != null && res.Count != 0)
                {
                    foreach (var article in res)
                        await EventNotifier.Instance.Notify(new PRTimesNewArticleEvent(article));
                }
            }
        }

        public static async Task BoothTask()
        {
            var groups = LiverGroup.GroupList;
            List<Task<List<BoothProduct>>> list = new(groups.Count);

            foreach (var group in groups) list.Add(BoothWatcher.Instance.GetNewProduct(group));
            foreach (var task in list)
            {
                var res = await task;
                if (res != null && res.Count != 0)
                {
                    foreach (var product in res)
                    {
                        if (!product.IsOnSale) TimerManager.Instance.AddEventAlarm(product.StartDate, new BoothStartSellEvent(product));
                        await EventNotifier.Instance.Notify(new BoothNewProductEvent(product));
                    }
                }
            }
        }

        public static async Task NijisanjiStoreTask()
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
                            TimerManager.Instance.AddAlarm(dt, new(() => InnerTask(dt, product)));
                        }
                        else TimerManager.Instance.AddEventAlarm(product.StartDate, new NijisanjiStartSellEvent(product));
                    }
                    await EventNotifier.Instance.Notify(new NijisanjiNewProductEvent(product));
                }
            }

            async Task InnerTask(DateTime check, NijisanjiProduct product)
            {
                if (await NijisanjiWatcher.Instance.CheckOnSale(product))
                    await EventNotifier.Instance.Notify(new NijisanjiStartSellEvent(product));
                else
                {
                    var dt = DateTime.Today.AddHours(DateTime.Now.Hour + 1);
                    TimerManager.Instance.AddAlarm(dt, new(() => InnerTask(dt, product)));
                }
            }
        }

        public static async Task DotliveStoreTask()
        {
            var res = await DotliveWatcher.Instance.GetNewProduct();
            if (res != null && res.Count != 0)
            {
                foreach (DotliveProduct product in res)
                {
                    if (!product.IsOnSale)
                        TimerManager.Instance.AddEventAlarm(product.StartDate, new DotliveStartSellEvent(product));
                    await EventNotifier.Instance.Notify(new DotliveNewProductEvent(product));
                }
            }
        }

        public static async Task YouTubeChangeTask()
        {
            var list = YouTubeWatcher.Instance.CheckLiveChanged();
            foreach (var evt in list)
                if (evt != null) await EventNotifier.Instance.Notify(evt);
        }

        public static async Task YouTubeNotificationTask()
        {
            var list = new List<Address>(LiverData.GetAllLiversList()).Concat(LiverGroup.GroupList).Concat(LiveChannel.GetLiveChannelList());
            foreach (var address in list)
            {
                var id = address.YouTubeId;
                if (id == null) return;

                bool suc;
                int i = 0;
                do
                {
                    suc = await YouTubeWatcher.Instance.RegisterNotification(id);
                    if (suc)
                    {
                        LocalConsole.Log("NotificationRegister", new(LogSeverity.Info, null, $"Registerd: {id}"));
                    }
                    else
                    {
                        LocalConsole.Log("NotificationRegister", new(LogSeverity.Error, null, $"Missing Register: {id}"));
                        if (i < 4)
                        {
                            LocalConsole.Log("NotificationRegister", new(LogSeverity.Warning, null, $"Retrying..."));
                            await Task.Delay(1000);
                        }
                        else LocalConsole.Log("NotificationRegister", new(LogSeverity.Critical, null, $"Failed registration: {id}"));
                        i++;
                    }
                }
                while (!suc && i < 5);
            }
            LocalConsole.Log("NotificationRegister", new(LogSeverity.Info, null, $"Finish all registration task."));
        }

        public static async Task NicoLiveTask()
        {
            var livers = LiverData.GetAllLiversList();
            List<Task<List<NicoLiveItem>>> list = new(livers.Count);

            foreach (var liver in livers) list.Add(NicoLiveWatcher.Instance.SearchList(liver));
            foreach (var task in list)
            {
                var res = await task;
                if (res != null && res.Count != 0)
                {
                    foreach (var live in res)
                    {
                        TimerManager.Instance.AddAlarm(live.LiveStartDate, () => NicoLiveWatcher.Instance.CheckOnair(live.Id));
                        await EventNotifier.Instance.Notify(new NicoNewLiveEvent(live));
                    }
                }
            }
        }

        /*
        public static async Task TwitterTask()
        {
            var all = new List<Address>(LiverData.GetAllLiversList().Select(l => (Address)l).Concat(LiverGroup.GroupList));
            List<Task<List<Tweet>>> list = new(all.Count);

            for (int i = 0; i < all.Count; i++) list.Add(TwitterWatcher.Instance.GetNewTweets(all[i]));
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
                            await NotifyEvent.Notify(new YouTubeNewEvent.LiveEvent(video));
                        }
                    }
                }
            }
        }*/
    }
}
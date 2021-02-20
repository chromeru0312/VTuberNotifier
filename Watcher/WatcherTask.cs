using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VTuberNotifier.Discord;
using VTuberNotifier.Liver;
using VTuberNotifier.Watcher.Event;
using VTuberNotifier.Watcher.Feed;
using VTuberNotifier.Watcher.Store;

namespace VTuberNotifier.Watcher
{
    public class WatcherTask
    {
        public static WatcherTask Instance { get; private set; } = null;

        private WatcherTask()
        {
            TimerManager.CreateInstance();

            //PRTimes.CreateInstance();
            //TimerTaskManager.Instance.AddAction(20, PRTimesTask);
            NijisanjiWatcher.CreateInstance();
            TimerManager.Instance.AddAction(20 * 60, NijisanjiStoreTask);
            BoothWatcher.CreateInstance();
            TimerManager.Instance.AddAction(20 * 60, BoothTask);
            TwitterWatcher.CreateInstance();
            //TimerTaskManager.Instance.AddAction(20, TwitterTask);
            YouTubeFeed.CreateInstance();
            TimerManager.Instance.AddAction(20, YouTubeTask);

            DiscordBot.CreateInstance();
            Task.Run(DiscordBot.Instance.BotStart);
        }
        public static void CreateInstance()
        {
            if (Instance == null) Instance = new WatcherTask();
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
                    {
                        foreach (var liver in article.Livers)
                        {
                            await DiscordNotify.NotifyInformation(liver, new PRTimesNewArticleEvent(article));
                        }
                    }
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
                        if (!product.IsOnSale) TimerManager.Instance.AddAlarm(product.StartDate, new BoothStartSellEvent(product));
                        foreach (var liver in product.Livers)
                        {
                            await DiscordNotify.NotifyInformation(liver, new BoothNewProductEvent(product));
                        }
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
                    if (!product.IsOnSale) TimerManager.Instance.AddAlarm(product.StartDate, new NijisanjiStartSellEvent(product));
                    foreach (var liver in product.Livers)
                    {
                        await DiscordNotify.NotifyInformation(liver, new NijisanjiNewProductEvent(product));
                    }
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
                            foreach (var liver in livers)
                            {
                                await DiscordNotify.NotifyInformation(liver, new YouTubeNewLiveEvent(video));
                            }
                        }
                    }
                }
            }
        }

        public async Task YouTubeTask()
        {
            var all = new List<Address>(LiverData.GetAllLiversList().Select(l => (Address)l).Concat(LiverGroup.GroupList));
            List<Task<List<YouTubeItem>>> list = new(all.Count);

            for (int i = 0; i < all.Count; i++)
            {
                list.Add(YouTubeFeed.Instance.ReadFeed(all[i]));
            }
            for (int i = 0; i < all.Count; i++)
            {
                var res = await list[i];
                if (res != null && res.Count != 0)
                {
                    foreach (var video in res)
                    {
                        if (video.Mode != YouTubeItem.YouTubeMode.Video)
                            TimerManager.Instance.AddAlarm(video.LiveStartDate, new YouTubeStartLiveEvent(video));
                        foreach (var liver in video.Livers)
                        {
                            await DiscordNotify.NotifyInformation(liver, new YouTubeNewLiveEvent(video));
                        }
                    }
                }
            }
        }

        public async Task OneDayTask()
        {
            LocalConsole.CreateNewLogFile();
            await LiverData.UpdateLivers();
        }
    }
}

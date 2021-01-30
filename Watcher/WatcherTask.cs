using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VTuberNotifier.Discord;
using VTuberNotifier.Liver;
using VTuberNotifier.Watcher.Feed;
using VTuberNotifier.Watcher.Store;
//using static Discord.LogSeverity;

namespace VTuberNotifier.Watcher
{
    public class WatcherTask
    {
        public static WatcherTask Instance { get; private set; } = null;

        private WatcherTask()
        {
            TimerTaskManager.CreateInstance();

            //PRTimes.CreateInstance();
            //TimerTaskManager.Instance.AddAction(20, PRTimesTask);
            NijisanjiWatcher.CreateInstance();
            TimerTaskManager.Instance.AddAction(20 * 60, NijisanjiStoreTask);
            BoothWatcher.CreateInstance();
            TimerTaskManager.Instance.AddAction(20 * 60, BoothTask);
            TwitterWatcher.CreateInstance();
            //TimerTaskManager.Instance.AddAction(20, TwitterTask);
            YouTubeFeed.CreateInstance();
            TimerTaskManager.Instance.AddAction(20, YouTubeTask);

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
                            await DiscordNotify.NotifyInformation(liver, article);
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
                        foreach (var liver in product.Livers)
                        {
                            await DiscordNotify.NotifyInformation(liver, product);
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
                foreach (var product in res)
                {
                    foreach (var liver in product.Livers)
                    {
                        await DiscordNotify.NotifyInformation(liver, product);
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
                        foreach (var url in tweet.URLs)
                        {
                            string id;
                            if (url.ExpandedURL.StartsWith("https://www.youtube.com/"))
                            {
                                id = url.ExpandedURL[(url.ExpandedURL.IndexOf("v=") + 2)..];
                                var index = id.IndexOf('&');
                                if (index != -1) id = id[..index];
                            }
                            else if (url.ExpandedURL.StartsWith("https://youtu.be/"))
                            {
                                id = url.ExpandedURL[17..];
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
                                await DiscordNotify.NotifyInformation(liver, video);
                            }
                        }
                    }
                }
            }
        }

        public async Task YouTubeTask()
        {
            //LocalConsole.Log("YouTubeTask", new(Debug, "Notify", "Task Start."));
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
                        foreach (var liver in video.Livers)
                        {
                            await DiscordNotify.NotifyInformation(liver, video);
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

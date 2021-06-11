using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using VTuberNotifier.Liver;
using VTuberNotifier.Notification.Discord;
using VTuberNotifier.Watcher.Event;

namespace VTuberNotifier.Notification
{
    public class NotifyEvent
    {
        public static IReadOnlyDictionary<LiverDetail, IReadOnlyList<DiscordChannel>> NotifyDiscordList { get; private set; } = null;
        public static IReadOnlyDictionary<LiverDetail, IReadOnlyList<WebhookDestination>> NotifyWebhookList { get; private set; } = null;
        public static IReadOnlyDictionary<IEventBase, IReadOnlyList<long>> NotifyDiscordMsgs { get; private set; } = null;

        internal static void LoadChannelList()
        {
            if (NotifyDiscordList != null) return;
            static Dictionary<LiverDetail, IReadOnlyList<T>> empty_func<T>()
                => new(LiverData.GetAllLiversList().Select(l => new KeyValuePair<LiverDetail, IReadOnlyList<T>>(l, new List<T>())));
            static Dictionary<LiverDetail, IReadOnlyList<T>> data_func<T>(IEnumerable<KeyValuePair<int, List<T>>> dic)
                => new ( dic.Select(p => new KeyValuePair<LiverDetail, IReadOnlyList<T>>(LiverData.GetAllLiversList().First(l => l.Id == p.Key), p.Value)));

            if (DataManager.Instance.TryDataLoad("NotifyDiscordList", out IEnumerable<KeyValuePair<int, List<DiscordChannel>>> ddic))
                NotifyDiscordList = data_func(ddic);
            else NotifyDiscordList = empty_func<DiscordChannel>();

            if (DataManager.Instance.TryDataLoad("NotifyWebhookList", out IEnumerable<KeyValuePair<int, List<WebhookDestination>>> wdic))
                NotifyWebhookList = data_func(wdic);
            else NotifyWebhookList = empty_func<WebhookDestination>();
        }

        public static async Task Notify<T>(EventBase<T> value) where T : INotificationContent
        {
            foreach (var liver in value.Item.Livers)
            {
                if (NotifyDiscordList.ContainsKey(liver))
                {
                    foreach (var dc in NotifyDiscordList[liver])
                    {
                        if (!dc.GetContent(value.GetType(), out var only, out var content)) continue;
                        var l = only ? liver : null;
                        content = content != "" ? value.ConvertContent(content, l) : value.GetDiscordContent(l);

                        var guild = Settings.Data.DiscordClient.GetGuild(dc.GuildId);
                        var ch = guild.GetTextChannel(dc.ChannelId);
                        await ch.SendMessageAsync(content);
                    }
                }
                if (NotifyWebhookList.ContainsKey(liver))
                {
                    foreach (var wd in NotifyWebhookList[liver])
                    {
                        if (!wd.GetContent(value.GetType(), out var only, out var content)) continue;
                        var l = only ? liver : null;
                        content = content != "" ? value.ConvertContent(content, l) : value.GetDiscordContent(l);

                        try
                        {
                            using var req = new HttpRequestMessage(HttpMethod.Post, wd.Url);
                            req.Headers.Add("UserAgent", "VInfoNotifier (ASP.NET 5.0 / Ubuntu 20.04) [@chromeru0312]");
                            req.Headers.Add("Content-Type", "application/json;charset=UTF-8");
                            req.Headers.Add("Accept", "application/json");
                            req.Content = new StringContent(wd.ConvertContentToJson(content));
                            await Settings.Data.HttpClient.SendAsync(req);
                        }
                        catch { }
                    }
                }
            }
            var now = DateTime.Now;
            var id = $"event/{now:yyyyMMdd}/{value.GetType().Name}_{now:HHmmssff}({value.Item.Id})";
            if (value is YouTubeChangeEvent ci) DataManager.Instance.DataSave(id, ci);
            else DataManager.Instance.DataSave(id, value);
        }

        public static bool AddDiscordList(LiverDetail liver, DiscordChannel channel)
        {
            var list = new List<DiscordChannel>(NotifyDiscordList[liver]);
            if (AddNotifyList(channel, ref list))
            {
                NotifyDiscordList = new Dictionary<LiverDetail, IReadOnlyList<DiscordChannel>>(NotifyDiscordList) { [liver] = list };
                SaveDiscordList();
                return true;
            }
            else return false;
        }
        public static bool AddWebhookList(LiverDetail liver, WebhookDestination destination)
        {
            var list = new List<WebhookDestination>(NotifyWebhookList[liver]);
            if (AddNotifyList(destination, ref list))
            {
                NotifyWebhookList = new Dictionary<LiverDetail, IReadOnlyList<WebhookDestination>>(NotifyWebhookList) { [liver] = list };
                SaveWebhookList();
                return true;
            }
            else return false;
        }
        private static bool AddNotifyList<T>(T value, ref List<T> list) where T : NotificationAddress
        {
            if (value == null) return false;
            if (list.Contains(value))
            {
                var old = list.FirstOrDefault(d => value.Equals(d));
                list.Remove(old);
                foreach (var type in value.MsgContentList.Keys)
                    if (value.GetContent(type, out var b, out var c)) old.AddContent(type, b, c);
                value = old;
            }
            list.Add(value);
            return true;
        }

        public static bool UpdateDiscordList(LiverDetail liver, DiscordChannel channel)
        {
            var list = new List<DiscordChannel>(NotifyDiscordList[liver]);
            if (UpdateNotifyList(channel, ref list))
            {
                NotifyDiscordList = new Dictionary<LiverDetail, IReadOnlyList<DiscordChannel>>(NotifyDiscordList) { [liver] = list };
                SaveDiscordList();
                return true;
            }
            else return false;
        }
        public static bool UpdateWebhookList(LiverDetail liver, WebhookDestination destination)
        {
            var list = new List<WebhookDestination>(NotifyWebhookList[liver]);
            if (UpdateNotifyList(destination, ref list))
            {
                NotifyWebhookList = new Dictionary<LiverDetail, IReadOnlyList<WebhookDestination>>(NotifyWebhookList) { [liver] = list };
                SaveWebhookList();
                return true;
            }
            else return false;
        }
        private static bool UpdateNotifyList<T>(T value, ref List<T> list) where T : NotificationAddress
        {
            if (value == null || !list.Contains(value)) return false;
            var old = list.FirstOrDefault(c => value.Equals(c));
            list.Remove(old);
            foreach (var type in value.MsgContentList.Keys)
                if (value.GetContent(type, out var b, out var c)) old.SetContent(type, b, c);
            list.Add(old);
            return true;
        }

        public static bool RemoveDiscordList(LiverDetail liver, DiscordChannel channel)
        {
            var list = new List<DiscordChannel>(NotifyDiscordList[liver]);
            if (RemoveNotifyList(channel, ref list))
            {
                NotifyDiscordList = new Dictionary<LiverDetail, IReadOnlyList<DiscordChannel>>(NotifyDiscordList) { [liver] = list };
                SaveDiscordList();
                return true;
            }
            else return false;
        }
        public static bool RemoveWebhookList(LiverDetail liver, WebhookDestination destination)
        {
            var list = new List<WebhookDestination>(NotifyWebhookList[liver]);
            if (RemoveNotifyList(destination, ref list))
            {
                NotifyWebhookList = new Dictionary<LiverDetail, IReadOnlyList<WebhookDestination>>(NotifyWebhookList) { [liver] = list };
                SaveWebhookList();
                return true;
            }
            else return false;
        }
        private static bool RemoveNotifyList<T>(T value, ref List<T> list) where T : NotificationAddress
        {
            if (value == null || !list.Contains(value)) return false;
            list.Remove(value);
            return true;
        }

        private static void SaveDiscordList()
        {
            var data = NotifyDiscordList.Select(p => new KeyValuePair<int, List<DiscordChannel>>(p.Key.Id, new(p.Value)));
            DataManager.Instance.DataSave("NotifyDiscordList", data, true);
        }
        private static void SaveWebhookList()
        {
            var data = NotifyWebhookList.Select(p => new KeyValuePair<int, List<WebhookDestination>>(p.Key.Id, new(p.Value)));
            DataManager.Instance.DataSave("NotifyWebhookList", data, true);
        }

        public static bool DetectTypes(LiverDetail liver, out Type[] types, params string[] servs)
        {
            var list = new List<Type>();
            types = null;
            if (servs.Length == 0) return false;

            foreach (var s in servs)
            {
                if (s == "youtube")
                    list.AddRange(new List<Type>()
                    {
                        typeof(YouTubeNewEvent.LiveEvent), typeof(YouTubeNewEvent.PremireEvent),
                        typeof(YouTubeNewEvent.VideoEvent), typeof(YouTubeChangeEvent.DateEvent),
                        typeof(YouTubeChangeEvent.LiverEvent), typeof(YouTubeChangeEvent.OtherEvent),
                        typeof(YouTubeDeleteLiveEvent), typeof(YouTubeStartLiveEvent)
                    });
                else if (s == "youtube_new")
                    list.AddRange(new List<Type>()
                    {
                        typeof(YouTubeNewEvent.LiveEvent), typeof(YouTubeNewEvent.PremireEvent),
                        typeof(YouTubeNewEvent.VideoEvent)
                    });
                else if (s == "youtube_change")
                    list.AddRange(new List<Type>()
                    {
                        typeof(YouTubeChangeEvent.DateEvent), typeof(YouTubeChangeEvent.LiverEvent),
                        typeof(YouTubeChangeEvent.OtherEvent)
                    });
                else if (s == "booth" && liver.Group.IsExistBooth)
                    list.AddRange(new List<Type>()
                    {
                        typeof(BoothNewProductEvent), typeof(BoothStartSellEvent)
                    });
                else if (s == "store" && liver.Group.IsExistStore)
                    list.AddRange(new List<Type>()
                    {
                        liver.Group.StoreInfo.NewProductEventType, liver.Group.StoreInfo.StartSaleEventType
                    });
                else if (DetectType(liver, out var t, s)) list.Add(t);
                else return false;
            }
            types = list.Distinct().ToArray();
            return true;
        }
        public static bool DetectType(LiverDetail liver, out Type type, string serv)
        {
            if ((serv.StartsWith("booth") && !liver.Group.IsExistBooth) ||
                (serv.StartsWith("store") && !liver.Group.IsExistStore))
            {
                type = null;
                return false;
            }
            type = serv switch
            {
                "youtube_new_live" => typeof(YouTubeNewEvent.LiveEvent),
                "youtube_new_premiere" => typeof(YouTubeNewEvent.PremireEvent),
                "youtube_new_video" => typeof(YouTubeNewEvent.VideoEvent),
                "youtube_change_date" => typeof(YouTubeChangeEvent.DateEvent),
                "youtube_change_liver" => typeof(YouTubeChangeEvent.LiverEvent),
                "youtube_change_other" => typeof(YouTubeChangeEvent.OtherEvent),
                "youtube_delete" => typeof(YouTubeDeleteLiveEvent),
                "youtube_start" => typeof(YouTubeStartLiveEvent),
                "booth_new" => typeof(BoothNewProductEvent),
                "booth_start" => typeof(BoothStartSellEvent),
                "store_new" => liver.Group.StoreInfo.NewProductEventType,
                "store_start" => liver.Group.StoreInfo.StartSaleEventType,
                "article" => typeof(PRTimesNewArticleEvent),
                _ => null
            };
            return type != null;
        }
    }
}

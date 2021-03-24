using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
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
                => new Dictionary<LiverDetail, IReadOnlyList<T>>(
                    LiverData.GetAllLiversList().Select(l => new KeyValuePair<LiverDetail, IReadOnlyList<T>>(l, new List<T>())));
            static Dictionary<LiverDetail, IReadOnlyList<T>> data_func<T>(IEnumerable<KeyValuePair<int, List<T>>> dic)
                => new Dictionary<LiverDetail, IReadOnlyList<T>>(
                    dic.Select(p => new KeyValuePair<LiverDetail, IReadOnlyList<T>>(LiverData.GetAllLiversList().First(l => l.Id == p.Key), p.Value)));

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

                        var guild = SettingData.DiscordClient.GetGuild(dc.GuildId);
                        var ch = guild.GetTextChannel(dc.ChannelId);
                        await ch.SendMessageAsync(content);
                    }
                }
                if (NotifyWebhookList.ContainsKey(liver))
                {
                    using var wc = new WebClient() { Encoding = Encoding.UTF8 };
                    wc.Headers[HttpRequestHeader.ContentType] = "application/json;charset=UTF-8";
                    wc.Headers[HttpRequestHeader.Accept] = "application/json";
                    foreach (var wd in NotifyWebhookList[liver])
                    {
                        if (!wd.GetContent(value.GetType(), out var only, out var content)) continue;
                        var l = only ? liver : null;
                        content = content != "" ? value.ConvertContent(content, l) : value.GetDiscordContent(l);

                        try
                        {
                            await wc.UploadStringTaskAsync(wd.Url, wd.ConvertContentToJson(content));
                        }
                        catch (Exception) { }
                    }
                }
            }
            DataManager.Instance.DataSave($"event/notified-{DateTime.Now:yyyyMMddHHmmssffff}", value);
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
                        typeof(YouTubeNewLiveEvent), typeof(YouTubeNewPremireEvent),
                        typeof(YouTubeNewVideoEvent), typeof(YouTubeChangeInfoEvent),
                        typeof(YouTubeDeleteLiveEvent), typeof(YouTubeStartLiveEvent)
                    });
                else if (s == "youtube_new")
                    list.AddRange(new List<Type>()
                    {
                        typeof(YouTubeNewLiveEvent), typeof(YouTubeNewPremireEvent),
                        typeof(YouTubeNewVideoEvent)
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
            type = null;
            if (serv == "youtube_new_live") type = typeof(YouTubeNewLiveEvent);
            else if (serv == "youtube_new_video") type = typeof(YouTubeNewVideoEvent);
            else if (serv == "youtube_new_premiere") type = typeof(YouTubeNewPremireEvent);
            else if (serv == "youtube_change") type = typeof(YouTubeChangeInfoEvent);
            else if (serv == "youtube_delete") type = typeof(YouTubeDeleteLiveEvent);
            else if (serv == "youtube_start") type = typeof(YouTubeStartLiveEvent);
            else if (serv == "booth_new" && liver.Group.IsExistBooth)
                type = typeof(BoothNewProductEvent);
            else if (serv == "booth_start" && liver.Group.IsExistBooth)
                type = typeof(BoothStartSellEvent);
            else if (serv == "store_new" && liver.Group.IsExistStore)
                type = liver.Group.StoreInfo.NewProductEventType;
            else if (serv == "store_start")
                type = liver.Group.StoreInfo.StartSaleEventType;
            else return false;
            return true;
        }
    }
}

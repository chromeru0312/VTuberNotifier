using Discord;
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
    public class EventNotifier
    {
        public static EventNotifier Instance { get; private set; } = null;

        public IReadOnlyDictionary<LiverDetail, IReadOnlyList<DiscordChannel>> NotifyDiscordList { get; private set; }
        public IReadOnlyDictionary<LiverDetail, IReadOnlyList<WebhookDestination>> NotifyWebhookList { get; private set; }
        private Dictionary<string, List<(DiscordChannel, ulong)>> NotifiedYouTubeMessages { get; }

        private EventNotifier()
        {
            if (NotifyDiscordList != null) return;
            static Dictionary<LiverDetail, IReadOnlyList<T>> empty_func<T>()
                => new(LiverData.GetAllLiversList().Select(l =>
                    new KeyValuePair<LiverDetail, IReadOnlyList<T>>(l, new List<T>())));
            static Dictionary<LiverDetail, IReadOnlyList<T>> data_func<T>(IEnumerable<KeyValuePair<int, List<T>>> dic)
                => new(dic.Select(p =>
                    new KeyValuePair<LiverDetail, IReadOnlyList<T>>(LiverData.GetLiverFromId(p.Key), p.Value)));

            if (DataManager.Instance.TryDataLoad("NotifyDiscordList", out IEnumerable<KeyValuePair<int, List<DiscordChannel>>> ddic))
                NotifyDiscordList = data_func(ddic);
            else NotifyDiscordList = empty_func<DiscordChannel>();

            if (DataManager.Instance.TryDataLoad("NotifyWebhookList", out IEnumerable<KeyValuePair<int, List<WebhookDestination>>> wdic))
                NotifyWebhookList = data_func(wdic);
            else NotifyWebhookList = empty_func<WebhookDestination>();

            if (DataManager.Instance.TryDataLoad("NotifiedDiscordMessages", out Dictionary<string, List<(DiscordChannel, ulong)>> msgs))
                NotifiedYouTubeMessages = msgs;
            else NotifiedYouTubeMessages = new();
        }
        public static void CreateInstance()
        {
            if (Instance == null) Instance = new();
        }

        public async Task Notify<T>(EventBase<T> value) where T : INotificationContent
        {
            if (value == null) return;
            var id = value.GetContainsItem().Id;
            if (value is YouTubeAlradyLivedEvent)
            {
                NotifiedYouTubeMessages.Remove(id);
                return;
            }
            var now = DateTime.Now;

            if (!NotifiedYouTubeMessages.TryGetValue(id, out var msgs))
                msgs = new();
            foreach (var (liver, evts) in value.EventsByLiver)
            {
                foreach (var evt in evts)
                {
                    if (NotifyDiscordList.ContainsKey(liver))
                    {
                        foreach (var dc in NotifyDiscordList[liver])
                        {
                            var guild = Settings.Data.DiscordClient.GetGuild(dc.GuildId);
                            var ch = guild.GetTextChannel(dc.ChannelId);
                            var tuple = msgs.FirstOrDefault(p => p.Item1 == dc);
                            if (dc.IsEditContent && tuple.Item1 != null &&
                                (value is YouTubeChangeEvent || value is YouTubeDeleteLiveEvent))
                            {
                                if (await ch.GetMessageAsync(tuple.Item2) is not IUserMessage msg)
                                    continue;

                                string notify;
                                if (value is YouTubeChangeEvent)
                                {
                                    notify = "Information updated.";
                                    if (!dc.GetContent(typeof(YouTubeEvent), out var only, out var content)) continue;
                                    var l = only ? liver : null;
                                    content = content != "" ? evt.ConvertContent(content, l) : evt.GetDiscordContent(l);

                                    if (msg.Content != content)
                                        await msg.ModifyAsync(m => m.Content = content);
                                }
                                else if (value is YouTubeDeleteLiveEvent)
                                {
                                    notify = "Live deleted.";
                                    var content = $"~~{msg.Content.Replace("\n", "~~\n~~")}~~".Replace("~~~~", "");
                                    await msg.ModifyAsync(m => m.Content = content);
                                }
                                else throw new InvalidOperationException();

                                if (dc.MsgContentList.ContainsKey(evt.GetType()))
                                    await ch.SendMessageAsync(notify, messageReference: new(messageId: tuple.Item2));
                            }
                            else
                            {
                                if (!dc.GetContent(evt.GetType(), out var only, out var content)) continue;
                                var l = only ? liver : null;
                                content = content != "" ? evt.ConvertContent(content, l) : evt.GetDiscordContent(l);

                                var msg = await ch.SendMessageAsync(content);
                                if (dc.IsEditContent) msgs.Add((dc, msg.Id));
                            }
                        }
                    }
                    if (NotifyWebhookList.ContainsKey(liver))
                    {
                        foreach (var wd in NotifyWebhookList[liver])
                        {
                            if (!wd.GetContent(evt.GetType(), out var only, out var content)) continue;
                            var l = only ? liver : null;
                            content = content != "" ? evt.ConvertContent(content, l) : evt.GetDiscordContent(l);

                            using var req = new HttpRequestMessage(HttpMethod.Post, wd.Url);
                            req.Headers.Add("UserAgent", "VInfoNotifier");
                            req.Headers.Add("Content-Type", "application/json;charset=UTF-8");
                            req.Headers.Add("Accept", "application/json");
                            req.Content = new StringContent(wd.ConvertContentToJson(content));
                            await Settings.Data.HttpClient.SendAsync(req);
                        }
                    }
                }
            }

            if (value is YouTubeNewEvent && !NotifiedYouTubeMessages.ContainsKey(id))
            {
                NotifiedYouTubeMessages.Add(id, msgs);
                await DataManager.Instance.DataSaveAsync("NotifiedDiscordMessages", NotifiedYouTubeMessages, true);
            }
            else if (value is YouTubeDeleteLiveEvent)
            {
                NotifiedYouTubeMessages.Remove(id);
            }
            else if (value is YouTubeStartLiveEvent)
            {
                NotifiedYouTubeMessages.Remove(id);
            }
            await DataManager.Instance.DataSaveAsync($"event/{now:yyyyMMdd/HHmmss}_{id}[{value.EventTypeName}]", value);
        }

        public bool AddDiscordList(LiverDetail liver, DiscordChannel channel)
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
        public bool AddWebhookList(LiverDetail liver, WebhookDestination destination)
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

        public bool UpdateDiscordList(LiverDetail liver, DiscordChannel channel)
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
        public bool UpdateWebhookList(LiverDetail liver, WebhookDestination destination)
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

        public bool RemoveDiscordList(LiverDetail liver, DiscordChannel channel)
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
        public bool RemoveWebhookList(LiverDetail liver, WebhookDestination destination)
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

        private void SaveDiscordList()
        {
            var data = NotifyDiscordList.Select(p => new KeyValuePair<int, List<DiscordChannel>>(p.Key.Id, new(p.Value)));
            DataManager.Instance.DataSave("NotifyDiscordList", data, true);
        }
        private void SaveWebhookList()
        {
            var data = NotifyWebhookList.Select(p => new KeyValuePair<int, List<WebhookDestination>>(p.Key.Id, new(p.Value)));
            DataManager.Instance.DataSave("NotifyWebhookList", data, true);
        }

        public bool DetectTypes(LiverDetail liver, out Type[] types, params string[] servs)
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
                        typeof(YouTubeNewEvent.VideoEvent), typeof(YouTubeChangeEvent.TitleEvent),
                        typeof(YouTubeChangeEvent.DateEvent), typeof(YouTubeChangeEvent.LiverEvent),
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
                        typeof(YouTubeChangeEvent.TitleEvent), typeof(YouTubeChangeEvent.DateEvent),
                        typeof(YouTubeChangeEvent.LiverEvent)
                    });
                else if (s == "youtube_change_all")
                    list.AddRange(new List<Type>()
                    {
                        typeof(YouTubeChangeEvent.TitleEvent), typeof(YouTubeChangeEvent.DescriptionEvent),
                        typeof(YouTubeChangeEvent.DateEvent), typeof(YouTubeChangeEvent.LiverEvent)
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
        public bool DetectType(LiverDetail liver, out Type type, string serv)
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
                "youtube_change_title" => typeof(YouTubeChangeEvent.TitleEvent),
                "youtube_change_desc" => typeof(YouTubeChangeEvent.DescriptionEvent),
                "youtube_change_date" => typeof(YouTubeChangeEvent.DateEvent),
                "youtube_change_liver" => typeof(YouTubeChangeEvent.LiverEvent),
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
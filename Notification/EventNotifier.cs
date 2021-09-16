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

            if (DataManager.Instance.TryDataLoad("NotifyDiscordList", out IEnumerable<KeyValuePair<int, List<DiscordChannel>>> ddic))
                NotifyDiscordList = data_func(ddic);
            else NotifyDiscordList = empty_func<DiscordChannel>();

            if (DataManager.Instance.TryDataLoad("NotifyWebhookList", out IEnumerable<KeyValuePair<int, List<WebhookDestination>>> wdic))
                NotifyWebhookList = data_func(wdic);
            else NotifyWebhookList = empty_func<WebhookDestination>();

            if (DataManager.Instance.TryDataLoad("NotifiedDiscordMessages", out Dictionary<string, List<(DiscordChannel, ulong)>> msgs))
                NotifiedYouTubeMessages = msgs;
            else NotifiedYouTubeMessages = new();


            static Dictionary<LiverDetail, IReadOnlyList<T>> empty_func<T>() => new(LiverData.GetAllLiversList().Select(l =>
                    new KeyValuePair<LiverDetail, IReadOnlyList<T>>(l, new List<T>())));
            static Dictionary<LiverDetail, IReadOnlyList<T>> data_func<T>(IEnumerable<KeyValuePair<int, List<T>>> dic)
            {
                var data = new Dictionary<LiverDetail, IReadOnlyList<T>>();
                foreach (var (id, list) in dic)
                {
                    var liver = LiverData.GetLiverFromId(id);
                    if (liver != null) data.Add(liver, list);
                }
                return data;
            }
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
            else if (value.EventsByLiver == null)
            {
                LocalConsole.Log(this, new(LogSeverity.Error, "Notifier", $"EvetsByLiver is null : {value.GetType()}"));
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

        private static readonly Type[] YouTubeNew
            = new[]
            {
                typeof(YouTubeNewEvent.LiveEvent.SelfEvent),
                typeof(YouTubeNewEvent.PremireEvent.SelfEvent),
                typeof(YouTubeNewEvent.VideoEvent.SelfEvent),
                typeof(YouTubeNewEvent.LiveEvent.CollaborationEvent),
                typeof(YouTubeNewEvent.PremireEvent.CollaborationEvent),
                typeof(YouTubeNewEvent.VideoEvent.CollaborationEvent),
            };
        private static readonly Type[] YouTubeChange
            = new[]
            {
                typeof(YouTubeChangeEvent.TitleEvent.SelfEvent),
                typeof(YouTubeChangeEvent.DescriptionEvent.SelfEvent),
                typeof(YouTubeChangeEvent.DateEvent.SelfEvent),
                typeof(YouTubeChangeEvent.LiverEvent.SelfEvent),
                typeof(YouTubeChangeEvent.TitleEvent.CollaborationEvent),
                typeof(YouTubeChangeEvent.DescriptionEvent.CollaborationEvent),
                typeof(YouTubeChangeEvent.DateEvent.CollaborationEvent),
                typeof(YouTubeChangeEvent.LiverEvent.CollaborationEvent)
            };
        private static readonly Type[] YouTubeRecomenndedChange
            = new[]
            {
                typeof(YouTubeChangeEvent.DateEvent.SelfEvent),
                typeof(YouTubeChangeEvent.LiverEvent.SelfEvent),
                typeof(YouTubeChangeEvent.DateEvent.CollaborationEvent),
                typeof(YouTubeChangeEvent.LiverEvent.CollaborationEvent)
            };
        private static readonly Type[] YouTubeDeleteLive
            = new[]
            {
                typeof(YouTubeDeleteLiveEvent.SelfEvent),
                typeof(YouTubeDeleteLiveEvent.CollaborationEvent),
            };
        private static readonly Type[] YouTubeStartLive
            = new[]
            {
                typeof(YouTubeStartLiveEvent.SelfEvent),
                typeof(YouTubeStartLiveEvent.CollaborationEvent),
            };

        public bool DetectTypes(LiverDetail liver, out Type[] types, params string[] servs)
        {
            var list = new List<Type>();
            types = null;
            if (servs.Length == 0) return false;

            foreach (var s in servs)
            {
                if (s == "youtube")
                    list.AddRange(YouTubeNew[..].Union(YouTubeChange[..]).Union(YouTubeDeleteLive[..]).Union(YouTubeStartLive[..]));
                else if (s == "youtube_self")
                    list.AddRange(YouTubeNew[..3].Union(YouTubeChange[..4]).Append(YouTubeDeleteLive[0]).Append(YouTubeStartLive[0]));
                else if (s == "youtube_collaboration")
                    list.AddRange(YouTubeNew[3..].Union(YouTubeChange[4..]).Append(YouTubeDeleteLive[1]).Append(YouTubeStartLive[1]));
                else if (s == "youtube_recommended")
                    list.AddRange(YouTubeNew[..].Union(YouTubeRecomenndedChange[..]));
                else if (s == "youtube_recommended_self")
                    list.AddRange(YouTubeNew[..3].Union(YouTubeRecomenndedChange[..2]));
                else if (s == "youtube_recommended_collaboration")
                    list.AddRange(YouTubeNew[3..].Union(YouTubeRecomenndedChange[2..]));
                else if (s == "youtube_new")
                    list.AddRange(YouTubeNew[..]);
                else if (s == "youtube_new_self")
                    list.AddRange(YouTubeNew[..3]);
                else if (s == "youtube_new_collaboration")
                    list.AddRange(YouTubeNew[3..]);
                else if (s == "youtube_new_live")
                    list.AddRange(new List<Type>() { YouTubeNew[0], YouTubeNew[3] });
                else if (s == "youtube_new_premire")
                    list.AddRange(new List<Type>() { YouTubeNew[1], YouTubeNew[4] });
                else if (s == "youtube_new_video")
                    list.AddRange(new List<Type>() { YouTubeNew[2], YouTubeNew[5] });
                else if (s == "youtube_change")
                    list.AddRange(YouTubeChange[..]);
                else if (s == "youtube_change_self")
                    list.AddRange(YouTubeChange[..4]);
                else if (s == "youtube_change_collaboration")
                    list.AddRange(YouTubeChange[4..]);
                else if (s == "youtube_new_title")
                    list.AddRange(new List<Type>() { YouTubeChange[0], YouTubeChange[4] });
                else if (s == "youtube_new_desc")
                    list.AddRange(new List<Type>() { YouTubeChange[1], YouTubeChange[5] });
                else if (s == "youtube_new_date")
                    list.AddRange(new List<Type>() { YouTubeChange[2], YouTubeChange[6] });
                else if (s == "youtube_new_liver")
                    list.AddRange(new List<Type>() { YouTubeChange[3], YouTubeChange[7] });
                else if (s == "youtube_change_recommended")
                    list.AddRange(YouTubeRecomenndedChange[..]);
                else if (s == "youtube_change_recommended_self")
                    list.AddRange(YouTubeRecomenndedChange[..2]);
                else if (s == "youtube_change_recommended_collaboration")
                    list.AddRange(YouTubeRecomenndedChange[2..]);
                else if (s == "youtube_delete")
                    list.AddRange(YouTubeDeleteLive);
                else if (s == "youtube_start")
                    list.AddRange(YouTubeStartLive);
                else if (s == "nicolive")
                    list.AddRange(new List<Type>() { typeof(NicoNewLiveEvent), typeof(NicoStartLiveEvent) });
                else if (s == "booth" && liver.Group.IsExistBooth)
                    list.AddRange(new List<Type>() { typeof(BoothNewProductEvent), typeof(BoothStartSellEvent) });
                else if (s == "store" && liver.Group.IsExistStore)
                    list.AddRange(new List<Type>() { liver.Group.StoreInfo.NewProductEventType, liver.Group.StoreInfo.StartSaleEventType });
                else if (DetectType(liver, out var t, s)) list.Add(t);
                else return false;
            }
            types = list.Distinct().ToArray();
            return true;
        }
        public bool DetectType(LiverDetail liver, out Type type, string serv)
        {
            if ((serv.StartsWith("booth") && !liver.Group.IsExistBooth) || (serv.StartsWith("store") && !liver.Group.IsExistStore))
            {
                type = null;
                return false;
            }
            type = serv switch
            {
                "youtube_new_live_self"
                    => typeof(YouTubeNewEvent.LiveEvent.SelfEvent),
                "youtube_new_live_collaboration"
                    => typeof(YouTubeNewEvent.LiveEvent.CollaborationEvent),
                "youtube_new_premiere_self"
                    => typeof(YouTubeNewEvent.PremireEvent.SelfEvent),
                "youtube_new_premiere_collaboration"
                    => typeof(YouTubeNewEvent.PremireEvent.CollaborationEvent),
                "youtube_new_video_self"
                    => typeof(YouTubeNewEvent.VideoEvent.SelfEvent),
                "youtube_new_video_collaboration"
                    => typeof(YouTubeNewEvent.VideoEvent.CollaborationEvent),
                "youtube_change_title_self"
                    => typeof(YouTubeChangeEvent.TitleEvent.SelfEvent),
                "youtube_change_title_collaboration"
                    => typeof(YouTubeChangeEvent.TitleEvent.CollaborationEvent),
                "youtube_change_desc_self"
                    => typeof(YouTubeChangeEvent.DescriptionEvent.SelfEvent),
                "youtube_change_desc_collaboration"
                    => typeof(YouTubeChangeEvent.DescriptionEvent.CollaborationEvent),
                "youtube_change_date_self"
                    => typeof(YouTubeChangeEvent.DateEvent.SelfEvent),
                "youtube_change_date_collaboration"
                    => typeof(YouTubeChangeEvent.DateEvent.CollaborationEvent),
                "youtube_change_liver_self"
                    => typeof(YouTubeChangeEvent.LiverEvent.SelfEvent),
                "youtube_change_liver_collaboration"
                    => typeof(YouTubeChangeEvent.LiverEvent.CollaborationEvent),
                "youtube_delete_self"
                    => typeof(YouTubeDeleteLiveEvent.SelfEvent),
                "youtube_delete_collaboration"
                    => typeof(YouTubeDeleteLiveEvent.CollaborationEvent),
                "youtube_start_self"
                    => typeof(YouTubeStartLiveEvent.SelfEvent),
                "youtube_start_collaboration"
                    => typeof(YouTubeStartLiveEvent.CollaborationEvent),
                "nicolive_new"
                    => typeof(NicoNewLiveEvent),
                "nicolive_start"
                    => typeof(NicoStartLiveEvent),
                "booth_new"
                    => typeof(BoothNewProductEvent),
                "booth_start"
                    => typeof(BoothStartSellEvent),
                "store_new"
                    => liver.Group.StoreInfo.NewProductEventType,
                "store_start"
                    => liver.Group.StoreInfo.StartSaleEventType,
                "article"
                    => typeof(PRTimesNewArticleEvent),
                _ => null
            };
            return type != null;
        }
    }
}
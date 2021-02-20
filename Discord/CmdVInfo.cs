using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VTuberNotifier.Liver;
using VTuberNotifier.Watcher.Event;

namespace VTuberNotifier.Discord
{
    public class CmdVInfo : CmdBase
    {
        [Command("vinfo add")]
        public async Task AddNotify(string liver, string services, bool only = true)
        {
            if (liver == null || liver == "")
            {
                await SendError(this, 3, "Argument is empty.");
                return;
            }
            if (services == null || services == "")
            {
                await SendError(this, 4, "Argument is empty.");
                return;
            }

            LiverDetail detail = SearchLiver(liver);
            if (detail == null)
            {
                await SendError(this, 3, "The specified river cannot be found.");
                return;
            }
            var ch = new DiscordChannel(Context.Guild.Id, Context.Channel.Id);
            if (!DetectTypes(detail, out var types, services.Split(',')))
            {
                await SendError(this, 4, "This service is not supported/found.");
                return;
            }
            foreach (var t in types) ch.SetContent(t, only, null);
            if (DiscordNotify.AddNotifyList(detail, ch)) await ReplyAsync("Success.");
            else await SendError(this, 2, "This channel is alrady added.");
        }

        [Command("vinfo set")]
        public async Task SetContent(string liver, string service, bool only = true, string content = null)
        {
            if (liver == null || liver == "")
            {
                await SendError(this, 3, "Argument is empty.");
                return;
            }
            if (service == null || service == "")
            {
                await SendError(this, 4, "Argument is empty.");
                return;
            }
            if (content == null || content == "" || content == "default")
                content = null;

            LiverDetail detail = SearchLiver(liver);
            if (detail == null)
            {
                await SendError(this, 3, "The specified river cannot be found.");
                return;
            }
            var ch = new DiscordChannel(Context.Guild.Id, Context.Channel.Id);
            if (!DetectType(detail, out var type, service))
            {
                await SendError(this, 4, "This service is not supported/found.");
                return;
            }
            ch.SetContent(type, only, content);

            if (DiscordNotify.UpdateNotifyList(detail, ch)) await ReplyAsync("Success.");
            else await SendError(this, 2, "This channel is not alrady added.");
        }

        [Command("vinfo remove")]
        public async Task RemoveNotify(string liver, string services = null)
        {
            if (liver == null || liver == "")
            {
                await SendError(this, 3, "Argument is empty.");
                return;
            }

            LiverDetail detail = SearchLiver(liver);
            if (detail == null)
            {
                await SendError(this, 3, "The specified river cannot be found.");
                return;
            }
            bool rem;
            var ch = new DiscordChannel(Context.Guild.Id, Context.Channel.Id);
            if (services != null && services != "")
            {
                if (!DetectTypes(detail, out var types, services.Split(',')))
                {
                    await SendError(this, 4, "This service is not supported/found.");
                    return;
                }
                foreach (var t in types) ch.RemoveContent(t);
                rem = ch.MsgContentList.Count == 0;
            }
            else rem = true;
            if (!rem && DiscordNotify.UpdateNotifyList(detail, ch)) await ReplyAsync("Success.");
            else if (rem && DiscordNotify.RemoveNotifyList(detail, ch)) await ReplyAsync("Success.");
            else await SendError(this, 2, "This channel is not alrady added.");
        }

        [Command("vinfo")]
        public async Task CommandHandler(params string[] args)
        {
            var list = new List<string>() { "add", "set", "remove" };
            if (list.Contains(args[0]))
            {
                if (args.Length == 3 && args[0] == list[0]) await AddNotify(args[1], args[2]);
                else if (args.Length == 5 && args[0] == list[1] && bool.TryParse(args[3], out var b))
                    await SetContent(args[1], args[2], b, args[4]);
                else if (args.Length == 2 && args[0] == list[2]) await RemoveNotify(args[1]);
                else if (args.Length == 3 && args[0] == list[2]) await RemoveNotify(args[1], args[2]);
                else await SendError(this, -1, "Invalid command argument.");
            }
            else
            {
                await SendError(this, 1, "This command type is not existed.");
            }
        }

        private static LiverDetail SearchLiver(string liver)
        {
            var search = liver.Split('=');
            LiverDetail detail = null;
            if (search.Length > 2) for (int i = 2; i < search.Length; i++) search[1] += '=' + search[i];

            if (search.Length == 1) detail = LiverData.GetLiverFromNameMatch(search[0]);
            else if (search[0] == "name") detail = LiverData.GetLiverFromNameMatch(search[1]);
            else if (search[0] == "youtube") detail = LiverData.GetLiverFromYouTubeId(search[1]);
            else if (search[0] == "twitter") detail = LiverData.GetLiverFromTwitterId(search[1]);
            return detail;
        }
        private static bool DetectTypes(LiverDetail liver, out Type[] types, params string[] servs)
        {
            var list = new List<Type>();
            types = null;
            if (servs.Length == 0) return false;

            foreach(var s in servs)
            {
                if (s == "youtube")
                    list.AddRange(new List<Type>()
                    {
                        typeof(YouTubeNewLiveEvent), typeof(YouTubeChangeInfoEvent),
                        typeof(YouTubeDeleteLiveEvent), typeof(YouTubeStartLiveEvent)
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
        private static bool DetectType(LiverDetail liver, out Type type, string serv)
        {
            type = null;
            if (serv == "youtube_new") type = typeof(YouTubeNewLiveEvent);
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

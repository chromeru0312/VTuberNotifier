using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VTuberNotifier.Liver;
using VTuberNotifier.Watcher;
using VTuberNotifier.Watcher.Feed;
using VTuberNotifier.Watcher.Store;

namespace VTuberNotifier.Discord
{
    public class CmdVInfo : CmdBase
    {
        [Command("vinfo add")]
        public async Task AddNotify(string liver, string services)
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
            var sers = services.Split(',');
            var ch = new DiscordChannel(Context.Guild.Id, Context.Channel.Id);
            foreach (var s in sers)
            {
                Type type = DetectType(detail, s);
                if (type == null)
                {
                    await SendError(this, 4, "This service is not supported/found.");
                    return;
                }
                ch.SetContent(type, null);
            }
            if (DiscordNotify.AddNotifyList(detail, ch)) await ReplyAsync("Success.");
            else await SendError(this, 2, "This channel is alrady added.");
        }

        [Command("vinfo set")]
        public async Task SetContent(string liver, string service, string content)
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
            Type type = DetectType(detail, service);
            if (type == null)
            {
                await SendError(this, 4, "This service is not supported/found.");
                return;
            }
            ch.SetContent(type, content);

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
                var sers = services.Split(',');
                foreach (var s in sers)
                {
                    Type type = DetectType(detail, s);
                    if (type == null)
                    {
                        await SendError(this, 4, "This service is not supported/found.");
                        return;
                    }
                    ch.RemoveContent(type);
                }
                rem = ch.MsgContentList.Count == 0;
            }
            else rem = true;
            if (!rem && DiscordNotify.UpdateNotifyList(detail, ch)) await ReplyAsync("Success.");
            else if (rem && DiscordNotify.RemoveNotifyList(detail, ch)) await ReplyAsync("Success.");
            else await SendError(this, 2, "This channel is not alrady added.");
        }

        [Command("vinfo")]
        public async Task RemoveNotify(params string[] args)
        {
            var list = new List<string>() { "add", "set", "remove" };
            if (list.Contains(args[0]))
            {
                if (args.Length == 3 && args[0] == list[0]) await AddNotify(args[1], args[2]);
                else if (args.Length == 4 && args[0] == list[1]) await SetContent(args[1], args[2], args[3]);
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
        private static Type DetectType(LiverDetail liver, string service)
        {
            if (service == "youtube") return typeof(YouTubeItem);
            else if (service == "twitter") return typeof(Tweet);
            else if (service == "booth") return typeof(BoothProduct);
            else if (service == "store")
            {
                if (liver.Group == LiverGroup.Nijiasnji) return typeof(NijisanjiProduct);
            }
            return null;
        }
    }
}

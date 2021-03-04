using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VTuberNotifier.Liver;
using VTuberNotifier.Watcher.Event;

namespace VTuberNotifier.Notification.Discord
{
    public class CmdVInfo : CmdBase
    {
        [Command("vinfo add")]
        public async Task AddNotify(string liver, string services, bool only = true)
        {
            if (await IsArgumentNullOrEmpty(3, liver) || await IsArgumentNullOrEmpty(4, services))
                return;

            if (!LiverData.DetectLiver(liver, out var detail))
            {
                await SendError(this, 3, "The specified river cannot be found.");
                return;
            }
            var ch = new DiscordChannel(Context.Guild.Id, Context.Channel.Id);
            if (!NotifyEvent.DetectTypes(detail, out var types, services.Split(',')))
            {
                await SendError(this, 4, "This service is not supported/found.");
                return;
            }
            foreach (var t in types) ch.AddContent(t, only);

            if (NotifyEvent.AddDiscordList(detail, ch)) await ReplyAsync("Success.");
            else await SendError(this, 2, "Some errors has occured.");
        }

        [Command("vinfo set")]
        public async Task SetContent(string liver, string service, bool only = true, string content = null)
        {
            if (await IsArgumentNullOrEmpty(3, liver) || await IsArgumentNullOrEmpty(4, service))
                return;
            if (string.IsNullOrEmpty(content) || content == "default")
                content = null;

            if (!LiverData.DetectLiver(liver, out var detail))
            {
                await SendError(this, 3, "The specified river cannot be found.");
                return;
            }
            var ch = new DiscordChannel(Context.Guild.Id, Context.Channel.Id);
            if (!NotifyEvent.DetectType(detail, out var type, service))
            {
                await SendError(this, 4, "This service is not supported/found.");
                return;
            }
            ch.SetContent(type, only, content);

            if (NotifyEvent.UpdateDiscordList(detail, ch)) await ReplyAsync("Success.");
            else await SendError(this, 2, "This channel or service is not alrady added.");
        }

        [Command("vinfo remove")]
        public async Task RemoveNotify(string liver, string services = null)
        {
            if (await IsArgumentNullOrEmpty(3, liver))
                return;

            if (!LiverData.DetectLiver(liver, out var detail))
            {
                await SendError(this, 3, "The specified river cannot be found.");
                return;
            }
            bool rem;
            var ch = new DiscordChannel(Context.Guild.Id, Context.Channel.Id);
            if (!string.IsNullOrEmpty(services))
            {
                if (!NotifyEvent.DetectTypes(detail, out var types, services.Split(',')))
                {
                    await SendError(this, 4, "This service is not supported/found.");
                    return;
                }
                foreach (var t in types) ch.RemoveContent(t);
                rem = ch.MsgContentList.Count == 0;
            }
            else rem = true;
            if (!rem && NotifyEvent.UpdateDiscordList(detail, ch)) await ReplyAsync("Success.");
            else if (rem && NotifyEvent.RemoveDiscordList(detail, ch)) await ReplyAsync("Success.");
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

        private async Task<bool> IsArgumentNullOrEmpty(int no, string arg)
        {
            var b = string.IsNullOrEmpty(arg);
            if (b) await SendError(this, no, "Argument is empty.");
            return b;
        }
    }
}
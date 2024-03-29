﻿using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VTuberNotifier.Liver;

namespace VTuberNotifier.Notification.Discord
{
    public class CmdVInfo : ModuleBase
    {
        [Command("vinfo add")]
        public async Task AddNotify(string liver, string services, bool only = true, bool edit = false)
        {
            if (await IsArgumentNullOrEmpty(3, liver) || await IsArgumentNullOrEmpty(4, services))
                return;

            if (!LiverData.DetectLiver(liver, out var detail))
            {
                await ReplyError(2, "The specified river cannot be found.");
                return;
            }
            var ch = new DiscordChannel(Context.Guild.Id, Context.Channel.Id, edit);
            if (!EventNotifier.Instance.DetectTypes(detail, out var types, services.Split(',')))
            {
                await ReplyError(3, "This service is not supported/found.");
                return;
            }
            foreach (var t in types) ch.AddContent(t, only);

            if (EventNotifier.Instance.AddDiscordList(detail, ch))
            {
                await ReplySuccess("Successfully added.");
                LocalConsole.Log("DiscordCmd", new (LogSeverity.Debug, "Add", "Add new service."));
                DiscordBot.Instance.AddChannel(Context.Guild.Id, Context.Channel.Id);
            }
            else
            {
                await ReplyError(2, "Some errors has occured.");
                LocalConsole.Log("DiscordCmd", new (LogSeverity.Warning, "Add", "Failed to add service."));
            }

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
                await ReplyError(2, "The specified river cannot be found.");
                return;
            }
            var ch = new DiscordChannel(Context.Guild.Id, Context.Channel.Id);
            if (!EventNotifier.Instance.DetectType(detail, out var type, service))
            {
                await ReplyError(3, "This service is not supported/found.");
                return;
            }
            ch.SetContent(type, only, content);

            if (EventNotifier.Instance.UpdateDiscordList(detail, ch))
            {
                await ReplySuccess("Successfully updated.");
                LocalConsole.Log("DiscordCmd", new (LogSeverity.Debug, "Update", "Update service."));
            }
            else
            {
                await ReplyError(2, "This channel or service is not alrady added.");
                LocalConsole.Log("DiscordCmd", new (LogSeverity.Warning, "Update", "Failed to update service."));
            }
        }

        [Command("vinfo remove")]
        public async Task RemoveNotify(string liver, string services = null)
        {
            if (await IsArgumentNullOrEmpty(3, liver))
                return;

            if (!LiverData.DetectLiver(liver, out var detail))
            {
                await ReplyError(2, "The specified river cannot be found.");
                return;
            }
            bool rem;
            var ch = new DiscordChannel(Context.Guild.Id, Context.Channel.Id);
            if (!string.IsNullOrEmpty(services))
            {
                if (!EventNotifier.Instance.DetectTypes(detail, out var types, services.Split(',')))
                {
                    await ReplyError(3, "This service is not supported/found.");
                    return;
                }
                foreach (var t in types) ch.RemoveContent(t);
                rem = ch.MsgContentList.Count == 0;
            }
            else rem = true;
            if (!rem && EventNotifier.Instance.UpdateDiscordList(detail, ch))
            {
                await ReplySuccess("Successfully updated.");
                LocalConsole.Log("DiscordCmd", new (LogSeverity.Debug, "Remove", "Update service."));
            }
            else if (rem && EventNotifier.Instance.RemoveDiscordList(detail, ch))
            {
                await ReplySuccess("Successfully removed.");
                LocalConsole.Log("DiscordCmd", new (LogSeverity.Debug, "Remove", "Remove service."));
                DiscordBot.Instance.RemoveChannel(Context.Guild.Id, Context.Channel.Id);
            }
            else
            {
                await ReplyError(2, "This channel is not alrady added.");
                LocalConsole.Log("DiscordCmd", new (LogSeverity.Warning, "Remove", "Failed to remove service."));
            }
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
                else await ReplyError(0, "Invalid command argument.");
            }
            else
            {
                await ReplyError(1, "This command type is not existed.");
            }
        }

        private async Task<bool> IsArgumentNullOrEmpty(byte no, string arg)
        {
            var b = string.IsNullOrEmpty(arg);
            if (b) await ReplyError(no, "Argument is empty.");
            return b;
        }

        private async Task<IUserMessage> ReplySuccess(string msg)
        {
            var embed = new EmbedBuilder()
            {
                Title = "Success",
                Description = msg,
                Color = Color.Blue
            };
            return await ReplyAsync(embed: embed.Build());
        }
        private async Task<IUserMessage> ReplyError(byte argpos, string msg)
        {
            var embed = new EmbedBuilder()
            {
                Title = "Error",
                Description = msg + (argpos != 0 ? $" : {Context.Message.Content.Split(' ')[argpos]}" : ""),
                Color = Color.DarkRed
            };
            return await ReplyAsync(embed: embed.Build());
            throw new ArgumentException(msg);
        }
    }
}
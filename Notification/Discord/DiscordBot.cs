using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace VTuberNotifier.Notification.Discord
{
    public class DiscordBot
    {
        public static DiscordBot Instance { get; private set; }
        internal IReadOnlyList<(ulong, ulong)> AllChannels { get; private set; }

        private DiscordBot()
        {
            Settings.Data.DiscordClient.Log += Log;
            Settings.Data.DiscordClient.MessageReceived += CommandRecieved;
            DataManager.CreateInstance();

            if (DataManager.Instance.TryDataLoad("AllDiscordList", out IEnumerable<string> list))
                AllChannels = new List<(ulong, ulong)>(list.Select(s => GetTuple(s)));
            else AllChannels = new List<(ulong, ulong)>();

            static (ulong, ulong) GetTuple(string s)
            {
                var ss = s.Split('/');
                return (ulong.Parse(ss[0].Trim()), ulong.Parse(ss[1].Trim()));
            }
        }

        public static void CreateInstance()
        {
            if (Instance != null) return;
            Instance = new DiscordBot();
        }

        public async Task BotStart()
        {
            try
            {
                await Settings.Data.DiscordClient.LoginAsync(TokenType.Bot, Settings.Data.DiscordToken);
                await Settings.Data.DiscordClient.StartAsync();

                await Task.Delay(-1);
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private async Task CommandRecieved(SocketMessage sm)
        {
            int arg = 0;
            if (sm is not SocketUserMessage msg || msg.Author.IsBot) return;

            if (msg.HasCharPrefix('>', ref arg))
            {
                var context = new CommandContext(Settings.Data.DiscordClient, msg);
                var result = await Settings.Data.DiscordCmdService.ExecuteAsync(context, arg, Settings.Data.ServicePrivider);
                var log = result.IsSuccess ? $"Command({msg}) is Success."
                    : $"Command({msg}) is Error.\n{result.Error.Value}: {result.ErrorReason}";
                await Log(new(LogSeverity.Info, "Command", log));
            }
        }

        public void AddChannel(ulong guild, ulong channel)
        {
            var taple = (guild, channel);
            var list = new List<(ulong, ulong)>(AllChannels);
            if (!list.Contains(taple)) list.Add(taple);
            AllChannels = list;
            SaveList();
        }
        public void RemoveChannel(ulong guild, ulong channel)
        {
            var taple = (guild, channel);
            var list = new List<(ulong, ulong)>(AllChannels);
            if (list.Contains(taple)) list.Remove(taple);
            AllChannels = list;
            SaveList();
        }
        private void SaveList()
        {
            var data = AllChannels.Select(g => $"{g.Item1} / {g.Item2}");
            DataManager.Instance.DataSave("AllDiscordList", data, true);
        }

        private Task Log(LogMessage msg)
        {
            LocalConsole.Log("Discord", msg);
            return Task.CompletedTask;
        }
    }
    public class CmdBase : ModuleBase
    {
        protected async Task<IUserMessage> SendError<T>(T _, int argpos, string msg) where T : CmdBase
        {
            var name = typeof(T).Name[3..].ToLower();
            var s = $"{Context.User.Mention}\nIllegal Command Error: {name}\n" +
                $"Argument No{argpos} -- {msg}";
            await ReplyAsync(s);
            throw new ArgumentException(msg);
        }

        protected async override Task<IUserMessage> ReplyAsync(string message = null, bool isTTS = false,
            Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null)
        {
            var msg = await base.ReplyAsync(message, isTTS, embed, options);
            return msg;
        }
    }
}

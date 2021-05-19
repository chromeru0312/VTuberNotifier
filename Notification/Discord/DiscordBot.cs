using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using static VTuberNotifier.SettingData;

namespace VTuberNotifier.Notification.Discord
{
    public class DiscordBot
    {
        public static DiscordBot Instance { get; private set; }
        internal IReadOnlyList<(ulong, ulong)> AllChannels { get; private set; }

        private DiscordBot()
        {
            DiscordClient.Log += Log;
            DiscordClient.MessageReceived += CommandRecieved;
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
                await DiscordCmdService.AddModulesAsync(Assembly.GetEntryAssembly(), ServicePrivider);
                await DiscordClient.LoginAsync(TokenType.Bot, DiscordToken);
                await DiscordClient.StartAsync();

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
                var context = new CommandContext(DiscordClient, msg);
                var result = await DiscordCmdService.ExecuteAsync(context, arg, ServicePrivider);
                if (result.IsSuccess)
                {
                    await Log(new LogMessage(LogSeverity.Info, "Command", $"Command({msg}) is Success."));
                }
                else
                {
                    await Log(new LogMessage(LogSeverity.Warning, "Command",
                        $"Command({msg}) is Error.\n{result.Error.Value}: {result.ErrorReason}"));
                }
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

        private Task Log(LogMessage msg) => LocalConsole.Log("Discord", msg);
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

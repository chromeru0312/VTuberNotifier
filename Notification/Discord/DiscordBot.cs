using System;
using System.Collections.Generic;
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
        internal IReadOnlyList<ulong> AdministratorID { get; private set; }

        private DiscordBot()
        {
            DiscordClient.Log += Log;
            DiscordClient.MessageReceived += CommandRecieved;
            DataManager.CreateInstance();
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
                await Log(new LogMessage(LogSeverity.Info, "Command", $"Command[{msg}] process is started."));
                var context = new CommandContext(DiscordClient, msg);
                var result = await DiscordCmdService.ExecuteAsync(context, arg, ServicePrivider);
                if (result.IsSuccess)
                {
                    await Log(new LogMessage(LogSeverity.Info, "Command", $"Command[{msg}] is Success."));
                }
                else
                {
                    await Log(new LogMessage(LogSeverity.Info, "Command",
                        $"Command[{msg}] is Error.\n{result.Error.Value}: {result.ErrorReason}"));
                }
            }
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

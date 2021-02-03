using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using static VTuberNotifier.SettingData;

namespace VTuberNotifier.Discord
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
                //var dc = DiscordCommand.Instance;
                //dc.StartCommandProcessing(context);
                var result = await DiscordCmdService.ExecuteAsync(context, arg, ServicePrivider);
                if (result.IsSuccess)
                {
                    await Log(new LogMessage(LogSeverity.Info, "Command", $"Command[{msg}] is Success."));
                    //DiscordCommand.Instance.CommandHistory[dc.ProcessingCommand[context]].SetStatus(CommandDetail.CommandStatus.Success);
                }
                else
                {
                    await Log(new LogMessage(LogSeverity.Info, "Command",
                        $"Command[{msg}] is Error.\n{result.Error.Value}: {result.ErrorReason}"));
                    //DiscordCommand.Instance.CommandHistory[dc.ProcessingCommand[context]].SetStatus(CommandDetail.CommandStatus.Error, result.ErrorReason);
                }
            }
        }

        private Task Log(LogMessage msg) => LocalConsole.Log("Discord", msg);
    }
}

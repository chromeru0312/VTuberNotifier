using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VTuberNotifier.Discord
{
    public class DiscordCommand
    {
        public static DiscordCommand Instance { get; private set; }
        internal IReadOnlyDictionary<ICommandContext, uint> ProcessingCommand { get; private set; }
        public IReadOnlyDictionary<uint, CommandDetail> CommandHistory { get; private set; }

        private DiscordCommand()
        {
            ProcessingCommand = new Dictionary<ICommandContext, uint>();
            CommandHistory =
                new Dictionary<uint, CommandDetail>(
                    DataManager.Instance.InitDataLoad<Dictionary<uint, CommandDetailJson>>("CommandHistory")
                    .Select(p => new KeyValuePair<uint, CommandDetail>(p.Key, new CommandDetail(p.Value))));
        }
        public static void CreateInstance()
        {
            if (Instance != null) return;
            Instance = new DiscordCommand();
        }
        public void AddHistory(uint id, CommandDetail cd)
        {
            CommandHistory = new Dictionary<uint, CommandDetail>(CommandHistory) { { id, cd } };
            DataManager.Instance.DataSave("CommandHistory", CommandHistory);
        }
        public void SetHistoryResult(ICommandContext context, CommandDetail.CommandStatus status, string comment = null)
        {
            if (!ProcessingCommand.ContainsKey(context)) return;
            var p = new Dictionary<ICommandContext, uint>(ProcessingCommand);
            p.Remove(context, out var id);
            ProcessingCommand = p;
            CommandHistory[id].SetStatus(status, comment);
            DataManager.Instance.DataSave("CommandHistory", CommandHistory);
        }
        public void StartCommandProcessing(CommandContext context)
        {
            uint id = (uint)((context, DateTime.Now).GetHashCode() - int.MinValue);
            ProcessingCommand = new Dictionary<ICommandContext, uint>(ProcessingCommand) { { context, id } };
        }
    }

    [Serializable]
    public class CommandDetail
    {
        public enum CommandStatus
        {
            Processing, Success, Error
        }

        public uint CommandId { get; }
        public ulong InputId { get; }
        public string InputCommand { get; }
        private IMessage InputMessage
        {
            get
            {
                if (input == null)
                    input = Channel.GetMessageAsync(InputId).Result;
                return input;
            }
        }
        [NonSerialized]
        private IMessage input;
        public ulong GuildId { get; private set; }
        private SocketGuild Guild
        {
            get
            {
                if (guild == null)
                    guild = SettingData.DiscordClient.GetGuild(GuildId);
                return guild;
            }
        }
        [NonSerialized]
        private SocketGuild guild;
        public ulong ChannelId { get; private set; }
        public SocketTextChannel Channel
        {
            get
            {
                if (channel == null)
                    channel = Guild.GetTextChannel(ChannelId);
                return channel;
            }
        }
        [NonSerialized]
        private SocketTextChannel channel;
        public ulong UserId { get; private set; }
        public SocketUser User
        {
            get
            {
                if (user == null)
                    user = SettingData.DiscordClient.GetUser(UserId);
                return user;
            }
        }
        [NonSerialized]
        private SocketUser user;
        public ulong ResultId { get; }
        public IMessage ResultMessage
        {
            get
            {
                if (result == null)
                    result = Channel.GetMessageAsync(ResultId).Result;
                return result;
            }
        }
        [NonSerialized]
        private IMessage result;
        public string ResultComment { get; private set; }
        public DateTime CompleteDate { get; private set; }
        public CommandStatus Status { get; private set; }
        public bool IsDeleted { get; private set; }

        internal CommandDetail(uint id, ICommandContext context, IUserMessage result)
        {
            CommandId = id;
            InputId = context.Message.Id;
            InputCommand = context.Message.Content;
            GuildId = context.Guild.Id;
            ChannelId = context.Channel.Id;
            UserId = context.User.Id;
            Status = CommandStatus.Processing;
            ResultId = result.Id;
            IsDeleted = false;
        }
        internal CommandDetail(CommandDetailJson json)
        {
            CommandId = json.CommandId;
            InputId = json.InputId;
            InputCommand = json.InputCommand;
            GuildId = json.GuildId;
            ChannelId = json.ChannelId;
            UserId = json.UserId;
            Status = json.Status;
            ResultId = json.ResultId;
            IsDeleted = json.IsDeleted;
        }

        internal void SetStatus(CommandStatus status, string comment = null)
        {
            if (Status == CommandStatus.Processing && status != CommandStatus.Processing)
            {
                Status = status;
                CompleteDate = DateTime.Now;
                ResultComment = comment;
            }
        }
        public async void DeleteMessage()
        {
            if (IsDeleted) return;
            await InputMessage.DeleteAsync();
            await ResultMessage.DeleteAsync();
            IsDeleted = true;
        }
    }

    public class CommandDetailJson
    {
        public uint CommandId { get; }
        public ulong InputId { get; }
        public string InputCommand { get; }
        public ulong GuildId { get; private set; }
        public ulong ChannelId { get; private set; }
        public ulong UserId { get; private set; }
        public ulong ResultId { get; }
        public string ResultComment { get; set; }
        public DateTime CompleteDate { get; set; }
        public CommandDetail.CommandStatus Status { get; set; }
        public bool IsDeleted { get; set; }
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
            //var id = DiscordCommand.Instance.ProcessingCommand[Context];
            //var text = $"CommandID:{id}";
            //if (message == null) message = text;
            //else message += "\n" + text;
            var msg = await base.ReplyAsync(message, isTTS, embed, options);
            //DiscordCommand.Instance.AddHistory(id, new CommandDetail(id, Context, msg));
            return msg;
        }
    }
}

using CoreTweet;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;

namespace VTuberNotifier
{
    public static class SettingData
    {
        public static YouTubeService YouTubeService { get { if (_YouTubeService == null) LoadSettingData(); return _YouTubeService; } }
        private static YouTubeService _YouTubeService = null;
        public static string NotificationCallback { get { if (_NotificationCallback == null) LoadSettingData(); return _NotificationCallback; } }
        private static string _NotificationCallback = null;

        public static Tokens TwitterToken { get { if (_TwitterToken == null) LoadSettingData(); return _TwitterToken; } }
        private static Tokens _TwitterToken = null;

        public static string DiscordToken { get { if (_DiscordToken == null) LoadSettingData(); return _DiscordToken; } }
        private static string _DiscordToken;
        public static DiscordSocketClient DiscordClient { get { if (_DiscordClient == null) LoadSettingData(); return _DiscordClient; } }
        private static DiscordSocketClient _DiscordClient = null;
        public static CommandService DiscordCmdService { get { if (_DiscordCmdService == null) LoadSettingData(); return _DiscordCmdService; } }
        private static CommandService _DiscordCmdService = null;
        public static IServiceProvider ServicePrivider { get { if (_ServicePrivider == null) LoadSettingData(); return _ServicePrivider; } }
        private static IServiceProvider _ServicePrivider = null;

        public static CultureInfo Culture { get; } = new ("ja-JP");

        private static void LoadSettingData()
        {
            var path = Path.Combine(Path.GetFullPath(@"./"), "Authentication.json");
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var reader = new StreamReader(stream);
            var json = JObject.Parse(reader.ReadToEnd());

            _YouTubeService = new (new() { ApiKey = json["youtube_apiKey"].Value<string>() });
            _NotificationCallback = json["youtube_callback_url"].Value<string>();
            _TwitterToken = Tokens.Create(json["twitter_apiKey"].Value<string>(), json["twitter_apiSecret"].Value<string>(),
                json["twitter_accessKey"].Value<string>(), json["twitter_accessSecret"].Value<string>());
            _DiscordToken = json["discord_token"].Value<string>();
            _DiscordClient = new (new () { LogLevel = LogSeverity.Debug });
            _DiscordCmdService = new ();
            _ServicePrivider = new ServiceCollection().BuildServiceProvider();
        }

        public static WebClient GetWebClient()
        {
            var wc = new WebClient() { Encoding = Encoding.UTF8 };
            wc.Headers[HttpRequestHeader.UserAgent] = "VInfoNotifier (ASP.NET 5.0 / Ubuntu 20.04) [@chromeru0312]";
            return wc;
        }
        internal static void Dispose()
        {
            YouTubeService.Dispose();
            DiscordClient.Dispose();
        }
    }
}

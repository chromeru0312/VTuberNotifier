using CoreTweet;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using static System.Net.SecurityProtocolType;

namespace VTuberNotifier
{
    public class Settings : IDisposable
    {
        public static Settings Data { get; private set; }

        public string ExecutingPath { get; }
        public int WebPort { get; }
        public YouTubeService YouTubeService { get; }
        public string NotificationCallback { get; }
        public Tokens TwitterToken { get; }
        public string DiscordToken { get; }
        public DiscordSocketClient DiscordClient { get; }
        public CommandService DiscordCmdService { get; }
        public IServiceProvider ServicePrivider { get; }
        public HttpClient HttpClient { get; }
        public CultureInfo Culture { get; }

        private Settings()
        {
            ExecutingPath = Path.GetDirectoryName(AppContext.BaseDirectory);
            var path = Path.Combine(ExecutingPath, "Authentication.json");
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var reader = new StreamReader(stream);
            var json = JObject.Parse(reader.ReadToEnd());

            WebPort = json["web_port"].Value<int>();
            YouTubeService = new(new() { ApiKey = json["youtube_apiKey"].Value<string>() });
            NotificationCallback = json["youtube_callback_url"].Value<string>();
            TwitterToken = Tokens.Create(json["twitter_apiKey"].Value<string>(), json["twitter_apiSecret"].Value<string>(),
                json["twitter_accessKey"].Value<string>(), json["twitter_accessSecret"].Value<string>());
            DiscordToken = json["discord_token"].Value<string>();
            DiscordClient = new(new() { LogLevel = LogSeverity.Debug });
            DiscordCmdService = new();
            DiscordCmdService.AddModulesAsync(Assembly.GetEntryAssembly(), ServicePrivider).Wait();
            ServicePrivider = new ServiceCollection().BuildServiceProvider();

            HttpClient = new(new HttpClientHandler { AllowAutoRedirect = true });
            HttpClient.DefaultRequestHeaders.UserAgent.Add(new("VInfoNotifier", "1.0"));
            ServicePointManager.SecurityProtocol = Tls | Tls12 | Tls11 | Tls13;
            Culture = new("ja-JP");
        }
        public static void LoadSettingData()
        {
            if (Data == null) Data = new();
        }

        public HttpRequestMessage CreateRequest(HttpMethod method, string url, params (string, string)[] headers)
        {
            var req = new HttpRequestMessage(method, url);
            req.Headers.Add("UserAgent", "VInfoNotifier (ASP.NET 5.0 / Ubuntu 20.04) [@chromeru0312]");
            foreach (var (name, value) in headers)
                req.Headers.Add(name, value);
            return req;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            YouTubeService.Dispose();
            DiscordClient.Dispose();
            HttpClient.Dispose();
            Data = null;
        }
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
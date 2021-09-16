using Discord;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using VTuberNotifier.Notification;
using VTuberNotifier.Notification.Discord;
using VTuberNotifier.Watcher;
using VTuberNotifier.Watcher.Store;

namespace VTuberNotifier
{
    public class ServerStart
    {
        public static void Main(string[] args)
        {
            Settings.LoadSettingData();
            LocalConsole.CreateNewLogFile();
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>().UseUrls($"http://localhost:{Settings.Data.WebPort}");
                });
    }

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddSwaggerGen();

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.KnownProxies.Add(IPAddress.Parse("10.0.0.100"));
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime lifetime, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "VTuberNotifier v1"));
            }

            loggerFactory.AddProvider(new ConsoleLoggerProvider());

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            app.UseAuthentication();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            lifetime.ApplicationStarted.Register(OnStarted);
            lifetime.ApplicationStopped.Register(OnStopped);
        }
        private void OnStarted()
        {
            DataManager.CreateInstance();
            TimerManager.CreateInstance();

            NijisanjiWatcher.CreateInstance();
            DotliveWatcher.CreateInstance();
            NijisanjiWatcher.LoadList();
            DotliveWatcher.LoadList();
            TimerManager.Instance.AddAction(20 * 60, WatcherTask.NijisanjiStoreTask);
            TimerManager.Instance.AddAction(20 * 60, WatcherTask.DotliveStoreTask);

            BoothWatcher.CreateInstance();
            TimerManager.Instance.AddAction(20 * 60, WatcherTask.BoothTask);

            PRTimesFeed.CreateInstance();
            TimerManager.Instance.AddAction(20 * 60, WatcherTask.PRTimesTask, 300);

            YouTubeWatcher.CreateInstance();
            TimerManager.Instance.AddAction(30, WatcherTask.YouTubeChangeTask);

            //NicoLiveWatcher.CreateInstance();
            //TimerManager.Instance.AddAction(6 * 3600, WatcherTask.NicoLiveTask, 3600);

            //TwitterWatcher.CreateInstance();
            //TimerManager.Instance.AddAction(60, WatcherTask.TwitterTask);

            EventNotifier.CreateInstance();
            //Task.Run(WatcherTask.YouTubeNotificationTask);
            DiscordBot.CreateInstance();
            Task.Run(DiscordBot.Instance.BotStart);
        }
        private void OnStopped()
        {
            try
            {
                Settings.Data.HttpClient.PostAsJsonAsync("http://localhost:55555/app",
                    new ErrorRequest { AppName = "VInfoNotifier", ErrorLog = "Application stopped.", IsExit = true }).Wait();
            }
            catch { }
            Settings.Data.Dispose();
            TimerManager.Instance.Dispose();
        }
    }

    public class ConsoleLogger : ILogger
    {
        private readonly string _name;
        public ConsoleLogger(string name)
        {
            _name = name.Split('.')[^1];
        }
        public IDisposable BeginScope<T>(T state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<T>(LogLevel level, EventId id, T state, Exception ex, Func<T, Exception, string> formatter)
        {
            LogSeverity severity = LogSeverity.Debug;
            if (level == LogLevel.Information) severity = LogSeverity.Info;
            else if (level == LogLevel.Warning) severity = LogSeverity.Warning;
            else if (level == LogLevel.Error) severity = LogSeverity.Error;
            else if (level == LogLevel.Critical) severity = LogSeverity.Critical;
            LocalConsole.Log("ASP.NET", new(severity, _name, formatter(state, ex), ex));
        }
    }

    public class ConsoleLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentDictionary<string, ConsoleLogger> _loggers;

        public ConsoleLoggerProvider()
        {
            _loggers = new();
        }

        public ILogger CreateLogger(string category)
        {
            return _loggers.GetOrAdd(category, name => new ConsoleLogger(name));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing) _loggers.Clear();
        }
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public struct NormalRequest
    {
        public string AppName { get; set; }
    }

    public struct ErrorRequest
    {
        public string AppName { get; set; }
        public string ErrorLog { get; set; }
        public bool IsExit { get; set; }
    }
}
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
using System.Text.Json;
using VTuberNotifier.Watcher;

namespace VTuberNotifier
{
    public class ServerStart
    {
        public static void Main(string[] args)
        {
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
                    webBuilder.UseStartup<Startup>().UseUrls("http://localhost:61104");
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

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.KnownProxies.Add(IPAddress.Parse("10.0.0.100"));
            });
        }

        public void Configure(IApplicationBuilder app, IHostApplicationLifetime lifetime, ILoggerFactory loggerFactory)
        {
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
            WatcherTask.CreateInstance();
        }
        private void OnStopped()
        {
            try
            {
                using var wc = SettingData.GetWebClient();
                wc.Headers.Add("Content-Type", "application/json");
                var error = new ErrorRequest { AppName = "VInfoNotifier", ErrorLog = "Application stopped", IsExit = true };
                wc.UploadString("http://localhost:55555/app", JsonSerializer.Serialize(error));
            }
            catch (Exception) { }
            SettingData.Dispose();
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
        public IDisposable BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel level, EventId id, TState state, Exception ex, Func<TState, Exception, string> formatter)
        {
            LogSeverity severity = LogSeverity.Debug;
            if (level == LogLevel.Information) severity = LogSeverity.Info;
            else if (level == LogLevel.Warning) severity = LogSeverity.Warning;
            else if (level == LogLevel.Error) severity = LogSeverity.Error;
            else if (level == LogLevel.Critical) severity = LogSeverity.Critical;
            LocalConsole.Log("ASP.NET", new(severity, _name, formatter(state, ex), ex)).Wait();
        }
    }

    public class ConsoleLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentDictionary<string, ConsoleLogger> _loggers;

        public ConsoleLoggerProvider()
        {
            _loggers = new();
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new ConsoleLogger(name));
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
        ~ConsoleLoggerProvider()
        {
            Dispose(disposing: false);
        }
    }

    public struct ErrorRequest
    {
        public string AppName { get; set; }
        public string ErrorLog { get; set; }
        public bool IsExit { get; set; }
    }
}
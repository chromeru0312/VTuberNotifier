using Discord;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace VTuberNotifier
{
    public static class LocalConsole
    {
        public static string LogPath { get; set; } = null;
        public static bool IsDebug { get; set; }
#if DEBUG
        = true;
#else
        = false;
#endif
        private static ConsoleStream ConsoleWriter { get; set; }
        private static ConsoleColor DefaultColor { get; set; }
        private static IReadOnlyDictionary<LogSeverity, ConsoleColor> SeverityColor { get; }
            = new Dictionary<LogSeverity, ConsoleColor>()
            {
                { LogSeverity.Critical, ConsoleColor.Red },
                { LogSeverity.Error, ConsoleColor.Red },
                { LogSeverity.Warning, ConsoleColor.Yellow },
                { LogSeverity.Info, ConsoleColor.DarkGreen },
                { LogSeverity.Verbose, ConsoleColor.DarkCyan },
                { LogSeverity.Debug, ConsoleColor.DarkGray },
            };

        private static Queue<(string, LogMessage)> LogQueue { get; } = new();
        private static Task ConsoleTask { get; set; } = Task.CompletedTask;

        internal static void CreateNewLogFile()
        {
            if (LogPath == null)
            {
                LogPath = Path.Combine(Settings.Data.ExecutingPath, "logs");
                if (!Directory.Exists(LogPath)) Directory.CreateDirectory(LogPath);
                DefaultColor = IsDebug ? ConsoleColor.White : Console.ForegroundColor;
            }
            var path = Path.Combine(LogPath, $"log-{DateTime.Now:yyyyMMddHHmm}.log");
            ConsoleWriter = new ConsoleStream(new(path, true, Encoding.UTF8)) { AutoFlush = true };
            Console.SetOut(ConsoleWriter);
        }

        public static void Log<T>(T _, LogMessage msg) => Log(typeof(T).Name, msg);
        public static void Log(string place, LogMessage msg)
        {
            LogQueue.Enqueue((place, msg));
            if (ConsoleTask.IsCompleted)
            {
                ConsoleTask = new Task(LogInner);
                ConsoleTask.Start();
            }
        }
        private static void LogInner()
        {
            while (LogQueue.Count > 0)
            {
                var (place, msg) = LogQueue.Dequeue();
                if (((string.IsNullOrWhiteSpace(place) && string.IsNullOrWhiteSpace(msg.Source)) ||
                    string.IsNullOrWhiteSpace(msg.Message)) && msg.Exception == null) return;

                var text = $"{DateTime.Now:HH:mm:ss.fff} {place}";
                if (!string.IsNullOrEmpty(msg.Source) && place != msg.Source)
                    text += $"({msg.Source})";
                var e = msg.Exception;
                var message = DefaultColor;

                if (msg.Severity == LogSeverity.Critical)
                    message = ConsoleColor.Red;
                else if (msg.Severity == LogSeverity.Debug)
                {
                    if (IsDebug)
                    {
                        Console.ForegroundColor = message = ConsoleColor.DarkGray;
                    }
                    else
                    {
                        ConsoleWriter.FileStream.WriteLine($"{text}\n             [debug] {msg.Message}");
                        continue;
                    }
                }

                Console.WriteLine(text);
                Console.ForegroundColor = SeverityColor[msg.Severity];
                Console.Write($"             [{msg.Severity.ToString().ToLower()}] ");
                Console.ForegroundColor = message;
                Console.WriteLine(msg.Message);
                if (e != null && SeverityColor[msg.Severity] == ConsoleColor.Red)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"{e.GetType()} - {e.Message}");
                    if (e.StackTrace != null)
                        ConsoleWriter.FileStream.WriteLine(e.StackTrace);
                    var ex = e;
                    var count = 5;
                    while (ex.InnerException != null)
                    {
                        ex = ex.InnerException;
                        ConsoleWriter.FileStream.WriteLine($"--> {ex.GetType()} - {ex.Message}" +
                            (ex.StackTrace == null ? "" : $"\n    {ex.StackTrace.Replace("\n", "\n    ")}"));
                        count--;
                        if (count == 0)
                        {
                            ConsoleWriter.FileStream.WriteLine("--> [And more inner exceptions...]");
                            break;
                        }
                    }
                }
                Console.ForegroundColor = DefaultColor;
            }
        }

        internal class ConsoleStream : StreamWriter
        {
            public override bool AutoFlush
            {
                get => base.AutoFlush;
                set
                {
                    base.AutoFlush = value;
                    FileStream.AutoFlush = value;
                }
            }
            internal StreamWriter FileStream { get; }

            public ConsoleStream(StreamWriter file) : base(Console.OpenStandardOutput())
            {
                FileStream = file;
                Console.OutputEncoding = file.Encoding;
            }

            public override void Write(char value)
            {
                base.Write(value);
                FileStream.Write(value);
            }
            public override void Write(char[] value)
            {
                base.Write(value);
                FileStream.Write(value);
            }
            public override void Write(string value)
            {
                if (value != null) Write(value.ToCharArray());
            }
            public override void WriteLine(string value)
            {
                base.WriteLine(value);
                FileStream.WriteLine(value);
            }

            public override void Flush()
            {
                base.Flush();
                FileStream.Flush();
            }
            public override void Close()
            {
                base.Close();
                FileStream.Close();
            }
            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                if (disposing) FileStream.Dispose();
            }
        }
    }
}
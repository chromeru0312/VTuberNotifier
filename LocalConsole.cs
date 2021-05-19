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
        public static bool IsDebug { get; set; } = false;
        private static ConsoleStream ConsoleWriter { get; set; }
        private static ConsoleColor? DefaultColor { get; set; }
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
        //private static Task WriteingTask { get; set; }
        //private static Queue<Task> WriteingTasks { get; } = new();

        internal static void CreateNewLogFile()
        {
            var s = "./logs";
            if (!Directory.Exists(s)) Directory.CreateDirectory(s);
            var path = Path.Combine(Path.GetFullPath(s), $"log-{DateTime.Now:yyyyMMddHHmm}.log");
            ConsoleWriter = new ConsoleStream(Console.OpenStandardOutput(), new StreamWriter(path, true, Encoding.UTF8)) { AutoFlush = true };
            Console.SetOut(ConsoleWriter);
        }
        public static Task Log<T>(T _, LogMessage msg) => Log(typeof(T).Name, msg);

        public static async Task Log(string place, LogMessage msg)
        {
            await LogInner(place, msg);
        }
        private static Task LogInner(string place, LogMessage msg)
        {
            if (DefaultColor == null) DefaultColor = Console.ForegroundColor;
            var text = $"{DateTime.Now:HH:mm:ss.fff} {place}";
            if (!string.IsNullOrEmpty(msg.Source) || place != msg.Source)
                text += $"({msg.Source})";
            var e = msg.Exception;
            var normal = (ConsoleColor)DefaultColor;
            var message = normal;

            if (msg.Severity == LogSeverity.Critical)
                message = ConsoleColor.Red;
            else if (msg.Severity == LogSeverity.Debug)
            {
                if (IsDebug)
                    message = ConsoleColor.DarkGray;
                else
                {
                    ConsoleWriter.FileStream.WriteLine($"{text}\n             [debug] {msg.Message}");
                    return Task.CompletedTask;
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
                ConsoleWriter.FileStream.WriteLine(e.StackTrace);
            }

            Console.ForegroundColor = normal;
            return Task.CompletedTask;
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

            public ConsoleStream(Stream console, StreamWriter file) : base(console)
            {
                FileStream = file;
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

using Discord;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace VTuberNotifier
{
    public static class LocalConsole
    {
        public static bool IsDebug { get; set; } = false;
        private static ConsoleStream ConsoleWriter { get; set; }

        internal static void CreateNewLogFile()
        {
            var s = "./logs";
            if (!Directory.Exists(s)) Directory.CreateDirectory(s);
            var path = Path.Combine(Path.GetFullPath(s), $"log-{DateTime.Now:yyyyMMddHHmm}.log");
            ConsoleWriter = new ConsoleStream(Console.OpenStandardOutput(), new StreamWriter(path, true, Encoding.UTF8)) { AutoFlush = true };
            Console.SetOut(ConsoleWriter);
        }
        public static Task Log<T>(T _, LogMessage msg) => Log(typeof(T).Name, msg);

        public static Task Log(string place, LogMessage msg)
        {
            string text = $"{DateTime.Now:HH:mm:ss.fff} [{place}({msg.Source})] ";
            text = text.Replace($"{place}({place})", place);
            var output = true;
            switch (msg.Severity)
            {
                case LogSeverity.Critical or LogSeverity.Error:
                    text += $"{msg.Severity}: {msg.Message}";
                    var e = msg.Exception;
                    if (e != null)
                    {
                        text += $"{e.GetType()} {e.Message}";
                        ConsoleWriter.FileStream.WriteLine(e.StackTrace);
                    }
                    break;
                case LogSeverity.Warning:
                    text += $"{msg.Severity}: {msg.Message}";
                    break;
                case LogSeverity.Debug:
                    if (!IsDebug) output = false;
                    goto default;
                default:
                    text += msg.Message;
                    break;
            }
            if (output) Console.WriteLine(text);
            else ConsoleWriter.FileStream.WriteLine(text);
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
            public override void Write(string value)
            {
                if (value != null)
                {
                    Write(value.ToCharArray());
                }
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

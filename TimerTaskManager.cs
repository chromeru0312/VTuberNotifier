using Discord;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using VTuberNotifier.Watcher;

namespace VTuberNotifier
{
    public class TimerTaskManager : IDisposable
    {
        public static TimerTaskManager Instance { get; private set; } = null;
        public int TimerCount { get; private set; }
        private Dictionary<int, HashSet<Func<Task>>> ActionList { get; set; }

        public const int Interval = 20;
        private readonly Timer Timer;
        private DateTime TimerReset;
        private bool disposed;

        private TimerTaskManager()
        {
            Timer = new Timer(Interval * 1000);
            Timer.Elapsed += TimerTask;
            var now = DateTime.Now;
            var sec = now.Second % Interval * 1000;
            Task.Delay(Interval * 1000 - sec - now.Millisecond).Wait();
            Timer.Start();
            TimerReset = DateTime.Today.AddDays(1);
            ActionList = new();
            LocalConsole.Log(this, new LogMessage(LogSeverity.Debug, "Timer", "Timer Start!"));
        }
        public static void CreateInstance()
        {
            if (Instance == null) Instance = new TimerTaskManager();
        }

        public void AddAction(int second, Func<Task> action)
        {
            second = (int)Math.Round(second / (double)Interval);
            if (!ActionList.ContainsKey(second)) ActionList.Add(second, new());
            ActionList[second].Add(action);
        }
        public void RemoveAction(int second, Func<Task> action)
        {
            second = (int)Math.Round(second / (double)Interval);
            ActionList[second].Remove(action);
        }
        public void Stop()
        {
            Timer.Stop();
        }

        private async void TimerTask(object sender, ElapsedEventArgs e)
        {
            TimerCount++;
            var list = new List<Task>();
            foreach(var (sec, set) in ActionList)
            {
                if (TimerCount % sec == 0)
                    foreach (var func in set) list.Add(func.Invoke());
            }
            if (TimerReset < e.SignalTime)
            {
                TimerCount = 0;
                TimerReset = DateTime.Today.AddDays(1);
                await WatcherTask.Instance.OneDayTask();
                await LocalConsole.Log(this, new LogMessage(LogSeverity.Debug, "Task", "Reset TimerCount."));
            }
            foreach (var task in list) await task;
        }

        protected virtual void Dispose(bool disposing)
        {
            Stop();
            if (!disposed)
            {
                if (disposing)
                {
                    Timer.Dispose();
                }
                disposed = true;
            }
        }
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        ~TimerTaskManager()
        {
            Dispose(disposing: false);
        }
    }
}

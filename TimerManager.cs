using Discord;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using VTuberNotifier.Notification;
using VTuberNotifier.Watcher;
using VTuberNotifier.Watcher.Event;

namespace VTuberNotifier
{
    public class TimerManager : IDisposable
    {
        public static TimerManager Instance { get; private set; } = null;
        public int TimerCount { get; private set; }
        private Dictionary<int, HashSet<Func<Task>>> ActionList { get; }
        private Dictionary<DateTime, HashSet<Func<Task>>> AlarmList { get; }

        public const int Interval = 10;
        private readonly Timer Timer;
        private DateTime TimerReset;
        private bool disposed;

        private TimerManager()
        {
            Timer = new Timer(Interval * 1000);
            Timer.Elapsed += TimerTask;
            var now = DateTime.Now;
            var sec = now.Second % Interval * 1000;
            Task.Delay(Interval * 1000 - sec - now.Millisecond).Wait();
            Timer.Start();
            TimerReset = DateTime.Today.AddDays(1);
            ActionList = new();
            AlarmList = new();
            LocalConsole.Log(this, new LogMessage(LogSeverity.Debug, "Timer", "Timer Start!"));
        }
        public static void CreateInstance()
        {
            if (Instance == null) Instance = new TimerManager();
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
            if (!ActionList.ContainsKey(second)) return;
            ActionList[second].Remove(action);
        }

        public void AddAlarm(DateTime dt, Func<Task> action)
        {
            dt = dt.AddSeconds(-dt.Second).AddMilliseconds(-dt.Millisecond);
            if (!AlarmList.ContainsKey(dt)) AlarmList.Add(dt, new());
            AlarmList[dt].Add(action);
        }
        public void AddEventAlarm<T>(DateTime dt, EventBase<T> evt) where T : INotificationContent
        {
            AddAlarm(dt, new(() => NotifyEvent.Notify(evt)));
        }
        public void RemoveAlarm(DateTime dt, Func<Task> action)
        {
            dt = dt.AddSeconds(-dt.Second).AddMilliseconds(-dt.Millisecond);
            if (!AlarmList.ContainsKey(dt)) return;
            AlarmList[dt].Remove(action);
            if (AlarmList[dt].Count == 0) AlarmList.Remove(dt);
        }
        public void RemoveEventAlarm<T>(DateTime dt, EventBase<T> evt) where T : INotificationContent
        {
            RemoveAlarm(dt, new(() => NotifyEvent.Notify(evt)));
        }

        public void Stop()
        {
            Timer.Stop();
        }

        private async void TimerTask(object sender, ElapsedEventArgs e)
        {
            var now = e.SignalTime;
            TimerCount++;
            var list = new List<Task>();
            foreach(var (sec, set) in ActionList)
            {
                if (TimerCount % sec == 0)
                    foreach (var func in set) list.Add(func.Invoke());
            }
            var dt = now.AddSeconds(-now.Second).AddMilliseconds(-now.Millisecond);
            if (AlarmList.TryGetValue(dt, out var funcs))
            {
                foreach (var func in funcs) await func.Invoke();
                AlarmList.Remove(dt);
            }
            if (TimerReset < now)
            {
                TimerCount = 0;
                TimerReset = DateTime.Today.AddDays(1);
                await WatcherTask.Instance.OneDayTask();
                await LocalConsole.Log(this, new LogMessage(LogSeverity.Info, "Task", "Reset TimerCount."));
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
        ~TimerManager()
        {
            Dispose(disposing: false);
        }
    }
}

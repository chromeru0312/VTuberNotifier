using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using VTuberNotifier.Notification;
using VTuberNotifier.Liver;

namespace VTuberNotifier.Watcher.Event
{
    [JsonConverter(typeof(EventConverter))]
    public abstract class EventBase<T> where T : INotificationContent
    {
        public abstract string EventTypeName { get; }
        public T Item { get; }
        public T OldItem { get; }
        public IReadOnlyDictionary<LiverDetail, EventBase<T>[]> EventsByLiver { get; protected private set; }
        public DateTime CreatedTime { get; }
        [JsonIgnore]
        public abstract string FormatContent { get; }

        protected private Dictionary<string, string> ContentFormat;
        protected private Dictionary<string, IEnumerable<object>> ContentFormatEnumerator;
        protected private Dictionary<string, Func<LiverDetail, IEnumerable<string>>> ContentFormatEnumeratorFunc;

        protected private EventBase(T latest, T old, DateTime dt, bool determine = true)
        {
            if (latest == null && old == null)
                throw new NullReferenceException("Either data must be not null.");

            CreatedTime = dt;
            Item = latest;
            OldItem = old;
            CreateInner();
            if (determine)
            {
                var value = Item ?? OldItem;
                EventsByLiver = new Dictionary<LiverDetail, EventBase<T>[]>(value.Livers.Select(l =>
                    new KeyValuePair<LiverDetail, EventBase<T>[]>(l, new[] { this })));
            }
            else
            {
                EventsByLiver = null;
            }
        }
        private void CreateInner()
        {
            var value = Item ?? OldItem;
            ContentFormat = new(value.ContentFormat);
            ContentFormatEnumerator = new(value.ContentFormatEnumerator);
            ContentFormatEnumeratorFunc = new(value.ContentFormatEnumeratorFunc);
        }

        public T GetContainsItem()
        {
            return Item ?? OldItem;
        }

        public virtual string GetDiscordContent(LiverDetail liver)
        {
            return ConvertContent(FormatContent, liver);
        }
        public string ConvertContent(string format, LiverDetail liver)
        {
            foreach (Match match in Regex.Matches(format,"{.+?}"))
            {
                var tag = match.Value[1..^1].Split(':');
                if (tag.Length > 2) for (int i = 2; i < tag.Length; i++) tag[1] += ':' + tag[i];

                if (ContentFormat.ContainsKey(tag[0]) && tag.Length == 1)
                    format = format.Replace(match.Value, ContentFormat[tag[0]]);
                else if (ContentFormatEnumerator.ContainsKey(tag[0]) && tag.Length > 1)
                    format = format.Replace(match.Value, string.Join(tag[1], ContentFormatEnumerator[tag[0]].Select(o => o.ToString())));
                else if (ContentFormatEnumeratorFunc.ContainsKey(tag[0]) && tag.Length > 1)
                    format = format.Replace(match.Value, string.Join(tag[1], ContentFormatEnumeratorFunc[tag[0]].Invoke(liver)));
                else continue;
            }
            format = format.Replace("\\n", "\n");
            return format;
        }
    }
}
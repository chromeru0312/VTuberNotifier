using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VTuberNotifier.Discord;
using VTuberNotifier.Liver;

namespace VTuberNotifier.Watcher.Event
{
    public abstract class EventBase<T> : IEventBase where T : IDiscordContent
    {
        public T Item { get; }
        public IReadOnlyList<LiverDetail> Livers { get; }
        protected private Dictionary<string, string> ContentFormat => new(Item.ContentFormat);
        protected private Dictionary<string, IEnumerable<object>> ContentFormatEnumerator
            => new(Item.ContentFormatEnumerator);
        protected private Dictionary<string, Func<LiverDetail, IEnumerable<string>>> ContentFormatEnumeratorFunc
            => new(Item.ContentFormatEnumeratorFunc);

        public EventBase(T value, List<LiverDetail> livers)
        {
            Item = value;
            Livers = livers;
        }

        public abstract string GetDiscordContent(LiverDetail liver);
        public string ConvertContent(string format, LiverDetail liver)
        {
            foreach (Match match in Regex.Matches(format, "{.+}"))
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

    public interface IEventBase
    {
        public IReadOnlyList<LiverDetail> Livers { get; }

        public string GetDiscordContent(LiverDetail liver);
        public string ConvertContent(string format, LiverDetail liver);
    }
}

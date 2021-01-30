using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace VTuberNotifier.Discord
{
    interface IDiscordContent
    {
        public IReadOnlyDictionary<string, string> ContentFormat { get; }
        public IReadOnlyDictionary<string, IEnumerable<object>> ContentFormatEnumerator { get; }

        public string GetDiscordContent();
        public string ConvertContent(string format)
        {
            foreach (Match match in Regex.Matches(format, "{.+}"))
            {
                var tag = match.Value[1..^1].Split(':');
                if (tag.Length > 2) for (int i = 2; i < tag.Length; i++) tag[1] += ':' + tag[i];

                if (ContentFormat.ContainsKey(tag[0]) && tag.Length == 1)
                    format = format.Replace(match.Value, ContentFormat[tag[0]]);
                else if (ContentFormatEnumerator.ContainsKey(tag[0]) && tag.Length > 1)
                    format = format.Replace(match.Value, string.Join(tag[1], ContentFormatEnumerator[tag[0]].Select(o => o.ToString())));
                else continue;
            }
            return format;
        }


        public string ConvertDateTime(DateTime dt)
        {
            var now = DateTime.Now;
            if (now.Year != dt.Year) return dt.ToString("yyyy/MM/dd HH:mm");
            else return dt.ToString("MM/dd HH:mm");
        }
        public string ConvertDuringDateTime(DateTime start, DateTime? end = null)
        {
            if (end != null) return $"{ConvertDateTime(start)} ～ {ConvertDateTime((DateTime)end)}";
            else return $"{ConvertDateTime(start)}～"; ;
        }
    }
}

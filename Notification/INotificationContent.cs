using System;
using System.Collections.Generic;
using VTuberNotifier.Liver;

namespace VTuberNotifier.Notification
{
    public interface INotificationContent
    {
        public string Id { get; }
        public IReadOnlyList<LiverDetail> Livers { get; }
        public IReadOnlyDictionary<string, string> ContentFormat { get; }
        public IReadOnlyDictionary<string, IEnumerable<object>> ContentFormatEnumerator { get; }
        public IReadOnlyDictionary<string, Func<LiverDetail, IEnumerable<string>>> ContentFormatEnumeratorFunc { get; }


        public string ConvertDateTime(DateTime dt)
        {
            var now = DateTime.Now;
            if (now.Year != dt.Year) return dt.ToString("yyyy/MM/dd HH:mm");
            else return dt.ToString("MM/dd HH:mm");
        }
        public string ConvertDuringDateTime(DateTime start, DateTime? end = null)
        {
            if (end != null) return $"{ConvertDateTime(start)} ～ {ConvertDateTime((DateTime)end)}";
            else return $"{ConvertDateTime(start)} ～"; ;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VTuberNotifier.Notification
{
    public abstract class NotificationAddress
    {
        public IReadOnlyDictionary<Type, string> MsgContentList { get; protected private set; }

        public NotificationAddress(Dictionary<Type, string> dic = null)
        {
            MsgContentList = dic ?? new();
        }

        public void AddContent(Type type, bool only, string content = null)
        {
            if (MsgContentList.ContainsKey(type)) return;
            AddSetContent(type, only, content);
        }
        public bool GetContent(Type type, out bool only, out string content)
        {
            only = false;
            content = null;
            string str = MsgContentList.ContainsKey(type) ? MsgContentList[type] :
                MsgContentList.FirstOrDefault(p => p.Key.IsAssignableFrom(type)).Value;
            if (str == null) return false;

            only = !str.StartsWith("@F");
            content = !only || str.StartsWith("@T") ? str[2..] : str;
            return true;
        }
        public void SetContent(Type type, bool only, string content)
        {
            if (!MsgContentList.ContainsKey(type)) return;
            AddSetContent(type, only, content);
        }
        private void AddSetContent(Type type, bool only, string content)
        {
            var s = only ? "@T" : "@F";
            MsgContentList = new Dictionary<Type, string>(MsgContentList) { [type] = s + content };
        }
        public void RemoveContent(Type type)
        {
            var dic = new Dictionary<Type, string>(MsgContentList);
            dic.Remove(type);
            MsgContentList = dic;
        }

        public string ConvertContentToJson(string content)
        {
            return JsonSerializer.Serialize(new PostContent() { Content = content });
        }
        private class PostContent
        {
            public string Content { get; set; }
        }

        public abstract class NotificationAddressConverter<T> : JsonConverter<T>
        {
            protected private static Dictionary<Type, string> ReadBase(ref Utf8JsonReader reader)
            {
                reader.Read();
                reader.Read();
                if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();
                var dic = new Dictionary<Type, string>();
                while (true)
                {
                    reader.Read();
                    if (reader.TokenType == JsonTokenType.EndObject) break;
                    var t = Type.GetType(reader.GetString());
                    reader.Read();
                    dic.Add(t, reader.GetString());
                }
                return dic;
            }

            protected private static void WriteBase(Utf8JsonWriter writer, NotificationAddress value)
            {
                writer.WriteStartObject("Content");
                foreach (var (type, content) in value.MsgContentList) writer.WriteString(type.FullName, content);
                writer.WriteEndObject();
            }
        }
    }
}

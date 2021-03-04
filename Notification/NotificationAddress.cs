using System;
using System.Collections.Generic;
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
            if (!MsgContentList.ContainsKey(type)) return false;
            var str = MsgContentList[type];
            if (str.StartsWith("@F"))
            {
                only = false;
                content = str[2..];
            }
            else
            {
                only = true;
                if (str.StartsWith("@T")) content = str[2..];
                else content = str;
            }
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

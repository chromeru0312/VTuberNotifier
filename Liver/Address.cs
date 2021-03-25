using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace VTuberNotifier.Liver
{
    [Serializable]
    [JsonConverter(typeof(AddressConverter))]
    public class Address : IEquatable<Address>
    {
        public int Id { get; }
        public string Name { get; }
        public string YouTubeId { get; }
        public string TwitterId { get; }

        private const string YouTubeUrl = "https://(www\\.)??youtube\\.com/channel/";
        private const string TwitterUrl = "https://twitter\\.com/";

        public Address(int id, string name, string youtube, string twitter)
        {
            Id = id;
            Name = name;

            if (youtube != null) youtube = GetId(youtube, YouTubeUrl);
            YouTubeId = youtube;

            if (twitter != null) twitter = GetId(twitter, TwitterUrl);
            TwitterId = twitter;
        }

        private static string GetId(string content, string baseurl)
        {
            var regex = new Regex(baseurl);
            content = regex.Replace(content, "");
            var i = content.IndexOf('?');
            if (i != -1) content = content[..i];
            content = content.Split('/')[0];
            return content;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }
        public override bool Equals(object obj)
        {
            return obj is Address a && Equals(a);
        }
        public bool Equals(Address other)
        {
            return Id == other.Id;
        }

        public override string ToString()
        {
            return Name;
        }

        public class AddressConverter : JsonConverter<Address>
        {
            public override Address Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

                reader.Read();
                reader.Read();
                var id = reader.GetInt32();
                reader.Read();
                reader.Read();
                var name = reader.GetString();
                reader.Read();
                reader.Read();
                var ytid = reader.GetString();
                reader.Read();
                reader.Read();
                var twid = reader.GetString();

                reader.Read();
                if (reader.TokenType == JsonTokenType.EndObject) return new(id, name, ytid, twid);
                throw new JsonException();
            }

            public override void Write(Utf8JsonWriter writer, Address value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                writer.WriteNumber("Id", value.Id);
                writer.WriteString("Name", value.Name);
                writer.WriteString("YouTubeId", value.YouTubeId);
                writer.WriteString("TwitterId", value.TwitterId);

                writer.WriteEndObject();
            }
        }
    }
}

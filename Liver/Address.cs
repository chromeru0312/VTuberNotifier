using System;

namespace VTuberNotifier.Liver
{
    [Serializable]
    public class Address : IEquatable<Address>
    {
        public int Id { get; }
        public string Name { get; }
        public string YouTubeId { get; }
        public string TwitterId { get; }

        private const string YouTubeUrl = "https://www.youtube.com/channel/";
        private const string TwitterUrl = "https://twitter.com/";

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
            content = content.Replace(baseurl, "");
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

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VTuberNotifier.Liver
{
    [Serializable]
    public class Address
    {
        public string Name { get; }
        public string YouTubeId { get; }
        public string TwitterId { get; }

        private const string YouTubeUrl = "https://www.youtube.com/channel/";
        private const string TwitterUrl = "https://twitter.com/";

        public Address(string name, string youtube, string twitter)
        {
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

        public override string ToString()
        {
            return Name;
        }
    }
}

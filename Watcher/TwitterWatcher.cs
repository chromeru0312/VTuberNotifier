using CoreTweet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VTuberNotifier;
using VTuberNotifier.Liver;

namespace VTuberNotifier.Watcher
{
    public class TwitterWatcher
    {
        public static TwitterWatcher Instance { get; private set; }
        public IReadOnlyDictionary<Address, IReadOnlyList<Tweet>> FoundTweetList { get; private set; }

        private TwitterWatcher()
        {
            var dic = new Dictionary<Address, IReadOnlyList<Tweet>>();
            foreach (var liver in LiverData.GetAllLiversList())
            {
                if (DataManager.Instance.TryDataLoad($"twitter/{liver.TwitterId}", out List<TweetJson> list))
                    dic.Add(liver, new List<Tweet>(list.Select(j => new Tweet(j))));
                else dic.Add(liver, new List<Tweet>());
            }
            foreach (var group in LiverGroup.GroupList)
            {
                if (group.TwitterId == null) continue;
                if (DataManager.Instance.TryDataLoad($"twitter/{group.TwitterId}", out List<TweetJson> list))
                    dic.Add(group, new List<Tweet>(list.Select(j => new Tweet(j))));
                else dic.Add(group, new List<Tweet>());
            }
            FoundTweetList = dic;
        }
        public static void CreateInstance()
        {
            if (Instance != null) return;
            Instance = new TwitterWatcher();
        }

        public async Task<List<Tweet>> GetNewTweets(Address address)
        {
            if (!FoundTweetList.ContainsKey(address))
                FoundTweetList = new Dictionary<Address, IReadOnlyList<Tweet>>(FoundTweetList) { { address, new List<Tweet>() } };
            SearchResult result;
            if (FoundTweetList[address].Count == 0 ) result = await SettingData.TwitterToken.Search.TweetsAsync(count => 20,
                q => $"from:{address.TwitterId}", result_type => "recent", trim_user => true, tweet_mode => "extended");
            else result = await SettingData.TwitterToken.Search.TweetsAsync(q => $"from:{address.TwitterId}", result_type => "recent",
                since_id => FoundTweetList[address][FoundTweetList[address].Count - 1].Id, trim_user => true, tweet_mode => "extended");
            var list = new List<Tweet>();
            foreach(var t in result)
            {
                if (t == null) continue;
                list.Add(new Tweet(t));
            }
            if (list.Count > 0)
            {
                FoundTweetList = new Dictionary<Address, IReadOnlyList<Tweet>>(FoundTweetList)
                { [address] = new List<Tweet>(FoundTweetList[address].Concat(list)) };
                await DataManager.Instance.DataSaveAsync($"twitter/{address.TwitterId}", FoundTweetList[address], true);
            }
            return list;
        }
    }

    public class Tweet
    {
        public long Id { get; }
        public long UserId { get; }
        public string UserName { get; }
        public bool IsReply { get; }
        public bool IsRetweeted { get; }
        public Tweet RetweetedTweet { get; }
        public bool IsQuoted { get; }
        public Tweet QuotedTweet { get; }
        public TwitterURL[] URLs { get; }

        public Tweet(Status status)
        {
            Id = status.Id;
            if(status.User.Id != null)
            {
                UserId = (long)status.User.Id;
                UserName = status.User.ScreenName;
            }
            IsReply = status.InReplyToScreenName != null;
            IsRetweeted = status.RetweetedStatus != null;
            if (IsRetweeted) RetweetedTweet = new Tweet(status.RetweetedStatus);
            IsQuoted = status.QuotedStatus != null;
            if (IsQuoted) QuotedTweet = new Tweet(status.QuotedStatus);
            URLs = status.Entities.Urls.Select(e => new TwitterURL(e)).ToArray();
        }

        public Tweet(TweetJson json)
        {
            Id = json.Id;
            UserId = json.UserId;
            UserName = json.UserName;
            IsReply = json.IsReply;
            IsRetweeted = json.IsRetweeted;
            RetweetedTweet = json.IsRetweeted ? new(json.RetweetedTweet) : null;
            IsQuoted = json.IsQuoted;
            QuotedTweet = json.IsQuoted ? new(json.QuotedTweet) : null;
            URLs = json.URLs;
        }
    }

    [Serializable]
    public class TweetJson
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public string UserName { get; set; }
        public bool IsReply { get; set; }
        public bool IsRetweeted { get; set; }
        public TweetJson RetweetedTweet { get; set; }
        public bool IsQuoted { get; set; }
        public TweetJson QuotedTweet { get; set; }
        public TwitterURL[] URLs { get; set; }
    }

    [Serializable]
    public struct TwitterURL
    {
        public string URL { get; set; }
        public string ExpandedURL { get; set; }

        public TwitterURL(UrlEntity entity)
        {
            URL = entity.Url;
            ExpandedURL = entity.ExpandedUrl;
        }
    }
}

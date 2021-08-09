using CoreTweet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VTuberNotifier.Liver;

namespace VTuberNotifier.Watcher
{
    public class TwitterWatcher
    {
        public static TwitterWatcher Instance { get; private set; }
        public IReadOnlyDictionary<Address, IReadOnlyList<TweetItem>> FoundTweetList { get; private set; }

        private TwitterWatcher()
        {
            var dic = new Dictionary<Address, IReadOnlyList<TweetItem>>();
            foreach (var liver in LiverData.GetAllLiversList())
            {
                if (DataManager.Instance.TryDataLoad($"twitter/{liver.TwitterId}", out List<TweetItem> list))
                    dic.Add(liver, list);
                else dic.Add(liver, new List<TweetItem>());
            }
            foreach (var group in LiverGroup.GroupList)
            {
                if (group.TwitterId == null) continue;
                if (DataManager.Instance.TryDataLoad($"twitter/{group.TwitterId}", out List<TweetItem> list))
                    dic.Add(group, list);
                else dic.Add(group, new List<TweetItem>());
            }
            FoundTweetList = dic;
        }
        public static void CreateInstance()
        {
            if (Instance != null) return;
            Instance = new TwitterWatcher();
        }

        public async Task<List<TweetItem>> GetNewTweets(Address address)
        {
            if (!FoundTweetList.ContainsKey(address))
                FoundTweetList = new Dictionary<Address, IReadOnlyList<TweetItem>>(FoundTweetList) { { address, new List<TweetItem>() } };
            SearchResult result;
            if (FoundTweetList[address].Count == 0 ) result = await Settings.Data.TwitterToken.Search.TweetsAsync(count => 20,
                q => $"from:{address.TwitterId}", result_type => "recent", trim_user => true, tweet_mode => "extended");
            else result = await Settings.Data.TwitterToken.Search.TweetsAsync(q => $"from:{address.TwitterId}", result_type => "recent",
                since_id => FoundTweetList[address][FoundTweetList[address].Count - 1].Id, trim_user => true, tweet_mode => "extended");
            var list = new List<TweetItem>();
            foreach(var t in result)
            {
                if (t == null) continue;
                list.Add(new TweetItem(t));
            }
            if (list.Count > 0)
            {
                FoundTweetList = new Dictionary<Address, IReadOnlyList<TweetItem>>(FoundTweetList)
                { [address] = new List<TweetItem>(FoundTweetList[address].Concat(list)) };
                await DataManager.Instance.DataSaveAsync($"twitter/{address.TwitterId}", FoundTweetList[address], true);
            }
            return list;
        }
    }

    [Serializable]
    [JsonConverter(typeof(TweetConverter))]
    public class TweetItem
    {
        public long Id { get; }
        public long UserId { get; }
        public string UserName { get; }
        public TextContent Content { get; }
        public bool IsReply { get; }
        public bool IsRetweeted { get; }
        public TweetItem RetweetedTweet { get; }
        public bool IsQuoted { get; }
        public TweetItem QuotedTweet { get; }

        public TweetItem(Status status)
        {
            Id = status.Id;
            if(status.User.Id != null)
            {
                UserId = (long)status.User.Id;
                UserName = status.User.ScreenName;
            }
            Content = new(status.FullText, status.Entities.Urls);
            IsReply = status.InReplyToScreenName != null;
            IsRetweeted = status.RetweetedStatus != null;
            if (IsRetweeted) RetweetedTweet = new TweetItem(status.RetweetedStatus);
            IsQuoted = status.QuotedStatus != null;
            if (IsQuoted) QuotedTweet = new TweetItem(status.QuotedStatus);
        }
        private TweetItem(long id, long user_id, string user_name, TextContent content, bool reply, TweetItem retweet, TweetItem quote)
        {
            Id = id;
            UserId = user_id;
            UserName = user_name;
            Content = content;
            IsReply = reply;
            IsRetweeted = retweet != null;
            RetweetedTweet = retweet;
            IsQuoted = quote != null;
            QuotedTweet = quote;
        }

        public class TweetConverter : JsonConverter<TweetItem>
        {
            public override TweetItem Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                reader.CheckStartToken();

                var id = reader.GetNextValue<long>();
                var user = reader.GetNextValue<long>();
                var name = reader.GetNextValue<string>(options);
                var content = reader.GetNextValue<TextContent>(options);
                var reply = reader.GetNextValue<bool>(options);
                var retweet = reader.GetNextValue<TweetItem>(options);
                var quote = reader.GetNextValue<TweetItem>(options);

                reader.CheckEndToken();
                return new(id, user, name, content, reply, retweet, quote);
            }

            public override void Write(Utf8JsonWriter writer, TweetItem value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                writer.WriteNumber("Id", value.Id);
                writer.WriteNumber("UserId", value.UserId);
                writer.WriteString("UserName", value.UserName);
                writer.WriteValue("Content", value.Content, options);
                writer.WriteBoolean("IsReply", value.IsReply);
                writer.WriteValue("RetweetedTweet", value.RetweetedTweet, options);
                writer.WriteValue("QuotedTweet", value.QuotedTweet, options);

                writer.WriteEndObject();
            }
        }
    }
}
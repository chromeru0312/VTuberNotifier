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
        public IReadOnlyDictionary<Address, IReadOnlyList<Tweet>> FoundTweetList { get; private set; }

        private TwitterWatcher()
        {
            var dic = new Dictionary<Address, IReadOnlyList<Tweet>>();
            foreach (var liver in LiverData.GetAllLiversList())
            {
                if (DataManager.Instance.TryDataLoad($"twitter/{liver.TwitterId}", out List<Tweet> list))
                    dic.Add(liver, list);
                else dic.Add(liver, new List<Tweet>());
            }
            foreach (var group in LiverGroup.GroupList)
            {
                if (group.TwitterId == null) continue;
                if (DataManager.Instance.TryDataLoad($"twitter/{group.TwitterId}", out List<Tweet> list))
                    dic.Add(group, list);
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

    [Serializable]
    [JsonConverter(typeof(TweetConverter))]
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
        public TwitterUrl[] Urls { get; }

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
            Urls = status.Entities.Urls.Select(e => new TwitterUrl(e)).ToArray();
        }
        private Tweet(long id, long user_id, string user_name, bool reply, Tweet retweet, Tweet quote, TwitterUrl[] urls)
        {
            Id = id;
            UserId = user_id;
            UserName = user_name;
            IsReply = reply;
            IsRetweeted = retweet != null;
            RetweetedTweet = retweet;
            IsQuoted = quote != null;
            QuotedTweet = quote;
            Urls = urls;
        }

        public class TweetConverter : JsonConverter<Tweet>
        {
            public override Tweet Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

                reader.Read();
                reader.Read();
                var id = reader.GetInt64();
                reader.Read();
                reader.Read();
                var user = reader.GetInt64();
                reader.Read();
                reader.Read();
                var name = reader.GetString();
                reader.Read();
                reader.Read();
                var reply = reader.GetBoolean();
                reader.Read();
                reader.Read();
                var retweet = JsonSerializer.Deserialize<Tweet>(ref reader, options);
                reader.Read();
                reader.Read();
                var quote = JsonSerializer.Deserialize<Tweet>(ref reader, options);
                reader.Read();
                reader.Read();
                var urls = JsonSerializer.Deserialize<TwitterUrl[]>(ref reader, options);

                reader.Read();
                if (reader.TokenType == JsonTokenType.EndObject) return new(id, user, name, reply, retweet, quote, urls);
                throw new JsonException();
            }

            public override void Write(Utf8JsonWriter writer, Tweet value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                writer.WriteNumber("Id", value.Id);
                writer.WriteNumber("UserId", value.UserId);
                writer.WriteString("UserName", value.UserName);
                writer.WriteBoolean("IsReply", value.IsReply);

                writer.WritePropertyName("RetweetedTweet");
                if (value.IsRetweeted) JsonSerializer.Serialize(writer, value.RetweetedTweet, options);
                else writer.WriteNullValue();
                writer.WritePropertyName("QuotedTweet");
                if (value.IsQuoted) JsonSerializer.Serialize(writer, value.QuotedTweet, options);
                else writer.WriteNullValue();

                writer.WritePropertyName("Urls");
                JsonSerializer.Serialize(writer, value.Urls, options);

                writer.WriteEndObject();
            }
        }
    }

    [Serializable]
    [JsonConverter(typeof(TwitterUrlConverter))]
    public struct TwitterUrl
    {
        public string Url { get; set; }
        public string ExpandedUrl { get; set; }

        public TwitterUrl(UrlEntity entity)
        {
            Url = entity.Url;
            ExpandedUrl = entity.ExpandedUrl;
        }
        private TwitterUrl(string url, string exp_url)
        {
            Url = url;
            ExpandedUrl = exp_url;
        }

        public class TwitterUrlConverter : JsonConverter<TwitterUrl>
        {
            public override TwitterUrl Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

                reader.Read();
                reader.Read();
                var url= reader.GetString();
                reader.Read();
                reader.Read();
                var exurl = reader.GetString();

                reader.Read();
                if (reader.TokenType == JsonTokenType.EndObject) return new(url, exurl);
                throw new JsonException();
            }

            public override void Write(Utf8JsonWriter writer, TwitterUrl value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                writer.WriteString("Url", value.Url);
                writer.WriteString("ExpandedUrl", value.ExpandedUrl);

                writer.WriteEndObject();
            }
        }
    }
}

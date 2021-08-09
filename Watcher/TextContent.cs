using CoreTweet;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VTuberNotifier.Liver;

namespace VTuberNotifier.Watcher
{
    [Serializable]
    [JsonConverter(typeof(TextContentConverter))]
    public class TextContent : IEquatable<TextContent>
    {
        public string Content { get; }
        public string TrimedContent { get; }
        public IReadOnlyList<ContentUrl> Urls { get; }

        public TextContent(string content)
            : this(content, DetectLinks(content)) { }
        private static List<ContentUrl> DetectLinks(string content)
        {
            var list = new List<ContentUrl>();
            foreach (Match match in Regex.Matches(content,
                "https?://([\\w-]+\\.)+[\\w-]+(/[\\w-./?%&=]*)?($|\\s|)"))
            {
                var url = match.Value.Replace("&amp;", "&").Trim();
                foreach (Match special in Regex.Matches(url, "&\\w+?;"))
                    url = url.Replace(special.Value, "");
                if (url.EndsWith(')') && !url.Contains('('))
                    url = url[..^1];
                list.Add(ContentUrl.CreateContentUrl(url).GetAwaiter().GetResult());
            }
            return list;
        }
        public TextContent(string content, IEnumerable<string> links)
            : this(content, CreateContainsLinks(content, links)) { }
        private static List<ContentUrl> CreateContainsLinks(string content, IEnumerable<string> links)
        {
            if (links == null) return DetectLinks(content);
            var list = new List<ContentUrl>();
            foreach (var link in links)
            {
                if (content.Contains(link))
                    list.Add(ContentUrl.CreateContentUrl(link.Replace("&amp;", "&")).GetAwaiter().GetResult());
            }
            return list;
        }

        public TextContent(string content, UrlEntity[] entities)
            : this(content, new List<ContentUrl>(entities.Select(e => ContentUrl.CreateContentUrl(e)))) { }
        private TextContent(string content, List<ContentUrl> urls)
        {
            Content = content;
            TrimedContent = content.Trim();
            Urls = urls;
        }

        public override bool Equals(object obj) => obj is TextContent tc && Equals(tc);
        public bool Equals(TextContent other) => Content == other.Content;
        public override int GetHashCode() => HashCode.Combine(Content);
        public static bool operator ==(TextContent a, TextContent b) => a?.Equals(b) ?? false;
        public static bool operator !=(TextContent a, TextContent b) => !(a == b);

        public class TextContentConverter : JsonConverter<TextContent>
        {
            public override TextContent Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                reader.CheckStartToken();

                var content = reader.GetNextValue<string>(options);
                var urls = reader.GetNextValue<List<ContentUrl>>(options);

                reader.CheckEndToken();
                return new(content, urls);
            }

            public override void Write(Utf8JsonWriter writer, TextContent value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                writer.WriteString("Content", value.Content);
                writer.WriteValue("Urls", value.Urls);

                writer.WriteEndObject();
            }
        }
    }

    [Serializable]
    [JsonConverter(typeof(ContentUrlConverter))]
    public class ContentUrl
    {
        public string Url { get; }
        public string ExpandedUrl { get; }
        public LiverDetail Liver { get; }

        private ContentUrl(string url, string exp_url)
        {
            Url = url;
            ExpandedUrl = exp_url;
        }
        public static ContentUrl CreateContentUrl(UrlEntity entity)
        {
            return new(entity.Url, entity.ExpandedUrl);
        }
        public static async Task<ContentUrl> CreateContentUrl(string url, string raw = null)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Head, url);
                var res = await Settings.Data.HttpClient.SendAsync(req);
                var code = (int)res.StatusCode;
                var code_first = code / 100;
                if (code_first == 2 || code == 405)
                    return new(raw ?? url, res.RequestMessage.RequestUri.ToString());
                else if (code_first == 3)
                    return await CreateContentUrl(res.Headers.Location.ToString(), url);
                else if (code == (int)HttpStatusCode.TooManyRequests || code == 999)
                    return new(raw ?? url, res.RequestMessage.RequestUri.ToString());
                else
                {
                    LocalConsole.Log("ContentUrl", new(LogSeverity.Error, "Creator", $"This url returned bad status code:{code}. {url}"));
                    return new(url, null);
                }
            }
            catch (Exception e)
            {
                LocalConsole.Log("ContentUrl", new(LogSeverity.Critical, "Creator", $"An error occured while sending request. {url}", e));
                return new(url, null);
            }
        }

        public class ContentUrlConverter : JsonConverter<ContentUrl>
        {
            public override ContentUrl Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                reader.CheckStartToken();

                var url = reader.GetNextValue<string>(options);
                var exurl = reader.GetNextValue<string>(options);

                reader.CheckEndToken();
                return new(url, exurl);
            }

            public override void Write(Utf8JsonWriter writer, ContentUrl value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                writer.WriteString("Url", value.Url);
                writer.WriteString("ExpandedUrl", value.ExpandedUrl);

                writer.WriteEndObject();
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace VTuberNotifier.Liver
{
    public static class LiverData
    {
        private static HashSet<LiverDetail> Livers = null;
        private static Dictionary<LiverGroupDetail, HashSet<LiverDetail>> LiversSeparateGroup = null;

        internal async static Task UpdateLivers()
        {
            var olddic = LiversSeparateGroup;
            var newdic = new Dictionary<LiverGroupDetail, HashSet<LiverDetail>>();
            var tasks = new Dictionary<LiverGroupDetail, Task<HashSet<LiverDetail>>>();

            foreach (var group in LiverGroup.GroupList)
            {
                if (DataManager.Instance.TryDataLoad($"liver/{group.GroupId}", out HashSet<LiverDetail> livers))
                    tasks.Add(group, group.LoadMembers(livers));
                else tasks.Add(group, group.LoadMembers(null));
            }
            foreach (var (group, task) in tasks) newdic.Add(group, await task);

            if (!olddic.SequenceEqual(newdic))
            {
                await SaveLivers();
                Livers = null;
                LiversSeparateGroup = newdic;
            }
        }
        private async static Task LoadLivers()
        {
            Livers = null;
            LiversSeparateGroup = new();
            var tasks = new Dictionary<LiverGroupDetail, Task<HashSet<LiverDetail>>>();

            foreach (var group in LiverGroup.GroupList)
            {
                if (DataManager.Instance.TryDataLoad($"liver/{group.GroupId}", out HashSet<LiverDetail> livers))
                    LiversSeparateGroup.Add(group, livers ?? new());
                else tasks.Add(group, group.LoadMembers(null));
            }
            foreach (var (group, task) in tasks) LiversSeparateGroup.Add(group, await task ?? new());
            await SaveLivers();
        }

        private static async Task SaveLivers()
        {
            foreach (var (group, set) in LiversSeparateGroup)
                await DataManager.Instance.DataSaveAsync($"liver/{group.GroupId}", set, true);
        }

        public static HashSet<LiverDetail> GetLiversList(LiverGroupDetail group)
        {
            if (LiversSeparateGroup == null) LoadLivers().Wait();
            return LiversSeparateGroup[group];
        }
        public static HashSet<LiverDetail> GetAllLiversList()
        {
            if (LiversSeparateGroup == null) LoadLivers().Wait();
            if (Livers == null)
            {
                Livers = new();
                foreach (var pair in LiversSeparateGroup) 
                    if(pair.Value != null) Livers.UnionWith(pair.Value);
            }
            return Livers;
        }

        public static LiverDetail GetLiverFromId(int id, LiverGroupDetail group = null)
        {
            var list = group == null ? GetAllLiversList() : GetLiversList(group);
            return list.FirstOrDefault(l => l.Id == id);
        }
        public static List<LiverDetail> GetLiversFromName(string name, LiverGroupDetail group = null)
        {
            var list = group == null ? GetAllLiversList() : GetLiversList(group);
            return new List<LiverDetail>(list.Where(l => l.Name.Contains(name) || l.ChannelName.Contains(name)));
        }
        public static LiverDetail GetLiverFromNameMatch(string name, LiverGroupDetail group = null)
        {
            var list = group == null ? GetAllLiversList() : GetLiversList(group);
            return list.FirstOrDefault(l => l.Name == name || l.ChannelName == name);
        }
        public static LiverDetail GetLiverFromYouTubeId(string id, LiverGroupDetail group = null)
        {
            var list = group == null ? GetAllLiversList() : GetLiversList(group);
            return list.FirstOrDefault(l => l.YouTubeId == id);
        }
        public static LiverDetail GetLiverFromTwitterId(string id, LiverGroupDetail group = null)
        {
            var list = group == null ? GetAllLiversList() : GetLiversList(group);
            return list.FirstOrDefault(l => l.TwitterId == id);
        }

        public static bool DetectLiver(string liver, out LiverDetail detail)
        {
            var search = liver.Split('=');

            if (search.Length == 1) detail = GetLiverFromNameMatch(search[0]);
            else
            {
                if (search.Length > 2) for (int i = 2; i < search.Length; i++) search[1] += '=' + search[i];

                if (search[0] == "name") detail = GetLiverFromNameMatch(search[1]);
                else if (search[0] == "youtube") detail = GetLiverFromYouTubeId(search[1]);
                else if (search[0] == "twitter") detail = GetLiverFromTwitterId(search[1]);
                else detail = null;
            }
            return detail != null;
        }

        internal static async Task<int> AddLiver(string group_str, string name, string youtube, string twitter)
        {
            var group = LiverGroup.GroupList.FirstOrDefault(g => g.GroupId == group_str);
            if (group == null) return 404;
            if (group.IsAutoLoad) return 403;
            var id = LiversSeparateGroup[group].Max(l => l.Id) + 1;
            var liver = new LiverDetail(id == 1 ? group.Id + 1 : id, group, name, youtube, twitter);
            if (LiversSeparateGroup[group].FirstOrDefault(l => l.YouTubeId == youtube) == null)
            {
                await liver.SetAutoChannelName();
                Livers.Add(liver);
                LiversSeparateGroup[group].Add(liver);
                await SaveLivers();
                return 201;
            }
            return 400;
        }
        internal static async Task<int> UpdateLiver(int id, string name = null, string youtube = null, string twitter = null)
        {
            var set = GetAllLiversList();
            var liver = set.FirstOrDefault(c => c.Id == id);
            if (liver == null) return 404;
            if (liver.Group.IsAutoLoad) return 403;
            set.Remove(liver);
            var updated = new LiverDetail(id, liver.Group, name ?? liver.Name, youtube ?? liver.YouTubeId, twitter ?? liver.TwitterId);
            var b = set.Add(updated);
            if (b)
            {
                Livers = set;
                LiversSeparateGroup[liver.Group].Remove(liver);
                LiversSeparateGroup[liver.Group].Add(updated);
                await SaveLivers();
                return 200;
            }
            return 400;
        }
        internal static async Task<int> DeleteLiver(int id)
        {
            var liver = GetLiverFromId(id);
            if (liver == null) return 404;
            if (liver.Group.IsAutoLoad) return 403;
            Livers.Remove(liver);
            LiversSeparateGroup[liver.Group].Remove(liver);
            await SaveLivers();
            return 200;
        }
    }


    [Serializable]
    [JsonConverter(typeof(LiverDetailConverter))]
    public class LiverDetail : Address
    {
        public LiverGroupDetail Group { get; }
        public string ChannelName { get; private set; }
        public string NicoTag { get; internal set; }

        public LiverDetail(int id, LiverGroupDetail group, string name, string youtube, string twitter, string youtube_name = null)
            : base(id, name, youtube, twitter)
        {
            Group = group;
            ChannelName = youtube_name;
            NicoTag = name;
        }

        internal async Task SetAutoChannelName()
        {
            if (YouTubeId == null) return;
            try
            {
                var req = Settings.Data.YouTubeService.Channels.List("snippet");
                req.Id = YouTubeId;
                var res = await req.ExecuteAsync();
                ChannelName = res.Items[0].Snippet.Title;
            }
            catch { }
        }

        public class LiverDetailConverter : JsonConverter<LiverDetail>
        {
            public override LiverDetail Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                reader.CheckStartToken();

                var id = reader.GetNextValue<int>(options);
                var name = reader.GetNextValue<string>(options);
                var gid = reader.GetNextValue<int>(options);
                var group = LiverGroup.GroupList.FirstOrDefault(g => g.Id == gid * 10000);
                var ytid = reader.GetNextValue<string>(options);
                var chname = reader.GetNextValue<string>(options);
                var twid = reader.GetNextValue<string>(options);

                reader.CheckEndToken();
                return new(id, group, name, ytid, twid, chname);
            }

            public override void Write(Utf8JsonWriter writer, LiverDetail value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                writer.WriteNumber("Id", value.Id);
                writer.WriteString("Name", value.Name);
                writer.WriteNumber("Group", value.Group.Id / 10000);
                writer.WriteString("YouTubeId", value.YouTubeId);
                writer.WriteString("ChannelName", value.ChannelName);
                writer.WriteString("TwitterId", value.TwitterId);

                writer.WriteEndObject();
            }
        }
    }
}
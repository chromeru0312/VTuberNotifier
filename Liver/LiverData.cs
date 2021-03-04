﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VTuberNotifier.Notification;

namespace VTuberNotifier.Liver
{
    public static class LiverData
    {
        private static HashSet<LiverDetail> Livers = null;
        private static Dictionary<LiverGroupDetail, HashSet<LiverDetail>> LiversSeparateGroup = null;

        internal async static Task UpdateLivers()
        {
            var olddic = LiversSeparateGroup;
            var newdic = await InspectLivers();
            if (olddic != newdic)
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
                    LiversSeparateGroup.Add(group, livers);
                else tasks.Add(group, group.LoadMembers(null));
            }
            foreach (var (group, task) in tasks) LiversSeparateGroup.Add(group, await task);
            await SaveLivers();
            NotifyEvent.LoadChannelList();
        }

        private static async Task SaveLivers()
        {
            foreach (var (group, set) in LiversSeparateGroup)
                await DataManager.Instance.DataSaveAsync($"liver/{group.GroupId}", set);
        }

        private static async Task<Dictionary<LiverGroupDetail, HashSet<LiverDetail>>> InspectLivers(List<LiverGroupDetail> list = null)
        {
            if(list == null) list = new(LiverGroup.GroupList);
            var dic = new Dictionary<LiverGroupDetail, HashSet<LiverDetail>>();
            var tasks = new Dictionary<LiverGroupDetail, Task<HashSet<LiverDetail>>>();

            foreach (var group in list)
            {
                HashSet<LiverDetail> set = LiversSeparateGroup.ContainsKey(group) ? LiversSeparateGroup[group] : null;
                tasks.Add(group, group.LoadMembers(set));
            }
            foreach (var (group, task) in tasks) dic.Add(group, await task);
            return dic;
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
                foreach (var pair in LiversSeparateGroup) Livers.UnionWith(pair.Value);
            }
            return Livers;
        }

        public static List<LiverDetail> GetLiverFromName(string name, LiverGroupDetail group = null)
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

            if (search.Length == 1) detail = LiverData.GetLiverFromNameMatch(search[0]);
            else
            {
                if (search.Length > 2) for (int i = 2; i < search.Length; i++) search[1] += '=' + search[i];

                if (search[0] == "name") detail = LiverData.GetLiverFromNameMatch(search[1]);
                else if (search[0] == "youtube") detail = LiverData.GetLiverFromYouTubeId(search[1]);
                else if (search[0] == "twitter") detail = LiverData.GetLiverFromTwitterId(search[1]);
                else detail = null;
            }
            return detail != null;
        }
    }


    [Serializable]
    [JsonConverter(typeof(LiverDetailConverter))]
    public class LiverDetail : Address
    {
        public LiverGroupDetail Group { get; private set; }
        public string ChannelName { get; private set; }

        public LiverDetail(int id, LiverGroupDetail group, string name, string youtube, string twitter, string youtube_name = null)
            : base(id, name, youtube, twitter)
        {
            Group = group;
            ChannelName = youtube_name;
        }

        internal void SetChannelName(string name)
        {
            ChannelName = name;
        }

        public class LiverDetailConverter : JsonConverter<LiverDetail>
        {
            public override LiverDetail Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
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
                var gid = reader.GetInt32();
                var group = LiverGroup.GroupList.FirstOrDefault(g => g.Id == gid * 10000);
                reader.Read();
                reader.Read();
                var ytid = reader.GetString();
                reader.Read();
                reader.Read();
                var chname = reader.GetString();
                reader.Read();
                reader.Read();
                var twid = reader.GetString();

                reader.Read();
                if (reader.TokenType == JsonTokenType.EndObject) return new(id, group, name, ytid, twid, chname);
                throw new JsonException();
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

using Discord;
using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace VTuberNotifier
{
    public class DataManager
    {
        public static DataManager Instance { get; private set; } = null;
        public string DataPath { get; }

        private DataManager()
        {
            string path = Assembly.GetEntryAssembly().Location;
            var dir = Path.GetDirectoryName(path);
            DataPath = Path.Combine(dir, "data");
            if (!Directory.Exists(DataPath)) Directory.CreateDirectory(DataPath);
        }
        public static void CreateInstance()
        {
            if (Instance == null) Instance = new DataManager();
        }

        public bool DataSave<T>(string id, T obj, bool update = false, JsonSerializerOptions options = null)
        {
            return DataSaveAsync(id, obj, update, options).Result;
        }

        public async Task<bool> DataSaveAsync<T>(string id, T obj, bool update = false, JsonSerializerOptions options = null)
        {
            var path = GetPathFromId(id);
            if (File.Exists(path) && !update) return false;

            try
            {
                using var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write);
                using var sw = new StreamWriter(fs);
                var json = JsonSerializer.Serialize(obj, options);
                await sw.WriteAsync(json);
                await LocalConsole.Log(this, new LogMessage(LogSeverity.Info, "SaveSystem", $"File Saved[ID-{id}]."));
                return true;
            }
            catch (Exception e)
            {
                if (File.Exists(path)) File.Delete(path);
                await LocalConsole.Log(this, new LogMessage(LogSeverity.Error, "SaveSystem", $"File Saving is failed[ID-{id}].", e));
                return false;
            }
        }

        public T DataLoad<T>(string id, JsonSerializerOptions options = null)
        {
            if (TryDataLoad(id, out T obj, options))
            {
                return obj;
            }
            else
            {
                LocalConsole.Log(this, new LogMessage(LogSeverity.Error, "LoadSystem", $"File is not found[ID-{id}]."));
                return default;
            }
        }
        public bool TryDataLoad<T>(string id, out T obj, JsonSerializerOptions options = null)
        {
            var path = GetPathFromId(id);
            if (!File.Exists(path))
            {
                obj = default;
                return false;
            }

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var sr = new StreamReader(fs);
            obj = JsonSerializer.Deserialize<T>(sr.ReadToEnd(), options);
            LocalConsole.Log(this, new LogMessage(LogSeverity.Info, "LoadSystem", $"File Loaded[ID-{id}]."));
            return true;
        }
        public T InitDataLoad<T>(string id) where T : new()
        {
            var data = DataLoad<T>(id);
            if (data.Equals(default(T))) data = new T();
            return data;
        }

        public bool DataRemove(string id)
        {
            var path = GetPathFromId(id);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
        public async Task<bool> DataRemoveAsync(string id)
        {
            return await Task.Run(() => DataRemove(id));
        }

        private string GetPathFromId(string id)
        {
            var split = id.Split('/');
            string path = DataPath;
            for (int i = 0; i < split.Length - 1; i++) path = Path.Combine(path, split[i]);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            path = Path.Combine(path, split[^1] + ".data");
            return path;
        }
    }
}

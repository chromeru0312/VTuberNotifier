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

        public bool StringSave(string id, string extention, string obj)
        {
            return StringSaveAsync(id, extention, obj).Result;
        }
        public async Task<bool> StringSaveAsync(string id, string extention, string obj)
        {
            return await DataSaveBase(id, extention, obj);
        }

        public bool DataSave<T>(string id, T obj, bool update = false, JsonSerializerOptions options = null)
        {
            return DataSaveAsync(id, obj, update, options).Result;
        }
        public async Task<bool> DataSaveAsync<T>(string id, T obj, bool update = false, JsonSerializerOptions options = null)
        {
            return await DataSaveBase(id, ".data", JsonSerializer.Serialize(obj, options), update);
        }

        private async Task<bool> DataSaveBase(string id, string extention, string obj, bool update = false)
        {
            var (temp, path) = GetPathFromId(id, extention);
            if (File.Exists(path) && !update) return false;

            try
            {
                var fs = new FileStream(temp, FileMode.Create, FileAccess.Write);
                var sw = new StreamWriter(fs);
                await sw.WriteAsync(obj);
                await sw.DisposeAsync();
                await fs.DisposeAsync();
                if (update) File.Delete(path);
                File.Move(temp, path);
                await LocalConsole.Log(this, new LogMessage(LogSeverity.Info, "SaveSystem", $"File Saved[ID-{id}]."));
                return true;
            }
            catch (Exception e)
            {
                if (File.Exists(temp)) File.Delete(temp);
                await LocalConsole.Log(this, new LogMessage(LogSeverity.Error, "SaveSystem", $"File Saving is failed[ID-{id}].", e));
                return false;
            }
        }

        public string StringLoad(string id)
        {
            if (TryStringLoad(id, out var str))
            {
                return str;
            }
            else
            {
                LocalConsole.Log(this, new LogMessage(LogSeverity.Warning, "LoadSystem", $"File is not found[ID-{id}]."));
                return null;
            }
        }
        public bool TryStringLoad(string id, out string str)
        {
            return LoadBase(id, out str);
        }
        public T DataLoad<T>(string id, JsonSerializerOptions options = null)
        {
            if (TryDataLoad(id, out T obj, options))
            {
                return obj;
            }
            else
            {
                LocalConsole.Log(this, new LogMessage(LogSeverity.Warning, "LoadSystem", $"File is not found[ID-{id}]."));
                return default;
            }
        }
        public bool TryDataLoad<T>(string id, out T obj, JsonSerializerOptions options = null)
        {
            if (LoadBase(id, out var str))
            {
                obj = JsonSerializer.Deserialize<T>(str, options);
                return true;
            }
            else
            {
                obj = default;
                return false;
            }
        }
        private bool LoadBase(string id, out string obj)
        {
            var (_, path) = GetPathFromId(id);
            if (!File.Exists(path))
            {
                obj = null;
                return false;
            }

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var sr = new StreamReader(fs);
            obj = sr.ReadToEnd();
            LocalConsole.Log(this, new LogMessage(LogSeverity.Info, "LoadSystem", $"File Loaded[ID-{id}]."));
            return true;
        }

        public bool DataRemove(string id)
        {
            var (_, path) = GetPathFromId(id);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
        public async Task<bool> DataRemoveAsync(string id)
        {
            return await Task.Run(() => DataRemove(id));
        }

        private (string temp, string data) GetPathFromId(string id, string extention = ".data")
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            else if (string.IsNullOrEmpty(extention)) throw new ArgumentNullException(nameof(extention));
            if (!extention.StartsWith('.')) extention = '.' + extention;
            var split = id.Split('/');
            var path = DataPath;
            for (int i = 0; i < split.Length - 1; i++) path = Path.Combine(path, split[i]);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            var data = Path.Combine(path, split[^1] + extention);
            var temp = Path.Combine(path, split[^1] + ".tmp");
            return (temp, data);
        }
    }
}

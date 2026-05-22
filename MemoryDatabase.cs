using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;

namespace ImplantiAI
{
    public class MemoryDatabase
    {
        private static MemoryDatabase? _instance;
        public static MemoryDatabase Instance => _instance ??= new MemoryDatabase();

        private readonly string _dataPath;
        private string _apiKey = "";
        private Dictionary<string, ProjectData> _projects = new();

        private MemoryDatabase()
        {
            _dataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ANGImpianti");
        }

        public void Initialize()
        {
            Directory.CreateDirectory(_dataPath);
            Load();
        }

        public string GetApiKey() => _apiKey;

        public void SetApiKey(string key)
        {
            _apiKey = key;
            Save();
        }

        public ProjectData GetCurrentProject(string filename)
        {
            var key = Path.GetFileName(filename);
            if (!_projects.TryGetValue(key, out var project))
            {
                project = new ProjectData { Name = key, FilePath = filename };
                _projects[key] = project;
            }
            return project;
        }

        public void Save()
        {
            try
            {
                var obj = new JObject
                {
                    ["ApiKey"] = _apiKey,
                    ["Projects"] = JToken.FromObject(_projects)
                };
                File.WriteAllText(
                    Path.Combine(_dataPath, "memory.json"),
                    obj.ToString(Formatting.Indented));
            }
            catch (System.Exception ex) { Logger.Log("Save error: " + ex.Message); }
        }

        private void Load()
        {
            try
            {
                // Carica API key
                var configPath = Path.Combine(_dataPath, "config.json");
                if (File.Exists(configPath))
                {
                    var cfg = JObject.Parse(File.ReadAllText(configPath));
                    _apiKey = cfg["api_key"]?.ToString() ?? "";
                }

                // Carica memoria
                var memPath = Path.Combine(_dataPath, "memory.json");
                if (File.Exists(memPath))
                {
                    var data = JObject.Parse(File.ReadAllText(memPath));
                    if (data["ApiKey"] != null)
                        _apiKey = data["ApiKey"]!.ToString();
                    if (data["Projects"] != null)
                    {
                        var p = JsonConvert.DeserializeObject<Dictionary<string, ProjectData>>(
                            data["Projects"]!.ToString());
                        if (p != null) _projects = p;
                    }
                }
            }
            catch (System.Exception ex) { Logger.Log("Load error: " + ex.Message); }
        }
    }
}

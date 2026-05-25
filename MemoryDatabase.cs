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
        private List<UserRule> _rules = new();

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

        // Aggiunge una regola imparata
        public void LearnRule(string rule, string context = "")
        {
            var existing = _rules.Find(r => r.Rule == rule);
            if (existing != null)
            {
                existing.UsageCount++;
            }
            else
            {
                _rules.Add(new UserRule
                {
                    Rule = rule,
                    Context = context,
                    UsageCount = 1
                });
            }
            Save();
            Logger.Log($"Regola imparata: {rule}");
        }

        public List<UserRule> GetRules() => _rules;

        public string GetRulesForPrompt()
        {
            if (_rules.Count == 0) return "";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("REGOLE PERSONALIZZATE (segui sempre queste):");
            foreach (var r in _rules)
                sb.AppendLine($"  - {r.Rule}");
            return sb.ToString();
        }

        public string GetProjectsSummary()
        {
            if (_projects.Count == 0) return "";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("PROGETTI PRECEDENTI:");
            int count = 0;
            foreach (var kv in _projects)
            {
                if (count++ > 3) break;
                var p = kv.Value;
                if (p.Circuits.Count > 0)
                {
                    sb.AppendLine($"  {p.Name}: {p.Circuits.Count} circuiti");
                    foreach (var c in p.Circuits)
                        sb.AppendLine($"    - {c.Type}: {c.CableSection}mm² int.{c.BreakerSize}A");
                }
            }
            return sb.ToString();
        }

        public void Save()
        {
            try
            {
                var obj = new JObject
                {
                    ["ApiKey"] = _apiKey,
                    ["Projects"] = JToken.FromObject(_projects),
                    ["Rules"] = JToken.FromObject(_rules)
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
                // Cerca API key in tutti i possibili percorsi
                var configPaths = new[]
                {
                    Path.Combine(_dataPath, "config.json"),
                    Path.Combine(Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                        "ImplantiPlugin", "config.json")
                };

                foreach (var cp in configPaths)
                {
                    Logger.Log("Cerco config in: " + cp);
                    if (File.Exists(cp))
                    {
                        var cfg = JObject.Parse(File.ReadAllText(cp));
                        var k = cfg["api_key"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(k))
                        {
                            _apiKey = k;
                            Logger.Log("API Key trovata in: " + cp);
                            break;
                        }
                    }
                }

                // Carica memoria
                var memPath = Path.Combine(_dataPath, "memory.json");
                if (File.Exists(memPath))
                {
                    var data = JObject.Parse(File.ReadAllText(memPath));

                    // API key da memory (solo se non vuota)
                    if (data["ApiKey"] != null)
                    {
                        var k = data["ApiKey"]!.ToString();
                        if (!string.IsNullOrEmpty(k)) _apiKey = k;
                    }

                    // Progetti
                    if (data["Projects"] != null)
                    {
                        var p = JsonConvert.DeserializeObject<Dictionary<string, ProjectData>>(
                            data["Projects"]!.ToString());
                        if (p != null) _projects = p;
                    }

                    // Regole
                    if (data["Rules"] != null)
                    {
                        var r = JsonConvert.DeserializeObject<List<UserRule>>(
                            data["Rules"]!.ToString());
                        if (r != null) _rules = r;
                    }
                }

                Logger.Log($"Memoria caricata. API Key: {(_apiKey.Length > 0 ? "OK" : "MANCANTE")}");
                Logger.Log($"Regole: {_rules.Count}, Progetti: {_projects.Count}");
            }
            catch (System.Exception ex) { Logger.Log("Load error: " + ex.Message); }
        }
    }
}

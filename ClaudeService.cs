using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ImplantiAI
{
    public class ClaudeService
    {
        private const string API_URL = "https://api.anthropic.com/v1/messages";
        private const string MODEL = "claude-sonnet-4-5";
        private readonly string _apiKey;
        private readonly HttpClient _client;

        private const string SYSTEM_BASE = @"Sei un assistente esperto in impianti elettrici integrato in AutoCAD.
Aiuti il progettista a tracciare circuiti secondo CEI 64-8.
Rispondi sempre in italiano.

NORMATIVA CEI 64-8:
- Circuiti luce: cavo 1.5mm², interruttore 10A curva C
- Circuiti prese: cavo 2.5mm², interruttore 16A curva C
- Cucina: circuito dedicato 20A
- Bagno: circuito dedicato con diff. 30mA
- Caduta tensione max 4%

SIMBOLI DISPONIBILI (usa questi nomi esatti):
- luce_soffitto: corpo illuminante a soffitto
- luce_parete: corpo illuminante a parete
- emergenza: lampada di emergenza
- interruttore_1p: interruttore 1P 16A
- interruttore_2p: interruttore bipolare 16A
- pulsante: pulsante 1P NO
- pulsante_doppio: doppio pulsante
- presa_univ: presa universale
- presa_cmd: presa comandata
- presa_tv: presa TV
- presa_sat: presa SAT
- scatola_fem: scatola derivazione FEM
- scatola_luce: scatola derivazione luci
- videocit_int: videocitofono interno
- videocit_est: videocitofono esterno
- suoneria: suoneria
- ventilatore: ventilatore da parete
- riv_gas: rivelatore GAS
- riv_acqua: rivelatore acqua
- cronoterm: cronotermostato

LAYER STANDARD:
- Impianto Elettrico Illuminazione (colore giallo)
- Impianto Elettrico Fem (colore rosso)
- Impianto Elettrico Dati (colore blu)
- Impianto Elettrico Allarme (colore arancio)

QUANDO L'UTENTE CHIEDE DI DISEGNARE rispondo con JSON:
{
  ""text"": ""Risposta testuale all'utente"",
  ""commands"": [
    {""action"":""symbol"", ""symbol_type"":""luce_soffitto"", ""x"":1000, ""y"":2000, ""layer"":""Impianto Elettrico Illuminazione"", ""label"":""PL1""},
    {""action"":""route"", ""x"":1000, ""y"":2000, ""x2"":2000, ""y2"":2000, ""layer"":""Impianto Elettrico Illuminazione"", ""cable_section"":""1.5"", ""label"":""C1""},
    {""action"":""label"", ""x"":1500, ""y"":2100, ""label"":""3x1.5mm²"", ""layer"":""Impianto Elettrico Illuminazione""}
  ],
  ""learn_rule"": ""regola da ricordare per progetti futuri (opzionale)""
}

Se NON devo disegnare, rispondo solo con testo normale senza JSON.
Usa le coordinate reali dei vani dal contesto disegno.";

        public ClaudeService()
        {
            _apiKey = MemoryDatabase.Instance.GetApiKey();
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            _client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            _client.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<ClaudeResponse?> Chat(
            List<ChatMessage> history, string drawingContext)
        {
            // [DEBUG v2.10] log query + context
            Logger.Log("=== CHAT START ===");
            if (history != null && history.Count > 0)
                Logger.Log("USER QUERY: " + (history[history.Count - 1].Content ?? "").Replace("\n", " | "));
            Logger.Log("DRAWING CONTEXT (len=" + (drawingContext?.Length ?? 0) + "):\n" +
                (drawingContext != null && drawingContext.Length > 2000
                    ? drawingContext.Substring(0, 2000) + "...[truncated]" : (drawingContext ?? "")));
            if (string.IsNullOrEmpty(_apiKey))
                throw new System.Exception(
                    "API Key non configurata!\n" +
                    "Configura in: %APPDATA%\\ANGImpianti\\config.json");

            var rules = MemoryDatabase.Instance.GetRulesForPrompt();
            var projects = MemoryDatabase.Instance.GetProjectsSummary();

            var systemPrompt = SYSTEM_BASE;
            if (!string.IsNullOrEmpty(rules))
                systemPrompt += "\n\n" + rules;
            if (!string.IsNullOrEmpty(projects))
                systemPrompt += "\n\n" + projects;
            systemPrompt += "\n\nCONTESTO DISEGNO CORRENTE:\n" + drawingContext;

            var messages = new List<object>();
            foreach (var msg in history)
                messages.Add(new { role = msg.Role, content = msg.Content });

            var body = new
            {
                model = MODEL,
                max_tokens = 2000,
                system = systemPrompt,
                messages
            };

            var content = new StringContent(
                JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");

            try
            {
                var resp = await _client.PostAsync(API_URL, content);
                var respStr = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    Logger.Log("API Error: " + respStr);
                    throw new System.Exception("Errore API: " + resp.StatusCode);
                }

                var json = JObject.Parse(respStr);
                var text = json["content"]?[0]?["text"]?.ToString() ?? "";
                // [DEBUG v2.10] log raw Claude response
                Logger.Log("CLAUDE RAW RESPONSE (len=" + text.Length + "):\n" +
                    (text.Length > 3000 ? text.Substring(0, 3000) + "...[truncated]" : text));
                var parsed = ParseResponse(text);
                Logger.Log("PARSED: text_len=" + (parsed?.Text?.Length ?? 0) +
                    " commands=" + (parsed?.Commands?.Count ?? 0) +
                    " learn_rule=" + (parsed?.LearnRule ?? "(none)"));
                if (parsed?.Commands != null)
                    foreach (var c in parsed.Commands)
                        Logger.Log("  CMD: action=" + c.Action + " type=" + c.SymbolType +
                            " x=" + c.X + " y=" + c.Y + " x2=" + c.X2 + " y2=" + c.Y2 +
                            " label=" + c.Label + " layer=" + c.Layer);
                Logger.Log("=== CHAT END ===\n");
                return parsed;
            }
            catch (TaskCanceledException)
            {
                throw new System.Exception("Timeout. Riprova.");
            }
        }

        private ClaudeResponse ParseResponse(string text)
        {
            // Prova a parsare come JSON con comandi
            try
            {
                var clean = CleanJson(text);
                if (clean.StartsWith("{"))
                {
                    var json = JObject.Parse(clean);
                    var response = new ClaudeResponse
                    {
                        Text = json["text"]?.ToString() ?? text,
                        LearnRule = json["learn_rule"]?.ToString()
                    };

                    var cmds = json["commands"] as JArray;
                    if (cmds != null && cmds.Count > 0)
                    {
                        response.Commands = new List<DrawCommand>();
                        foreach (var cmd in cmds)
                        {
                            response.Commands.Add(new DrawCommand
                            {
                                Action = cmd["action"]?.ToString() ?? "",
                                SymbolType = cmd["symbol_type"]?.ToString() ?? "",
                                Layer = cmd["layer"]?.ToString() ?? "Impianto Elettrico",
                                X = cmd["x"]?.Value<double>() ?? 0,
                                Y = cmd["y"]?.Value<double>() ?? 0,
                                X2 = cmd["x2"]?.Value<double>() ?? 0,
                                Y2 = cmd["y2"]?.Value<double>() ?? 0,
                                Label = cmd["label"]?.ToString() ?? "",
                                CableSection = cmd["cable_section"]?.ToString() ?? "",
                                RoomName = cmd["room_name"]?.ToString() ?? ""
                            });
                        }
                    }
                    return response;
                }
            }
            catch { }

            return new ClaudeResponse { Text = text };
        }

        private string CleanJson(string text)
        {
            var cleaned = Regex.Replace(text.Trim(), @"^```(?:json)?\s*", "",
                RegexOptions.Multiline);
            cleaned = Regex.Replace(cleaned, @"\s*```$", "", RegexOptions.Multiline);
            int start = cleaned.IndexOf('{');
            if (start > 0) cleaned = cleaned.Substring(start);
            return cleaned.Trim();
        }
    }
}

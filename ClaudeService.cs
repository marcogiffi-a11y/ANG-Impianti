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

        private const string SYSTEM = @"Sei un assistente esperto in impianti elettrici integrato in AutoCAD.
Aiuti il progettista a tracciare circuiti, calcolare cavi e interruttori secondo CEI 64-8.
Rispondi in italiano, in modo chiaro e conciso.
Normativa: Circuiti luce 1.5mm² int.10A, Prese 2.5mm² int.16A, Cucina/bagno circuiti dedicati.";

        public ClaudeService()
        {
            _apiKey = MemoryDatabase.Instance.GetApiKey();
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            _client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            _client.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<ClaudeResponse?> Chat(
            List<ChatMessage> history, string context)
        {
            if (string.IsNullOrEmpty(_apiKey))
                throw new Exception("API Key non configurata!\nConfigura in: %APPDATA%\\ANGImpianti\\config.json");

            var messages = new List<object>();
            foreach (var msg in history)
                messages.Add(new { role = msg.Role, content = msg.Content });

            var body = new
            {
                model = MODEL,
                max_tokens = 1000,
                system = SYSTEM + $"\n\nDISEGNO CORRENTE:\n{context}",
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
                    Logger.Log($"API Error: {respStr}");
                    throw new Exception($"Errore API: {resp.StatusCode}");
                }

                var json = JObject.Parse(respStr);
                var text = json["content"]?[0]?["text"]?.ToString() ?? "";
                return new ClaudeResponse { Text = text };
            }
            catch (TaskCanceledException)
            {
                throw new Exception("Timeout. Riprova.");
            }
        }
    }
}

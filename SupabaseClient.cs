using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ImplantiAI
{
    /// <summary>
    /// Client REST diretto verso Supabase (no SDK ufficiali per .NET maturi).
    /// Usa le credenziali del progetto ANG-Gest principale (lo stesso DB di tutto il sistema).
    /// </summary>
    public static class SupabaseClient
    {
        private const string SUPABASE_URL = "https://fezkgexyvbduuurodggz.supabase.co";
        // Anon key pubblica (RLS attiva sulle tabelle)
        private const string SUPABASE_KEY = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImZlemtnZXh5dmJkdXV1cm9kZ2d6Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3MjMyMjMxNzgsImV4cCI6MjAzODc5OTE3OH0.HnGEUbDPjqJqXG0eXqxAlT3xNlD-tn-WUYn4WJk7yhU";

        private static readonly HttpClient _http = CreateClient();
        private static HttpClient CreateClient()
        {
            var c = new HttpClient();
            c.DefaultRequestHeaders.Add("apikey", SUPABASE_KEY);
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SUPABASE_KEY);
            c.Timeout = TimeSpan.FromSeconds(20);
            return c;
        }

        // Genera l'URL completo REST
        private static string Url(string path) => $"{SUPABASE_URL}/rest/v1/{path}";

        /// <summary>GET su una tabella con filtri PostgREST. Es: Select("mary_simboli", "categoria=eq.presa")</summary>
        public static async Task<JArray> Select(string table, string query = "")
        {
            var req = new HttpRequestMessage(HttpMethod.Get, Url(table) + (string.IsNullOrEmpty(query) ? "?select=*" : "?" + query));
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var resp = await _http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) throw new System.Exception($"Supabase Select {table}: {resp.StatusCode} {body}");
            return JArray.Parse(body);
        }

        /// <summary>INSERT di un oggetto JSON. Ritorna l'oggetto inserito (con id assegnato).</summary>
        public static async Task<JObject> Insert(string table, JObject row)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, Url(table));
            req.Headers.Add("Prefer", "return=representation");
            req.Content = new StringContent("[" + row.ToString() + "]", Encoding.UTF8, "application/json");
            var resp = await _http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) throw new System.Exception($"Supabase Insert {table}: {resp.StatusCode} {body}");
            var arr = JArray.Parse(body);
            return arr.Count > 0 ? (JObject)arr[0] : new JObject();
        }

        /// <summary>UPDATE filtrato. Es: Update("mary_simboli", "id=eq.xxx", patch)</summary>
        public static async Task<bool> Update(string table, string filter, JObject patch)
        {
            var req = new HttpRequestMessage(new HttpMethod("PATCH"), Url(table) + "?" + filter);
            req.Content = new StringContent(patch.ToString(), Encoding.UTF8, "application/json");
            var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }

        /// <summary>DELETE filtrato.</summary>
        public static async Task<bool> Delete(string table, string filter)
        {
            var req = new HttpRequestMessage(HttpMethod.Delete, Url(table) + "?" + filter);
            var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
    }
}

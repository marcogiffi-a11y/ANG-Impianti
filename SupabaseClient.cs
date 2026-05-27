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
    /// Le credenziali si caricano da %APPDATA%\ANGImpianti\config.json
    /// (stesso file usato per la API key Claude). Fallback hardcoded per
    /// retrocompatibilità: se mancano i campi nel JSON, usa i valori sotto.
    ///
    /// Schema atteso in config.json:
    ///   { "api_key": "sk-...", "supabase_url": "https://...", "supabase_anon_key": "eyJ..." }
    /// </summary>
    public static class SupabaseClient
    {
        // Fallback hardcoded (in caso config.json non contenga le chiavi).
        // Sostituisci se ruoti la chiave e non vuoi aggiornare il config.json.
        private const string FALLBACK_URL = "https://fezkgexyvbduuurodggz.supabase.co";
        private const string FALLBACK_KEY = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImZlemtnZXh5dmJkdXV1cm9kZ2d6Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3MjMyMjMxNzgsImV4cCI6MjAzODc5OTE3OH0.HnGEUbDPjqJqXG0eXqxAlT3xNlD-tn-WUYn4WJk7yhU";

        private static readonly string SUPABASE_URL;
        private static readonly string SUPABASE_KEY;

        static SupabaseClient()
        {
            var (url, key) = LoadCredentials();
            SUPABASE_URL = url;
            SUPABASE_KEY = key;
        }

        private static (string url, string key) LoadCredentials()
        {
            try
            {
                var configPath = System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                    "ANGImpianti", "config.json");

                if (System.IO.File.Exists(configPath))
                {
                    var cfg = JObject.Parse(System.IO.File.ReadAllText(configPath));
                    var url = cfg["supabase_url"]?.ToString();
                    var key = cfg["supabase_anon_key"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(key))
                    {
                        Logger.Log($"SupabaseClient: credenziali caricate da {configPath}");
                        return (url, key);
                    }
                    Logger.Log("SupabaseClient: config.json esiste ma campi supabase_url/supabase_anon_key mancanti, uso fallback");
                }
                else
                {
                    Logger.Log($"SupabaseClient: config.json non trovato in {configPath}, uso fallback");
                }
            }
            catch (System.Exception ex) { Logger.Log("SupabaseClient.LoadCredentials: " + ex.Message); }
            return (FALLBACK_URL, FALLBACK_KEY);
        }

        private static HttpClient? _http;
        private static HttpClient Http => _http ??= CreateClient();
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
            var resp = await Http.SendAsync(req);
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
            var resp = await Http.SendAsync(req);
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
            var resp = await Http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }

        /// <summary>DELETE filtrato.</summary>
        public static async Task<bool> Delete(string table, string filter)
        {
            var req = new HttpRequestMessage(HttpMethod.Delete, Url(table) + "?" + filter);
            var resp = await Http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }

        // ============================================================
        //  STORAGE API (bucket public 'symbol-previews')
        // ============================================================

        /// <summary>
        /// Carica un PNG su Supabase Storage e ritorna l'URL pubblico.
        /// Path tipico: '{categoria}/{nome}-{uuid}.png'. Se path esiste già,
        /// usa l'header x-upsert per sovrascrivere.
        /// </summary>
        public static async Task<string?> UploadImage(byte[] png, string bucket, string path)
        {
            try
            {
                var url = $"{SUPABASE_URL}/storage/v1/object/{bucket}/{Uri.EscapeUriString(path)}";
                var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Headers.Add("x-upsert", "true");
                req.Content = new ByteArrayContent(png);
                req.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
                var resp = await Http.SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    Logger.Log($"UploadImage failed: {resp.StatusCode} {body}");
                    return null;
                }
                var publicUrl = $"{SUPABASE_URL}/storage/v1/object/public/{bucket}/{Uri.EscapeUriString(path)}";
                Logger.Log("UploadImage: " + publicUrl);
                return publicUrl;
            }
            catch (System.Exception ex) { Logger.Log("UploadImage: " + ex.Message); return null; }
        }

        /// <summary>Scarica bytes da un URL pubblico (no auth, Storage public bucket).</summary>
        public static async Task<byte[]?> DownloadBytes(string url)
        {
            try
            {
                using var c = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                return await c.GetByteArrayAsync(url);
            }
            catch (System.Exception ex) { Logger.Log("DownloadBytes " + url + ": " + ex.Message); return null; }
        }

        /// <summary>Cancella un file da Supabase Storage. Path: '{categoria}/{nome}-{uuid}.png'.</summary>
        public static async Task<bool> DeleteImage(string bucket, string path)
        {
            try
            {
                var url = $"{SUPABASE_URL}/storage/v1/object/{bucket}/{Uri.EscapeUriString(path)}";
                var req = new HttpRequestMessage(HttpMethod.Delete, url);
                var resp = await Http.SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    Logger.Log($"DeleteImage failed: {resp.StatusCode} {body}");
                    return false;
                }
                return true;
            }
            catch (System.Exception ex) { Logger.Log("DeleteImage: " + ex.Message); return false; }
        }

        /// <summary>Estrae il path '{bucket}/{...}' da un URL pubblico Storage.</summary>
        public static string? ExtractStoragePath(string publicUrl, string bucket)
        {
            try
            {
                var marker = $"/storage/v1/object/public/{bucket}/";
                var idx = publicUrl.IndexOf(marker);
                if (idx < 0) return null;
                return Uri.UnescapeDataString(publicUrl.Substring(idx + marker.Length));
            }
            catch { return null; }
        }
    }
}

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

[assembly: CommandClass(typeof(ImplantiAI.Commands))]
[assembly: CommandClass(typeof(ImplantiAI.PluginApp))]
[assembly: ExtensionApplication(typeof(ImplantiAI.PluginApp))]

namespace ImplantiAI
{
    public class PluginApp : IExtensionApplication
    {
        public static PaletteSet? Palette { get; private set; }
        public static ChatPanel? Chat { get; private set; }

        // Endpoint su Vercel - non rivela il repository privato
        private const string UPDATE_URL = "https://ang-gest.vercel.app/api/ang-impianti-version";
        private const string CURRENT_VERSION = "2.4";
        private const string BUNDLE_PATH = @"C:\ProgramData\Autodesk\ApplicationPlugins\ANGImpianti.bundle";
        private static bool _updateChecked = false;

        public void Initialize()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application
                .DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            try
            {
                MemoryDatabase.Instance.Initialize();

                Palette = new PaletteSet("ANG-Impianti AI",
                    new Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890"));
                Chat = new ChatPanel();
                var host = new ElementHost { Child = Chat, Dock = DockStyle.Fill };
                Palette.Add("Chat AI", host);
                Palette.Style = PaletteSetStyles.ShowCloseButton |
                                PaletteSetStyles.ShowAutoHideButton |
                                PaletteSetStyles.Snappable;
                Palette.MinimumSize = new Size(300, 400);
                Palette.Size = new Size(350, 600);
                Palette.DockEnabled = DockSides.Left | DockSides.Right;
                Palette.Dock = DockSides.Right;
                Palette.Visible = true;

                Autodesk.AutoCAD.ApplicationServices.Application.Idle += OnIdle;

                doc.Editor.WriteMessage(
                    "\n╔══════════════════════════╗\n" +
                    "║  ANG-Impianti AI v2.3    ║\n" +
                    "╚══════════════════════════╝\n");
            }
            catch (System.Exception ex)
            {
                doc?.Editor.WriteMessage("\n✗ Errore: " + ex.Message + "\n");
            }
        }

        private void OnIdle(object? sender, EventArgs e)
        {
            Autodesk.AutoCAD.ApplicationServices.Application.Idle -= OnIdle;
            try { RibbonManager.CreateRibbon(); }
            catch (System.Exception ex) { Logger.Log("Ribbon: " + ex.Message); }

            if (!_updateChecked)
            {
                _updateChecked = true;
                Task.Run(() => CheckForUpdates());
            }
        }

        [CommandMethod("CHECK_UPDATE")]
        public void CheckUpdateCommand()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application
                .DocumentManager.MdiActiveDocument;
            doc?.Editor.WriteMessage("\nANG: controllo aggiornamenti...\n");
            Task.Run(() => CheckForUpdates());
        }

        private void CheckForUpdates()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "ANG-Impianti/" + CURRENT_VERSION);
                client.Timeout = TimeSpan.FromSeconds(15);

                var json = client.GetStringAsync(UPDATE_URL).GetAwaiter().GetResult();

                // Parse version and url from JSON
                var verMatch = Regex.Match(json, "\"version\":\\s*\"([^\"]+)\"");
                var urlMatch = Regex.Match(json, "\"url\":\\s*\"([^\"]+)\"");

                if (!verMatch.Success) return;

                var latest = verMatch.Groups[1].Value.Trim();
                var downloadUrl = urlMatch.Success ? urlMatch.Groups[1].Value : "";

                var doc = Autodesk.AutoCAD.ApplicationServices.Application
                    .DocumentManager.MdiActiveDocument;
                doc?.Editor.WriteMessage(
                    $"\nANG: v{CURRENT_VERSION} installata, v{latest} disponibile.\n");

                if (latest == CURRENT_VERSION)
                {
                    doc?.Editor.WriteMessage("ANG: sei all'ultima versione ✓\n");
                    return;
                }

                var result = MessageBox.Show(
                    $"ANG-Impianti: aggiornamento disponibile!\n\n" +
                    $"Versione installata:   v{CURRENT_VERSION}\n" +
                    $"Versione disponibile: v{latest}\n\n" +
                    "Aggiornare adesso?\n(AutoCAD si riavvierà automaticamente)",
                    "Aggiornamento ANG-Impianti",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes && !string.IsNullOrEmpty(downloadUrl))
                    Task.Run(() => InstallUpdate(downloadUrl));
            }
            catch (System.Exception ex)
            {
                Logger.Log("UpdateCheck: " + ex.Message);
                var doc = Autodesk.AutoCAD.ApplicationServices.Application
                    .DocumentManager.MdiActiveDocument;
                doc?.Editor.WriteMessage($"\nANG: errore controllo update: {ex.Message}\n");
            }
        }

        private void InstallUpdate(string downloadUrl)
        {
            try
            {
                // Download su Desktop
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var tempZip = Path.Combine(desktop, "ANGImpianti_update.zip");

                MessageBox.Show("Download in corso...",
                    "Aggiornamento", MessageBoxButtons.OK, MessageBoxIcon.Information);

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "ANG-Impianti-Updater");
                client.Timeout = TimeSpan.FromMinutes(5);
                var bytes = client.GetByteArrayAsync(downloadUrl).GetAwaiter().GetResult();
                File.WriteAllBytes(tempZip, bytes);

                // Apri Esplora File su ApplicationPlugins
                System.Diagnostics.Process.Start("explorer.exe",
                    @"C:\ProgramData\Autodesk\ApplicationPlugins");

                // Apri la cartella del Desktop con lo zip
                System.Diagnostics.Process.Start("explorer.exe",
                    $"/select,\"{tempZip}\"");

                MessageBox.Show(
                    "Scaricato! Ora:

" +
                    "1. Chiudi AutoCAD
" +
                    "2. Apri ANGImpianti_update.zip dal Desktop
" +
                    "3. Copia ANGImpianti.bundle nella finestra ApplicationPlugins
" +
                    "4. Riapri AutoCAD

" +
                    "Le due cartelle sono già aperte!",
                    "Installazione",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (System.Exception ex)
            {
                Logger.Log("Install: " + ex.Message);
                MessageBox.Show("Errore: " + ex.Message, "Errore",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void Terminate() => MemoryDatabase.Instance.Save();
    }
}

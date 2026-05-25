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
[assembly: ExtensionApplication(typeof(ImplantiAI.PluginApp))]

namespace ImplantiAI
{
    public class PluginApp : IExtensionApplication
    {
        public static PaletteSet? Palette { get; private set; }
        public static ChatPanel? Chat { get; private set; }

        private const string GITHUB_REPO = "marcogiffi-a11y/ANG-Impianti";
        private const string CURRENT_VERSION = "2.1";
        private const string BUNDLE_PATH = @"C:\ProgramData\Autodesk\ApplicationPlugins\ANGImpianti.bundle";

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
                    "║  ANG-Impianti AI v2.1    ║\n" +
                    "║  Simboli F-05 + Auto-Upd ║\n" +
                    "╚══════════════════════════╝\n");

                // Check for updates in background
                Task.Run(async () =>
                {
                    try { await CheckForUpdatesAsync(doc); }
                    catch (Exception ex) { Logger.Log("Updater: " + ex.Message); }
                });
            }
            catch (Exception ex)
            {
                doc?.Editor.WriteMessage("\n✗ Errore avvio: " + ex.Message + "\n");
            }
        }

        private async Task CheckForUpdatesAsync(
            Autodesk.AutoCAD.ApplicationServices.Document doc)
        {
            try
            {
                doc?.Editor.WriteMessage("\nANG: controllo aggiornamenti...\n");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "ANG-Impianti-Updater");
                client.Timeout = TimeSpan.FromSeconds(15);

                var json = await client.GetStringAsync(
                    $"https://api.github.com/repos/{GITHUB_REPO}/releases/latest");

                var tagMatch = Regex.Match(json, "\"tag_name\":\\s*\"v?([^\"]+)\"");
                if (!tagMatch.Success)
                {
                    doc?.Editor.WriteMessage("\nANG: impossibile leggere versione GitHub.\n");
                    return;
                }

                var latest = tagMatch.Groups[1].Value.Trim();
                doc?.Editor.WriteMessage($"\nANG: v{CURRENT_VERSION} installata, v{latest} su GitHub.\n");

                if (latest == CURRENT_VERSION) return;

                var urlMatch = Regex.Match(json,
                    "\"browser_download_url\":\\s*\"([^\"]+\\.zip)\"");
                if (!urlMatch.Success) return;
                var downloadUrl = urlMatch.Groups[1].Value;

                // MessageBox.Show è thread-safe in WinForms
                var result = MessageBox.Show(
                    $"Nuova versione ANG-Impianti disponibile!\n\n" +
                    $"Installata: v{CURRENT_VERSION}\n" +
                    $"Disponibile: v{latest}\n\n" +
                    "Aggiornare adesso?",
                    "ANG-Impianti - Aggiornamento",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                    await DownloadAndInstallAsync(downloadUrl);
            }
            catch (Exception ex)
            {
                doc?.Editor.WriteMessage($"\nANG: errore controllo update: {ex.Message}\n");
                Logger.Log("UpdateCheck: " + ex.Message);
            }
        }

        private async Task DownloadAndInstallAsync(string downloadUrl)
        {
            try
            {
                var tempZip = Path.Combine(Path.GetTempPath(), "ANGImpianti_update.zip");

                MessageBox.Show(
                    "Download in corso...\nAutoCAD si chiuderà al termine.",
                    "Aggiornamento", MessageBoxButtons.OK, MessageBoxIcon.Information);

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "ANG-Impianti-Updater");
                var bytes = await client.GetByteArrayAsync(downloadUrl);
                File.WriteAllBytes(tempZip, bytes);

                var scriptPath = Path.Combine(Path.GetTempPath(), "ang_update.bat");
                File.WriteAllText(scriptPath,
                    "@echo off\r\n" +
                    "timeout /t 3 /nobreak >nul\r\n" +
                    "if exist \"" + BUNDLE_PATH + "\" rmdir /s /q \"" + BUNDLE_PATH + "\"\r\n" +
                    "powershell -Command \"Expand-Archive -Path '" + tempZip +
                    "' -DestinationPath 'C:\\ProgramData\\Autodesk\\ApplicationPlugins\\' -Force\"\r\n" +
                    "del \"" + tempZip + "\"\r\n" +
                    "start acad.exe\r\n");

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = scriptPath,
                    UseShellExecute = true,
                    Verb = "runas"
                });

                Autodesk.AutoCAD.ApplicationServices.Application.Quit();
            }
            catch (Exception ex)
            {
                Logger.Log("Install: " + ex.Message);
                MessageBox.Show("Errore: " + ex.Message, "Errore aggiornamento",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnIdle(object? sender, EventArgs e)
        {
            Autodesk.AutoCAD.ApplicationServices.Application.Idle -= OnIdle;
            try { RibbonManager.CreateRibbon(); }
            catch (Exception ex) { Logger.Log("Ribbon: " + ex.Message); }
        }

        public void Terminate() => MemoryDatabase.Instance.Save();
    }
}

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
        private const string CURRENT_VERSION = "2.0";
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
                var host = new ElementHost
                {
                    Child = Chat,
                    Dock = DockStyle.Fill
                };
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
                    "║  ANG-Impianti AI v2.0    ║\n" +
                    "║  Ribbon e Chat pronti!   ║\n" +
                    "╚══════════════════════════╝\n");

                Task.Run(() => CheckForUpdatesAsync());
            }
            catch (System.Exception ex)
            {
                doc?.Editor.WriteMessage("\n✗ Errore avvio: " + ex.Message + "\n");
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "ANG-Impianti-AutoUpdater");
                client.Timeout = TimeSpan.FromSeconds(10);

                var json = await client.GetStringAsync(
                    "https://api.github.com/repos/" + GITHUB_REPO + "/releases/latest");

                var tagMatch = Regex.Match(json, "\"tag_name\":\\s*\"([^\"]+)\"");
                if (!tagMatch.Success) return;

                var latestTag = tagMatch.Groups[1].Value.TrimStart('v');
                if (latestTag == CURRENT_VERSION) return;

                var urlMatch = Regex.Match(json, "\"browser_download_url\":\\s*\"([^\"]+\\.zip)\"");
                if (!urlMatch.Success) return;
                var downloadUrl = urlMatch.Groups[1].Value;

                Autodesk.AutoCAD.ApplicationServices.Application.Invoke((Action)(() =>
                {
                    var result = MessageBox.Show(
                        "Nuova versione ANG-Impianti disponibile!\n\n" +
                        "Versione attuale: v" + CURRENT_VERSION + "\n" +
                        "Nuova versione: v" + latestTag + "\n\n" +
                        "Aggiornare adesso?",
                        "ANG-Impianti - Aggiornamento",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (result == DialogResult.Yes)
                        Task.Run(() => DownloadAndInstallAsync(downloadUrl));
                }));
            }
            catch (System.Exception ex)
            {
                Logger.Log("Update check: " + ex.Message);
            }
        }

        private async Task DownloadAndInstallAsync(string downloadUrl)
        {
            try
            {
                var tempZip = Path.Combine(Path.GetTempPath(), "ANGImpianti_update.zip");

                Autodesk.AutoCAD.ApplicationServices.Application.Invoke((Action)(() =>
                    MessageBox.Show("Download in corso...\nAutoCAD si chiuderà al termine.",
                        "Aggiornamento", MessageBoxButtons.OK, MessageBoxIcon.Information)));

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "ANG-Impianti-AutoUpdater");
                var bytes = await client.GetByteArrayAsync(downloadUrl);
                File.WriteAllBytes(tempZip, bytes);

                var scriptPath = Path.Combine(Path.GetTempPath(), "ang_update.bat");
                File.WriteAllText(scriptPath,
                    "@echo off\r\n" +
                    "timeout /t 3 /nobreak >nul\r\n" +
                    "if exist \"" + BUNDLE_PATH + "\" rmdir /s /q \"" + BUNDLE_PATH + "\"\r\n" +
                    "powershell -Command \"Expand-Archive -Path '" + tempZip + "' -DestinationPath 'C:\\ProgramData\\Autodesk\\ApplicationPlugins\\' -Force\"\r\n" +
                    "del \"" + tempZip + "\"\r\n" +
                    "start acad.exe\r\n");

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = scriptPath,
                    UseShellExecute = true,
                    Verb = "runas"
                });

                Autodesk.AutoCAD.ApplicationServices.Application.Invoke((Action)(() =>
                    Autodesk.AutoCAD.ApplicationServices.Application.Quit()));
            }
            catch (System.Exception ex)
            {
                Logger.Log("Update install: " + ex.Message);
                Autodesk.AutoCAD.ApplicationServices.Application.Invoke((Action)(() =>
                    MessageBox.Show("Errore: " + ex.Message, "Errore aggiornamento",
                        MessageBoxButtons.OK, MessageBoxIcon.Error)));
            }
        }

        private void OnIdle(object? sender, EventArgs e)
        {
            Autodesk.AutoCAD.ApplicationServices.Application.Idle -= OnIdle;
            try { RibbonManager.CreateRibbon(); }
            catch (System.Exception ex) { Logger.Log("Ribbon error: " + ex.Message); }
        }

        public void Terminate() => MemoryDatabase.Instance.Save();
    }
}

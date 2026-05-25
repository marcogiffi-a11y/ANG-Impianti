using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
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
                    "\n╔══════════════════════════════╗\n" +
                    "║  ANG-Impianti AI v2.1        ║\n" +
                    "║  Controllo aggiornamenti...  ║\n" +
                    "╚══════════════════════════════╝\n");

                // Avvia updater dopo 3 secondi
                var timer = new System.Windows.Forms.Timer();
                timer.Interval = 3000;
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    Task.Run(() => CheckForUpdates());
                };
                timer.Start();
            }
            catch (System.Exception ex)
            {
                doc?.Editor.WriteMessage("\n✗ Errore: " + ex.Message + "\n");
            }
        }

        private void CheckForUpdates()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "ANG-Impianti");
                client.Timeout = TimeSpan.FromSeconds(10);

                var json = client.GetStringAsync(
                    $"https://api.github.com/repos/{GITHUB_REPO}/releases/latest")
                    .GetAwaiter().GetResult();

                var tagMatch = Regex.Match(json, "\"tag_name\":\\s*\"v?([^\"]+)\"");
                if (!tagMatch.Success) return;

                var latest = tagMatch.Groups[1].Value.Trim();

                // Scrivi nella command line
                var doc = Autodesk.AutoCAD.ApplicationServices.Application
                    .DocumentManager.MdiActiveDocument;
                doc?.Editor.WriteMessage(
                    $"\nANG-Impianti: v{CURRENT_VERSION} installata, v{latest} disponibile.\n");

                if (latest == CURRENT_VERSION)
                {
                    doc?.Editor.WriteMessage("ANG-Impianti: già all'ultima versione ✓\n");
                    return;
                }

                var urlMatch = Regex.Match(json,
                    "\"browser_download_url\":\\s*\"([^\"]+\\.zip)\"");
                if (!urlMatch.Success) return;
                var downloadUrl = urlMatch.Groups[1].Value;

                // Popup aggiornamento
                var result = MessageBox.Show(
                    $"ANG-Impianti: nuova versione disponibile!\n\n" +
                    $"Installata:   v{CURRENT_VERSION}\n" +
                    $"Disponibile: v{latest}\n\n" +
                    "Aggiornare adesso?\n(AutoCAD si riavvierà)",
                    "Aggiornamento disponibile",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                    Task.Run(() => InstallUpdate(downloadUrl));
            }
            catch (System.Exception ex)
            {
                Logger.Log("UpdateCheck error: " + ex.Message);
            }
        }

        private void InstallUpdate(string downloadUrl)
        {
            try
            {
                var tempZip = Path.Combine(Path.GetTempPath(), "ANGImpianti_update.zip");

                MessageBox.Show("Download in corso...",
                    "Aggiornamento", MessageBoxButtons.OK, MessageBoxIcon.Information);

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "ANG-Impianti");
                var bytes = client.GetByteArrayAsync(downloadUrl).GetAwaiter().GetResult();
                File.WriteAllBytes(tempZip, bytes);

                var scriptPath = Path.Combine(Path.GetTempPath(), "ang_update.bat");
                File.WriteAllText(scriptPath,
                    "@echo off\r\n" +
                    "timeout /t 3 /nobreak >nul\r\n" +
                    "if exist \"" + BUNDLE_PATH + "\" rmdir /s /q \"" + BUNDLE_PATH + "\"\r\n" +
                    "powershell -Command \"Add-Type -Assembly System.IO.Compression.FileSystem; " +
                    "[IO.Compression.ZipFile]::ExtractToDirectory('" + tempZip +
                    "', 'C:\\ProgramData\\Autodesk\\ApplicationPlugins\\')\"\r\n" +
                    "del \"" + tempZip + "\"\r\n" +
                    "start \"\" \"acad.exe\"\r\n");

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = scriptPath,
                    UseShellExecute = true,
                    Verb = "runas"
                });

                Autodesk.AutoCAD.ApplicationServices.Application.Quit();
            }
            catch (System.Exception ex)
            {
                Logger.Log("Install error: " + ex.Message);
                MessageBox.Show("Errore: " + ex.Message, "Errore",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnIdle(object? sender, EventArgs e)
        {
            Autodesk.AutoCAD.ApplicationServices.Application.Idle -= OnIdle;
            try { RibbonManager.CreateRibbon(); }
            catch (System.Exception ex) { Logger.Log("Ribbon: " + ex.Message); }
        }

        public void Terminate() => MemoryDatabase.Instance.Save();
    }
}

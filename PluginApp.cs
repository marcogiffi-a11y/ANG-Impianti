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
                var tempZip = Path.Combine(Path.GetTempPath(), "ANGImpianti_update.zip");
                var bundleDest = @"C:\ProgramData\Autodesk\ApplicationPlugins\ANGImpianti.bundle";

                MessageBox.Show(
                    "Download in corso...\nAl termine ti verrà chiesto di riavviare AutoCAD.",
                    "Aggiornamento", MessageBoxButtons.OK, MessageBoxIcon.Information);

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "ANG-Impianti-Updater");
                client.Timeout = TimeSpan.FromMinutes(5);
                var bytes = client.GetByteArrayAsync(downloadUrl).GetAwaiter().GetResult();
                File.WriteAllBytes(tempZip, bytes);

                // Semplice script: chiudi AutoCAD, installa, mostra messaggio
                var scriptPath = Path.Combine(Path.GetTempPath(), "ang_update.bat");
                var script = new System.Text.StringBuilder();
                script.AppendLine("@echo off");
                script.AppendLine("echo ANG-Impianti: installazione aggiornamento...");
                script.AppendLine("timeout /t 5 /nobreak >nul");
                script.AppendLine("taskkill /f /im acad.exe 2>nul");
                script.AppendLine("timeout /t 3 /nobreak >nul");
                script.AppendLine("if exist \"" + bundleDest + "\" rmdir /s /q \"" + bundleDest + "\"");
                script.AppendLine("if exist \"C:\\ProgramData\\Autodesk\\ApplicationPlugins\\ANGImpianti\" rmdir /s /q \"C:\\ProgramData\\Autodesk\\ApplicationPlugins\\ANGImpianti\"");
                script.AppendLine("powershell -Command \"$zip='" + tempZip + "'; $dest='C:\\ProgramData\\Autodesk\\ApplicationPlugins'; Add-Type -Assembly System.IO.Compression.FileSystem; [IO.Compression.ZipFile]::ExtractToDirectory($zip, $dest)\"");
                script.AppendLine("if exist \"C:\\ProgramData\\Autodesk\\ApplicationPlugins\\ANGImpianti\" ren \"C:\\ProgramData\\Autodesk\\ApplicationPlugins\\ANGImpianti\" ANGImpianti.bundle");
                script.AppendLine("del \"" + tempZip + "\" 2>nul");
                script.AppendLine("echo.");
                script.AppendLine("echo Aggiornamento completato!");
                script.AppendLine("echo Ora puoi riaprire AutoCAD.");
                script.AppendLine("pause");

                File.WriteAllText(scriptPath, script.ToString());

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
                Logger.Log("Install: " + ex.Message);
                MessageBox.Show("Errore: " + ex.Message, "Errore",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void Terminate() => MemoryDatabase.Instance.Save();
    }
}

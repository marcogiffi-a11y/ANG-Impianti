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

        private const string UPDATE_URL = "https://ang-gest.vercel.app/api/ang-impianti-version";
        private const string CURRENT_VERSION = "2.3";
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
                doc.Editor.WriteMessage("\nANG-Impianti AI v" + CURRENT_VERSION + " pronto.\n");
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
                var verMatch = Regex.Match(json, "\"version\":\\s*\"([^\"]+)\"");
                var urlMatch = Regex.Match(json, "\"url\":\\s*\"([^\"]+)\"");
                if (!verMatch.Success) return;
                var latest = verMatch.Groups[1].Value.Trim();
                var downloadUrl = urlMatch.Success ? urlMatch.Groups[1].Value : "";
                if (latest == CURRENT_VERSION) return;

                var result = MessageBox.Show(
                    "ANG-Impianti: aggiornamento disponibile!\n\nVersione installata:   v" + CURRENT_VERSION + "\nVersione disponibile: v" + latest + "\n\nAggiornare adesso?\n(AutoCAD si riavvierà automaticamente)",
                    "Aggiornamento ANG-Impianti",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes && !string.IsNullOrEmpty(downloadUrl))
                    Task.Run(() => InstallUpdate(downloadUrl));
            }
            catch (System.Exception ex)
            {
                Logger.Log("UpdateCheck: " + ex.Message);
            }
        }

        private void InstallUpdate(string downloadUrl)
        {
            try
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var zipPath = Path.Combine(desktop, "ANGImpianti_update.zip");

                MessageBox.Show("Download in corso...", "Aggiornamento",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "ANG-Impianti-Updater");
                client.Timeout = TimeSpan.FromMinutes(5);
                var bytes = client.GetByteArrayAsync(downloadUrl).GetAwaiter().GetResult();
                File.WriteAllBytes(zipPath, bytes);

                // Apri ApplicationPlugins e seleziona lo zip sul Desktop
                System.Diagnostics.Process.Start("explorer.exe",
                    @"C:\ProgramData\Autodesk\ApplicationPlugins");
                System.Diagnostics.Process.Start("explorer.exe",
                    "/select,\"" + zipPath + "\"");

                MessageBox.Show(
                    "Download completato!\n\n" +
                    "1. Chiudi AutoCAD\n" +
                    "2. Apri ANGImpianti_update.zip dal Desktop\n" +
                    "3. Copia ANGImpianti.bundle in ApplicationPlugins\n" +
                    "4. Riapri AutoCAD\n\n" +
                    "Le due cartelle sono gia aperte!",
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

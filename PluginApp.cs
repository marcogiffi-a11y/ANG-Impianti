﻿﻿﻿﻿using Autodesk.AutoCAD.Runtime;
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
        public  const string CURRENT_VERSION = "2.11";
        private static bool _updateChecked = false;
        private static string _acadExePath = "";

        public void Initialize()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application
                .DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            try
            {
                // Memorizza il path dell'AutoCAD corrente per il restart dopo update
                try {
                    _acadExePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                } catch { _acadExePath = ""; }
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
                doc?.Editor.WriteMessage("\nErrore: " + ex.Message + "\n");
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
                    "ANG-Impianti: aggiornamento disponibile!\n\nVersione installata: v" +
                    CURRENT_VERSION + "\nVersione disponibile: v" + latest +
                    "\n\nAggiornare adesso?\n(AutoCAD si chiudera e riaprira automaticamente)",
                    "Aggiornamento ANG-Impianti",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes && !string.IsNullOrEmpty(downloadUrl))
                    Task.Run(() => InstallUpdate(downloadUrl));
            }
            catch (System.Exception ex) { Logger.Log("UpdateCheck: " + ex.Message); }
        }

        // ================================================================
        //  InstallUpdate v2.7 - fix:
        //  - rimosso MessageBox "Download in corso" bloccante (era PRIMA del download)
        //  - kill processi Autodesk esteso (non solo acad.exe)
        //  - retry su Remove-Item con backoff (gestisce lock residui)
        //  - PowerShell con WindowStyle Normal (errori visibili)
        //  - pausa finale (5s) per leggere eventuali errori
        //  - supporto riapertura AutoCAD 2024/2025/2026
        // ================================================================
        private void InstallUpdate(string downloadUrl)
        {
            try
            {
                var tempZip = Path.Combine(Path.GetTempPath(), "ANGImpianti_update.zip");
                var bundlePath = @"C:\ProgramData\Autodesk\ApplicationPlugins\ANGImpianti.bundle";
                var pluginsPath = @"C:\ProgramData\Autodesk\ApplicationPlugins";

                // 1) Download (silenzioso, senza popup bloccante)
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "ANG-Impianti-Updater");
                client.Timeout = TimeSpan.FromMinutes(5);
                var bytes = client.GetByteArrayAsync(downloadUrl).GetAwaiter().GetResult();
                File.WriteAllBytes(tempZip, bytes);

                // 2) Feedback DOPO il download (non blocca il download stesso)
                MessageBox.Show(
                    "Download completato (" + (bytes.Length / 1024) + " KB).\n\n" +
                    "AutoCAD sta per chiudersi. Una finestra PowerShell si aprira\n" +
                    "per completare l'installazione (e' normale, lasciala lavorare).\n\n" +
                    "Al termine AutoCAD si riavviera automaticamente.",
                    "Aggiornamento ANG-Impianti",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                // 3) Scrivi script PowerShell di install
                var ps1Path = Path.Combine(Path.GetTempPath(), "ang_install.ps1");
                var ps1 = string.Join(Environment.NewLine, new[]
                {
                    "$ErrorActionPreference = 'Continue'",
                    "Write-Host '========================================' -ForegroundColor Cyan",
                    "Write-Host '  ANG-Impianti Updater                  ' -ForegroundColor Cyan",
                    "Write-Host '========================================' -ForegroundColor Cyan",
                    "Write-Host ''",
                    "Write-Host '[1/5] Kill processi Autodesk...'",
                    "Get-Process | Where-Object { $_.ProcessName -match 'acad|AcWebBrowser|AdSSO|AdAppMgrSvc|AutodeskDesktopApp|DLM|Autoload|adsk' } | Stop-Process -Force -ErrorAction SilentlyContinue",
                    "Start-Sleep -Seconds 5",
                    "Write-Host '[2/5] Rimozione bundle vecchio (con retry)...'",
                    "$bundlePath = '" + bundlePath + "'",
                    "$ok = $false",
                    "for ($i = 1; $i -le 5 -and -not $ok; $i++) {",
                    "    try {",
                    "        if (Test-Path $bundlePath) { Remove-Item $bundlePath -Recurse -Force }",
                    "        $ok = $true",
                    "        Write-Host '      OK al tentativo' $i -ForegroundColor Green",
                    "    } catch {",
                    "        Write-Host '      Tentativo' $i 'fallito (lock?). Attendo...' -ForegroundColor Yellow",
                    "        Start-Sleep -Seconds ($i * 2)",
                    "    }",
                    "}",
                    "if (-not $ok) {",
                    "    Write-Host 'IMPOSSIBILE RIMUOVERE IL BUNDLE - chiudi manualmente eventuali processi Autodesk e riprova' -ForegroundColor Red",
                    "    Read-Host 'Premi INVIO per chiudere'",
                    "    exit 1",
                    "}",
                    "Write-Host '[3/5] Estrazione nuovo bundle...'",
                    "Expand-Archive -Path '" + tempZip + "' -DestinationPath '" + pluginsPath + "' -Force",
                    "if (Test-Path '" + pluginsPath + "\\ANGImpianti') { Rename-Item '" + pluginsPath + "\\ANGImpianti' 'ANGImpianti.bundle' }",
                    "Write-Host '[4/5] Verifica DLL...'",
                    "$dll = $bundlePath + '\\Contents\\2025\\ImplantiAI.dll'",
                    "if (Test-Path $dll) {",
                    "    Write-Host '      OK DLL presente' -ForegroundColor Green",
                    "} else {",
                    "    Write-Host '      MANCA DLL - estrazione fallita' -ForegroundColor Red",
                    "    Get-ChildItem $bundlePath -Recurse -ErrorAction SilentlyContinue | ForEach-Object { Write-Host ('   ' + $_.FullName) }",
                    "    Read-Host 'Premi INVIO per chiudere'",
                    "    exit 1",
                    "}",
                    "Remove-Item '" + tempZip + "' -Force -ErrorAction SilentlyContinue",
                    "Write-Host '[5/5] Riavvio AutoCAD...'",
                    // Preferisci l'esatto path AutoCAD che era in esecuzione (no Electrical/Map/MEP per errore)
                    "$preferred = '" + _acadExePath.Replace("\\", "\\\\") + "'",
                    "$candidates = @($preferred,",
                    "    \"$env:ProgramFiles\\Autodesk\\AutoCAD 2026\\acad.exe\",",
                    "    \"$env:ProgramFiles\\Autodesk\\AutoCAD 2025\\acad.exe\",",
                    "    \"$env:ProgramFiles\\Autodesk\\AutoCAD 2024\\acad.exe\"",
                    ")",
                    "$started = $false",
                    "foreach ($p in $candidates) {",
                    "    if ($p -and (Test-Path $p)) {",
                    "        Start-Process $p",
                    "        Write-Host ('      Avviato: ' + $p) -ForegroundColor Green",
                    "        $started = $true",
                    "        break",
                    "    }",
                    "}",
                    "if (-not $started) { Write-Host 'AutoCAD non trovato, aprilo manualmente' -ForegroundColor Yellow }",
                    "Write-Host ''",
                    "Write-Host '========================================' -ForegroundColor Green",
                    "Write-Host '  Aggiornamento completato!             ' -ForegroundColor Green",
                    "Write-Host '========================================' -ForegroundColor Green",
                    "Start-Sleep -Seconds 5"
                });
                File.WriteAllText(ps1Path, ps1);

                // 4) Esegui PowerShell con finestra VISIBILE (Normal style)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-ExecutionPolicy Bypass -WindowStyle Normal -NoProfile -File \"" + ps1Path + "\"",
                    UseShellExecute = true,
                    Verb = "runas"
                });

                // 5) Chiudi AutoCAD (lo script PS aspetta 5s e poi killa il resto)
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

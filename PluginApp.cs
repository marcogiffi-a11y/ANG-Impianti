﻿﻿﻿﻿﻿﻿﻿﻿using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Xml.Linq;
using System.IO.Compression;
using System.Reflection;

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

        // CURRENT_VERSION è risolta a runtime leggendo PackageContents.xml accanto al DLL.
        // Niente hardcoding: il CI/CD aggiorna PackageContents.xml prima del build,
        // così il binario distribuito riporta sempre la versione corretta.
        // Mantenuta come API pubblica statica perché ChatPanel.cs la consuma.
        private static string? _currentVersion;
        public static string CURRENT_VERSION => _currentVersion ??= ResolveInstalledVersionString();

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

        // ─────────────────────────────────────────────────────────────────
        //  RESOLVER VERSIONE INSTALLATA — fonte di verità: PackageContents.xml
        //  Cerca il file risalendo dall'assembly corrente (DLL → Contents/2025/ →
        //  Contents/ → bundle root). Se non lo trova, fallback al file VERSION
        //  copiato nel bundle (vedi CI). In ultima istanza ritorna "0.0" per
        //  forzare l'update (safe-by-default).
        // ─────────────────────────────────────────────────────────────────
        private static string ResolveInstalledVersionString()
        {
            try
            {
                var asmLoc = Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrEmpty(asmLoc)) return "0.0";
                var dir = new DirectoryInfo(Path.GetDirectoryName(asmLoc)!);

                // Risali max 4 livelli cercando PackageContents.xml o VERSION
                for (int i = 0; i < 4 && dir != null; i++)
                {
                    var pc = Path.Combine(dir.FullName, "PackageContents.xml");
                    if (File.Exists(pc))
                    {
                        try
                        {
                            var xml = XDocument.Load(pc);
                            var root = xml.Root;
                            if (root != null)
                            {
                                // Priorità: AppVersion → Version
                                var v = (string?)root.Attribute("AppVersion")
                                     ?? (string?)root.Attribute("Version");
                                if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                            }
                        }
                        catch { /* xml malformato, prova VERSION */ }
                    }
                    var verFile = Path.Combine(dir.FullName, "VERSION");
                    if (File.Exists(verFile))
                    {
                        var v = File.ReadAllText(verFile).Trim();
                        if (!string.IsNullOrWhiteSpace(v)) return v;
                    }
                    dir = dir.Parent;
                }
            }
            catch (System.Exception ex) { Logger.Log("ResolveVersion: " + ex.Message); }
            return "0.0";
        }

        // Confronto numerico vero. Tollera "3", "3.0", "3.0.1" e tutto in mezzo.
        // Ritorna true se `remote` è più recente di `installed`.
        private static bool IsNewer(string remote, string installed)
        {
            // Normalizza: "3.0" → "3.0.0.0"
            static System.Version Norm(string s)
            {
                if (System.Version.TryParse(s, out var v)) return v;
                // tentativo: prendi solo cifre e punti
                var clean = Regex.Replace(s ?? "", @"[^\d\.]", "");
                return System.Version.TryParse(clean, out var v2) ? v2 : new System.Version(0, 0);
            }
            return Norm(remote) > Norm(installed);
        }

        // Cooldown anti-loop: dopo un tentativo fallito (o riuscito) non ricontrollare
        // entro 1 ora. Evita di tempestare l'utente se qualcosa va storto.
        private static string CooldownFilePath() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "ANGImpianti", "last_attempt.txt");

        private static bool IsInCooldown()
        {
            try
            {
                var f = CooldownFilePath();
                if (!File.Exists(f)) return false;
                if (DateTime.TryParse(File.ReadAllText(f).Trim(), out var ts))
                    return (DateTime.UtcNow - ts.ToUniversalTime()) < TimeSpan.FromHours(1);
            }
            catch { }
            return false;
        }

        private static void MarkFailure()
        {
            try
            {
                var f = CooldownFilePath();
                Directory.CreateDirectory(Path.GetDirectoryName(f)!);
                File.WriteAllText(f, DateTime.UtcNow.ToString("o"));
                Logger.Log("MarkFailure: cooldown attivato per 1h");
            }
            catch { }
        }

        private static void ClearCooldown()
        {
            try
            {
                var f = CooldownFilePath();
                if (File.Exists(f)) File.Delete(f);
            }
            catch { }
        }

        private void CheckForUpdates()
        {
            try
            {
                Logger.Log("CheckForUpdates: start");
                if (IsInCooldown()) { Logger.Log("CheckForUpdates: in cooldown, skip"); return; }

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "ANG-Impianti/" + CURRENT_VERSION);
                client.Timeout = TimeSpan.FromSeconds(15);
                var json = client.GetStringAsync(UPDATE_URL).GetAwaiter().GetResult();
                Logger.Log("CheckForUpdates: endpoint response received, " + json.Length + " chars");
                var verMatch = Regex.Match(json, "\"version\":\\s*\"([^\"]+)\"");
                var urlMatch = Regex.Match(json, "\"url\":\\s*\"([^\"]+)\"");
                if (!verMatch.Success) { Logger.Log("CheckForUpdates: no version in response"); return; }
                var latest = verMatch.Groups[1].Value.Trim();
                var downloadUrl = urlMatch.Success ? urlMatch.Groups[1].Value : "";
                Logger.Log("CheckForUpdates: latest=" + latest + " current=" + CURRENT_VERSION);

                // Confronto NUMERICO (era ==, ora >): nessun loop su downgrade o uguale
                if (!IsNewer(latest, CURRENT_VERSION))
                {
                    Logger.Log("CheckForUpdates: installed >= latest, nothing to do");
                    ClearCooldown();  // auto-recovery se cooldown stantio da release passata
                    return;
                }

                // v2.15: dispatcher al UI thread per popup + install sincroni
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher == null)
                {
                    Logger.Log("CheckForUpdates: WPF dispatcher null, fallback to current thread");
                    DoUpdateDialog(latest, downloadUrl);
                }
                else
                {
                    Logger.Log("CheckForUpdates: dispatching to UI thread");
                    dispatcher.Invoke(() => DoUpdateDialog(latest, downloadUrl));
                }
            }
            catch (System.Exception ex) { Logger.Log("UpdateCheck error: " + ex.Message); }
        }

        // v2.15: popup + install eseguiti sincronamente (su UI thread quando possibile)
        private void DoUpdateDialog(string latest, string downloadUrl)
        {
            Logger.Log("DoUpdateDialog: show MessageBox");
            var result = MessageBox.Show(
                "ANG-Impianti: aggiornamento disponibile!\n\nVersione installata: v" +
                CURRENT_VERSION + "\nVersione disponibile: v" + latest +
                "\n\nAggiornare adesso?\n(AutoCAD si chiudera e riaprira automaticamente)",
                "Aggiornamento ANG-Impianti",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);
            Logger.Log("DoUpdateDialog: user clicked " + result);

            if (result == DialogResult.Yes && !string.IsNullOrEmpty(downloadUrl))
            {
                Logger.Log("DoUpdateDialog: calling InstallUpdate SYNCHRONOUSLY");
                InstallUpdate(downloadUrl);  // sincrono, sul thread corrente (UI thread)
            }
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
            Logger.Log("InstallUpdate: ENTRY url=" + downloadUrl);
            // Cooldown SOLO dopo failure: vedi MarkFailure() nei catch.
            // Niente più "ho cliccato Sì e adesso devo aspettare 1h per il prossimo update".
            try
            {
                var tempZip = Path.Combine(Path.GetTempPath(), "ANGImpianti_update.zip");
                Logger.Log("InstallUpdate: downloading to " + tempZip);
                var bundlePath = @"C:\ProgramData\Autodesk\ApplicationPlugins\ANGImpianti.bundle";
                var pluginsPath = @"C:\ProgramData\Autodesk\ApplicationPlugins";

                // 1) Download (silenzioso, senza popup bloccante)
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "ANG-Impianti-Updater");
                client.Timeout = TimeSpan.FromMinutes(5);
                var bytes = client.GetByteArrayAsync(downloadUrl).GetAwaiter().GetResult();
                File.WriteAllBytes(tempZip, bytes);
                Logger.Log("InstallUpdate: downloaded " + bytes.Length + " bytes");

                // 1b) VALIDA lo zip prima di chiudere AutoCAD. Se è corrotto o vuoto,
                //     abortisci adesso che il plugin è ancora caricato: niente loop.
                try
                {
                    using var zipCheck = ZipFile.OpenRead(tempZip);
                    var hasDll = false;
                    foreach (var entry in zipCheck.Entries)
                        if (entry.FullName.EndsWith("ImplantiAI.dll", StringComparison.OrdinalIgnoreCase))
                        { hasDll = true; break; }
                    if (!hasDll) throw new System.Exception("Zip valido ma senza ImplantiAI.dll");
                    Logger.Log("InstallUpdate: zip validated OK");
                }
                catch (System.Exception zex)
                {
                    Logger.Log("InstallUpdate: ZIP INVALID, abort. " + zex.Message);
                    MarkFailure();
                    MessageBox.Show(
                        "Download danneggiato. Riprovo al prossimo avvio.\n\nDettagli: " + zex.Message,
                        "ANG-Impianti", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

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
                Logger.Log("InstallUpdate: wrote ps1 script to " + ps1Path + " (" + ps1.Length + " chars)");

                // 4) Esegui PowerShell con finestra VISIBILE (Normal style)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-ExecutionPolicy Bypass -WindowStyle Normal -NoProfile -File \"" + ps1Path + "\"",
                    UseShellExecute = true,
                    Verb = "runas"
                });

                Logger.Log("InstallUpdate: PowerShell launched, calling Application.Quit");
                // 5) Chiudi AutoCAD (lo script PS aspetta 5s e poi killa il resto)
                Autodesk.AutoCAD.ApplicationServices.Application.Quit();
                Logger.Log("InstallUpdate: Application.Quit returned (should never see this)");
            }
            catch (System.Exception ex)
            {
                Logger.Log("Install: " + ex.Message);
                MarkFailure();
                MessageBox.Show("Errore: " + ex.Message, "Errore",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void Terminate() => MemoryDatabase.Instance.Save();
    }
}

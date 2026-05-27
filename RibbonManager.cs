using Autodesk.Windows;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace ImplantiAI
{
    public static class RibbonManager
    {
        public static void CreateRibbon()
        {
            var rc = ComponentManager.Ribbon;
            if (rc == null) return;

            RibbonTab? old = null;
            foreach (var t in rc.Tabs)
                if (t.Id == "ANG_TAB") { old = t; break; }
            if (old != null) rc.Tabs.Remove(old);

            var tab = new RibbonTab { Title = "ANG-Impianti AI", Id = "ANG_TAB" };

            // ============ LAYER ============
            tab.Panels.Add(MkPanel("Layer ANG",
                MkBig("Aggiorna\nLayer",    "AGGIORNA_LAYER"),
                MkBig("Nuovo\nLayer",       "NUOVO_LAYER")));

            // ============ LIBRERIA SIMBOLI ============
            tab.Panels.Add(MkPanel("Libreria Simboli",
                MkBig("Aggiungi\nSimbolo",    "AGGIUNGI_SIMBOLO"),
                MkBig("Mostra\nLibreria",     "LIBRERIA_SIMBOLI"),
                MkBig("Inserisci\nda Libreria", "INSERISCI_DA_LIBRERIA")));

            // ============ MEMORIZZAZIONE ============
            tab.Panels.Add(MkPanel("Memoria Mary",
                MkBig("Memorizza\nOggetto",   "MEMORIZZA_OGGETTO"),
                MkBig("Memorizza\nProgetto",  "MEMORIZZA_PROGETTO")));

            // ============ VANI ============
            tab.Panels.Add(MkPanel("Vani",
                MkBig("Disegna\nVano", "DISEGNA_VANO"),
                MkBig("Riconosci\nVani", "RICONOSCI_VANI")));

            // ============ AI ============
            tab.Panels.Add(MkPanel("AI",
                MkBig("Chat\nMary", "APRI_CHAT")));

            // ============ SIMBOLI DINAMICI (caricati da Supabase) ============
            // Placeholder che verrà popolato in async
            var dynamicPanel = MkPanel("Simboli (libreria)",
                MkSm("Caricamento...", ""));
            tab.Panels.Add(dynamicPanel);
            _dynamicPanel = dynamicPanel;  // memorizza per refresh dopo AGGIUNGI_SIMBOLO

            rc.Tabs.Add(tab);
            tab.IsActive = true;

            // Carica simboli da Supabase in background
            _ = LoadDynamicSymbols(dynamicPanel);
        }

        // Riferimento statico al panel dinamico: serve a RefreshSymbolsPanel
        // per ricaricare i simboli senza riavviare AutoCAD.
        private static RibbonPanel? _dynamicPanel;

        /// <summary>
        /// Simbolo "in attesa di essere inserito": l'handler del bottone ribbon
        /// (sul thread WPF) lo deposita qui, poi il comando AutoCAD
        /// _RIBBON_INSERT_SYMBOL lo legge dal thread del documento (dove
        /// GetPoint/Database/etc sono utilizzabili).
        /// </summary>
        public static JObject? PendingSymbol;

        /// <summary>
        /// Ricarica il panel "Simboli (libreria)" da Supabase. Chiamato da
        /// AggiungiSimboloCommand dopo un salvataggio andato a buon fine.
        /// </summary>
        public static Task RefreshSymbolsPanel()
        {
            if (_dynamicPanel == null) return Task.CompletedTask;
            return LoadDynamicSymbols(_dynamicPanel);
        }

        private static async Task LoadDynamicSymbols(RibbonPanel panel)
        {
            JArray simboli;
            string? errore = null;
            try { simboli = await SymbolLibrary.CaricaSimboli(); }
            catch (Exception ex) { simboli = new JArray(); errore = ex.Message; Logger.Log("LoadDynamicSymbols: " + ex.Message); }

            // L'aggiornamento del ribbon DEVE girare sul UI thread.
            // Nei plugin AutoCAD `System.Windows.Application.Current` è null
            // (AutoCAD non è un'applicazione WPF standard), quindi usiamo
            // il Dispatcher del panel stesso (è un DispatcherObject WPF) e
            // facciamo fallback a chiamata diretta solo come ultima spiaggia.
            Action update = () =>
            {
                try
                {
                    panel.Source.Items.Clear();

                    if (errore != null)
                    {
                        panel.Source.Title = "Simboli (errore)";
                        panel.Source.Items.Add(MkSm("⚠ Verifica config", ""));
                        return;
                    }

                    panel.Source.Title = $"Simboli ({simboli.Count})";

                    if (simboli.Count == 0)
                    {
                        panel.Source.Items.Add(MkSm("(vuota — usa AGGIUNGI_SIMBOLO)", ""));
                        return;
                    }

                    var perCat = new Dictionary<string, List<JObject>>();
                    foreach (JObject s in simboli)
                    {
                        var cat = (string?)s["categoria"] ?? "Altro";
                        if (!perCat.ContainsKey(cat)) perCat[cat] = new List<JObject>();
                        perCat[cat].Add(s);
                    }

                    bool first = true;
                    foreach (var kv in perCat)
                    {
                        if (!first) panel.Source.Items.Add(new RibbonSeparator());
                        first = false;
                        foreach (var s in kv.Value)
                        {
                            var nome = (string?)s["nome"] ?? "?";
                            var preview = SymbolLibrary.RenderPreview(s, 32);
                            panel.Source.Items.Add(new RibbonButton
                            {
                                Text = TruncateLabel(nome),
                                ShowText = true,
                                ShowImage = preview != null,
                                Image = preview,
                                LargeImage = preview,
                                Size = RibbonItemSize.Standard,
                                Orientation = System.Windows.Controls.Orientation.Horizontal,
                                CommandHandler = new InsertSymbolHandler(s),
                                MinWidth = 100,
                            });
                        }
                    }
                }
                catch (Exception uiEx) { Logger.Log("LoadDynamicSymbols UI: " + uiEx.Message); }
            };

            // Priorità: dispatcher del RibbonControl (è WPF, sicuro) → Application.Current → diretto
            System.Windows.Threading.Dispatcher? dispatcher = null;
            try { dispatcher = ComponentManager.Ribbon?.Dispatcher; } catch { }
            if (dispatcher == null)
            {
                try { dispatcher = System.Windows.Application.Current?.Dispatcher; } catch { }
            }
            if (dispatcher != null)
            {
                if (dispatcher.CheckAccess()) update();
                else dispatcher.Invoke(update);
            }
            else
            {
                Logger.Log("LoadDynamicSymbols: nessun dispatcher disponibile, chiamata diretta");
                update();
            }
        }

        private static string TruncateLabel(string s) => s.Length > 20 ? s.Substring(0, 18) + "…" : s;

        private static RibbonPanel MkPanel(string title, params RibbonItem[] items)
        {
            var p = new RibbonPanel { Source = new RibbonPanelSource { Title = title } };
            foreach (var item in items) p.Source.Items.Add(item);
            return p;
        }

        private static RibbonButton MkBig(string text, string cmd) => new RibbonButton
        {
            Text = text, ShowText = true, ShowImage = false,
            Size = RibbonItemSize.Large,
            Orientation = System.Windows.Controls.Orientation.Vertical,
            CommandHandler = new RibbonCommandHandler(cmd),
            CommandParameter = cmd,
        };

        private static RibbonButton MkSm(string text, string cmd) => new RibbonButton
        {
            Text = text, ShowText = true, ShowImage = false,
            Size = RibbonItemSize.Standard,
            CommandHandler = new RibbonCommandHandler(cmd),
            CommandParameter = cmd,
        };
    }

    /// <summary>Handler generico ribbon: lancia comando AutoCAD.</summary>
    public class RibbonCommandHandler : System.Windows.Input.ICommand
    {
        private readonly string _cmd;
        public RibbonCommandHandler(string cmd) { _cmd = cmd; }
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => !string.IsNullOrEmpty(_cmd);
        public void Execute(object? parameter)
        {
            if (string.IsNullOrEmpty(_cmd)) return;
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc != null) doc.SendStringToExecute(_cmd + " ", true, false, true);
        }
    }

    /// <summary>Handler specifico inserisci simbolo (passa nome simbolo al comando AutoCAD).</summary>
    public class InsertSymbolHandler : System.Windows.Input.ICommand
    {
        private readonly JObject _simbolo;
        public InsertSymbolHandler(JObject simbolo) { _simbolo = simbolo; }
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter)
        {
            // PROBLEMA: questo handler gira sul thread WPF, ma GetPoint/Database/etc
            // richiedono il document context (thread del doc). Soluzione: salvare il
            // simbolo target in una static e iniettare un comando AutoCAD che lo
            // raccoglierà dal contesto giusto via SendStringToExecute.
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) { Logger.Log("InsertSymbolHandler: no active document"); return; }

                RibbonManager.PendingSymbol = _simbolo;
                Logger.Log("InsertSymbolHandler: pending=" + (string?)_simbolo["nome"] + ", invio comando _RIBBON_INSERT_SYMBOL");
                // Lo spazio finale equivale a "premere INVIO" dopo il nome comando
                doc.SendStringToExecute("_RIBBON_INSERT_SYMBOL ", true, false, true);
            }
            catch (Exception ex) { Logger.Log("InsertSymbolHandler.Execute: " + ex.Message); }
        }
    }
}

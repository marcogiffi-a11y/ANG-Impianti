using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ImplantiAI
{
    /// <summary>
    /// v3.0 - Comandi base ANG-Impianti AI
    /// Legenda F-05 cancellata: ogni simbolo viene insegnato da Marco e salvato su Supabase.
    /// </summary>
    public class Commands
    {
        // ========================================================
        // GESTIONE LAYER
        // ========================================================

        [CommandMethod("AGGIORNA_LAYER")]
        public async void AggiornaLayerCommand()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n🔄 Aggiornamento layer ANG da libreria...");
            int n = await LayerManager.AggiornaLayer();
            if (n < 0) ed.WriteMessage("\n⚠ Errore connessione Supabase\n");
        }

        [CommandMethod("NUOVO_LAYER")]
        public async void NuovoLayerCommand()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            var pso = new PromptStringOptions("\nNome layer (senza prefisso ANG_): ") { AllowSpaces = false };
            var nameRes = ed.GetString(pso);
            if (nameRes.Status != PromptStatus.OK) return;
            var nomeBase = nameRes.StringResult.ToUpper().Replace(" ", "_");

            var colorPrompt = new PromptIntegerOptions("\nColore ACI (1-255, default 7=bianco): ")
            { DefaultValue = 7, AllowNone = true, LowerLimit = 1, UpperLimit = 255 };
            var colorRes = ed.GetInteger(colorPrompt);
            short colorAci = (short)(colorRes.Status == PromptStatus.OK ? colorRes.Value : 7);

            var spessorePrompt = new PromptDoubleOptions("\nSpessore mm (default 0.25): ")
            { DefaultValue = 0.25, AllowNone = true };
            var spessoreRes = ed.GetDouble(spessorePrompt);
            double spessore = spessoreRes.Status == PromptStatus.OK ? spessoreRes.Value : 0.25;

            ed.WriteMessage($"\n💾 Creo ANG_{nomeBase}...");
            LayerManager.GetOrCreateLayer(doc.Database, nomeBase, colorAci);

            var keyPrompt = new PromptKeywordOptions("\nSalva nella libreria globale? ");
            keyPrompt.Keywords.Add("Si"); keyPrompt.Keywords.Add("No");
            keyPrompt.Keywords.Default = "Si";
            var keyRes = ed.GetKeywords(keyPrompt);
            if (keyRes.Status == PromptStatus.OK && keyRes.StringResult == "Si")
            {
                bool ok = await LayerManager.SalvaLayer(nomeBase, colorAci, spessore);
                ed.WriteMessage(ok ? "\n✅ Layer salvato in libreria globale.\n" : "\n⚠ Errore salvataggio libreria.\n");
            }
            else
            {
                ed.WriteMessage("\n✅ Layer creato (solo in questo file).\n");
            }
        }

        // ========================================================
        // GESTIONE SIMBOLI
        // ========================================================

        [CommandMethod("AGGIUNGI_SIMBOLO")]
        public async void AggiungiSimboloCommand()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            ed.WriteMessage("\n📚 Seleziona le linee/cerchi/archi del simbolo da memorizzare:");
            var sel = ed.GetSelection();
            if (sel.Status != PromptStatus.OK || sel.Value == null) return;

            var ids = sel.Value.GetObjectIds().ToList();
            if (ids.Count == 0) { ed.WriteMessage("\n⚠ Nessuna entità selezionata.\n"); return; }

            // Estrai geometria
            var geometria = SymbolLibrary.EstraiGeometria(ids, db);
            ed.WriteMessage($"\n📐 Geometria estratta: {(int)geometria["count"]!} entità, "
                + $"bbox {(double)geometria["bbox_w"]!:F1} × {(double)geometria["bbox_h"]!:F1}");

            // Chiedi nome
            var pNome = new PromptStringOptions("\nNome simbolo: ") { AllowSpaces = true };
            var nomeRes = ed.GetString(pNome);
            if (nomeRes.Status != PromptStatus.OK) return;

            // Categoria (keyword)
            var pCat = new PromptKeywordOptions("\nCategoria: ");
            pCat.Keywords.Add("Prese"); pCat.Keywords.Add("Luci");
            pCat.Keywords.Add("Interruttori"); pCat.Keywords.Add("Speciali");
            pCat.Keywords.Add("Domotica"); pCat.Keywords.Add("Sicurezza");
            pCat.Keywords.Add("Altro");
            pCat.Keywords.Default = "Prese";
            var catRes = ed.GetKeywords(pCat);
            if (catRes.Status != PromptStatus.OK) return;

            // Layer associato
            var pLayer = new PromptStringOptions("\nNome layer (sarà prefissato ANG_, default GENERICO): ") { AllowSpaces = false };
            pLayer.DefaultValue = "GENERICO";
            var layerRes = ed.GetString(pLayer);
            var layerNome = (layerRes.Status == PromptStatus.OK && !string.IsNullOrEmpty(layerRes.StringResult))
                ? "ANG_" + layerRes.StringResult.ToUpper().Replace(" ", "_")
                : "ANG_GENERICO";

            // Salva
            ed.WriteMessage("\n💾 Salvataggio simbolo su Supabase...");
            bool ok = await SymbolLibrary.SalvaSimbolo(nomeRes.StringResult, catRes.StringResult, geometria, layerNome);
            ed.WriteMessage(ok
                ? $"\n✅ Simbolo '{nomeRes.StringResult}' salvato in libreria ({catRes.StringResult}).\n   Riavvia AutoCAD per vedere il nuovo pulsante in ribbon.\n"
                : "\n⚠ Errore salvataggio simbolo.\n");
        }

        [CommandMethod("LIBRERIA_SIMBOLI")]
        public async void LibreriaSimboliCommand()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n📦 Caricamento libreria simboli...");
            var simboli = await SymbolLibrary.CaricaSimboli();
            if (simboli.Count == 0)
            {
                ed.WriteMessage("\n📭 Libreria vuota. Usa AGGIUNGI_SIMBOLO per aggiungere il primo.\n");
                return;
            }
            ed.WriteMessage($"\n📚 {simboli.Count} simboli in libreria:");
            string? lastCat = null;
            foreach (JObject s in simboli)
            {
                var cat = (string?)s["categoria"] ?? "Altro";
                if (cat != lastCat) { ed.WriteMessage($"\n\n  [{cat}]"); lastCat = cat; }
                ed.WriteMessage($"\n    • {s["nome"]} (layer: {s["layer_nome"]})");
            }
            ed.WriteMessage("\n\nUsa INSERISCI_DA_LIBRERIA <nome> per piazzarli nel disegno.\n");
        }

        [CommandMethod("INSERISCI_DA_LIBRERIA")]
        public async void InserisciDaLibreriaCommand()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            ed.WriteMessage("\n📥 Caricamento libreria...");
            var simboli = await SymbolLibrary.CaricaSimboli();
            if (simboli.Count == 0) { ed.WriteMessage("\n⚠ Libreria vuota.\n"); return; }

            var pNome = new PromptStringOptions("\nNome simbolo da inserire: ") { AllowSpaces = true };
            var res = ed.GetString(pNome);
            if (res.Status != PromptStatus.OK) return;
            var nome = res.StringResult.Trim().ToLower();

            JObject? simbolo = simboli.Cast<JObject>().FirstOrDefault(s =>
                ((string?)s["nome"] ?? "").ToLower() == nome ||
                ((string?)s["nome"] ?? "").ToLower().Contains(nome));
            if (simbolo == null) { ed.WriteMessage("\n⚠ Simbolo non trovato.\n"); return; }

            var pPoint = new PromptPointOptions("\nPosizione: ");
            var pRes = ed.GetPoint(pPoint);
            if (pRes.Status != PromptStatus.OK) return;

            SymbolLibrary.InserisciSimbolo(simbolo, pRes.Value);
            ed.WriteMessage($"\n✅ {simbolo["nome"]} inserito.\n");
        }

        // ========================================================
        // MEMORIZZA OGGETTO (per arredi/mobili - riconoscimento)
        // ========================================================

        [CommandMethod("MEMORIZZA_OGGETTO")]
        public async void MemorizzaOggettoCommand()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            ed.WriteMessage("\n📦 Seleziona le linee dell'oggetto (mobile/sanitario/arredo):");
            var sel = ed.GetSelection();
            if (sel.Status != PromptStatus.OK || sel.Value == null) return;

            var ids = sel.Value.GetObjectIds().ToList();
            var geometria = SymbolLibrary.EstraiGeometria(ids, db);

            var pNome = new PromptStringOptions("\nCos'è? (nome oggetto): ") { AllowSpaces = true };
            var nomeRes = ed.GetString(pNome);
            if (nomeRes.Status != PromptStatus.OK) return;

            var pCat = new PromptKeywordOptions("\nCategoria: ");
            pCat.Keywords.Add("Arredo"); pCat.Keywords.Add("Sanitario");
            pCat.Keywords.Add("Elettrodomestico"); pCat.Keywords.Add("Infisso");
            pCat.Keywords.Add("Strutturale"); pCat.Keywords.Add("Altro");
            pCat.Keywords.Default = "Arredo";
            var catRes = ed.GetKeywords(pCat);
            if (catRes.Status != PromptStatus.OK) return;

            try
            {
                var payload = new JObject {
                    ["nome"] = nomeRes.StringResult,
                    ["categoria"] = catRes.StringResult,
                    ["geometria"] = geometria,
                    ["bbox_w_cm"] = geometria["bbox_w"],
                    ["bbox_h_cm"] = geometria["bbox_h"],
                    ["num_entities"] = geometria["count"],
                };
                await SupabaseClient.Insert("mary_oggetti_riconosciuti", payload);
                ed.WriteMessage($"\n✅ Oggetto '{nomeRes.StringResult}' memorizzato.\n");
            }
            catch (System.Exception ex) { ed.WriteMessage($"\n⚠ {ex.Message}\n"); }
        }

        // ========================================================
        // MEMORIZZA PROGETTO (stile di progettazione)
        // ========================================================

        [CommandMethod("MEMORIZZA_PROGETTO")]
        public async void MemorizzaProgettoCommand()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            ed.WriteMessage("\n🧠 Salvo i pattern del progetto corrente nella memoria Mary...");

            try
            {
                // Conta simboli per layer ANG_*
                var conteggi = new JObject();
                using var tr = doc.Database.TransactionManager.StartTransaction();
                var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(
                    ((BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead))[BlockTableRecord.ModelSpace],
                    OpenMode.ForRead);
                foreach (ObjectId id in btr)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;
                    if (!ent.Layer.StartsWith("ANG_")) continue;
                    var key = ent.Layer;
                    conteggi[key] = (conteggi[key]?.Value<int>() ?? 0) + 1;
                }
                tr.Commit();

                var payload = new JObject {
                    ["nome_file"] = doc.Name,
                    ["conteggi_per_layer"] = conteggi,
                    ["totale_entita_ang"] = conteggi.Properties().Sum(p => p.Value.Value<int>()),
                };
                await SupabaseClient.Insert("mary_esperienza_progetti", payload);

                ed.WriteMessage($"\n✅ Memorizzati pattern del progetto:\n");
                foreach (var kv in conteggi)
                    ed.WriteMessage($"    {kv.Key}: {kv.Value} entità\n");
            }
            catch (System.Exception ex) { ed.WriteMessage($"\n⚠ {ex.Message}\n"); }
        }

        // ========================================================
        // VANI (comandi legacy mantenuti dalla v2.x)
        // ========================================================

        [CommandMethod("RICONOSCI_VANI")]
        public void RiconosciVaniCommand()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n🏠 Riconoscimento vani: comando ancora da finalizzare in v3.x\n");
        }

        [CommandMethod("DISEGNA_VANO")]
        public void DisegnaVanoCommand()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;
            var pStart = ed.GetPoint("\nPrimo angolo vano: ");
            if (pStart.Status != PromptStatus.OK) return;
            var pEnd = ed.GetCorner("\nSecondo angolo: ", pStart.Value);
            if (pEnd.Status != PromptStatus.OK) return;

            LayerManager.GetOrCreateLayer(db, "ANG_VANI", 5);
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var btr = (BlockTableRecord)tr.GetObject(
                    ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[BlockTableRecord.ModelSpace],
                    OpenMode.ForWrite);
                var pl = new Polyline(4);
                pl.AddVertexAt(0, new Point2d(pStart.Value.X, pStart.Value.Y), 0, 0, 0);
                pl.AddVertexAt(1, new Point2d(pEnd.Value.X, pStart.Value.Y), 0, 0, 0);
                pl.AddVertexAt(2, new Point2d(pEnd.Value.X, pEnd.Value.Y), 0, 0, 0);
                pl.AddVertexAt(3, new Point2d(pStart.Value.X, pEnd.Value.Y), 0, 0, 0);
                pl.Closed = true;
                pl.Layer = "ANG_VANI";
                btr.AppendEntity(pl); tr.AddNewlyCreatedDBObject(pl, true);
                tr.Commit();
            }
            ed.WriteMessage("\n✅ Vano disegnato.\n");
        }

        // ========================================================
        // AI / CHAT (legacy)
        // ========================================================

        [CommandMethod("APRI_CHAT")]
        public void ApriChatCommand()
        {
            if (PluginApp.Palette != null) PluginApp.Palette.Visible = true;
        }
    }
}

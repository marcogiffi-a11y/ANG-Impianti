using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;

namespace ImplantiAI
{
    public class Commands
    {
        [CommandMethod("APRI_CHAT", CommandFlags.Modal)]
        public void ApriChat()
        {
            if (PluginApp.Palette != null)
                PluginApp.Palette.Visible = true;
        }

        // ── DISEGNA VANO ────────────────────────────────────
        [CommandMethod("DISEGNA_VANO", CommandFlags.Modal)]
        public void DisegnaVano()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            try
            {
                var pso = new PromptStringOptions("\nNome vano (es. Soggiorno, Camera 1): ")
                { AllowSpaces = true };
                var psr = ed.GetString(pso);
                if (psr.Status != PromptStatus.OK) return;
                string nome = psr.StringResult;

                var pko = new PromptKeywordOptions(
                    "\nTipo [Soggiorno/Camera/Cucina/Bagno/Corridoio/Studio/Altro]: ");
                foreach (var k in new[] { "Soggiorno","Camera","Cucina","Bagno",
                    "Corridoio","Studio","Altro" })
                    pko.Keywords.Add(k);
                pko.AllowNone = true;
                var pkr = ed.GetKeywords(pko);
                string tipo = pkr.Status == PromptStatus.OK ? pkr.StringResult : "Altro";

                ed.WriteMessage("\nDisegna perimetro '" + nome + "' - INVIO per chiudere:\n");
                var pts = new List<Point3d>();
                while (true)
                {
                    var ppo = new PromptPointOptions(
                        pts.Count == 0 ? "\nPrimo punto: " : "\nPunto " + (pts.Count+1) + ": ");
                    ppo.AllowNone = pts.Count >= 3;
                    var ppr = ed.GetPoint(ppo);
                    if (ppr.Status == PromptStatus.None && pts.Count >= 3) break;
                    if (ppr.Status != PromptStatus.OK) return;
                    pts.Add(ppr.Value);
                }

                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var layerName = "VANO_" + tipo.ToUpper();
                    EnsureLayer(tr, db, layerName, TipoColore(tipo));
                    var btr = GetMS(tr, db);

                    var poly = new Polyline();
                    for (int i = 0; i < pts.Count; i++)
                        poly.AddVertexAt(i, new Point2d(pts[i].X, pts[i].Y), 0, 0, 0);
                    poly.Closed = true;
                    poly.Layer = layerName;
                    poly.LineWeight = LineWeight.LineWeight050;
                    btr.AppendEntity(poly); tr.AddNewlyCreatedDBObject(poly, true);

                    double area = Math.Abs(poly.Area) / 1_000_000;
                    double cx = 0, cy = 0;
                    double minX = double.MaxValue, minY = double.MaxValue;
                    double maxX = double.MinValue, maxY = double.MinValue;
                    foreach (var p in pts)
                    {
                        cx += p.X; cy += p.Y;
                        minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y);
                        maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y);
                    }
                    cx /= pts.Count; cy /= pts.Count;

                    // Testo nome vano
                    var txt = new MText
                    {
                        Contents = nome + "\\P" + area.ToString("F1") + " m\u00b2",
                        Location = new Point3d(cx, cy, 0),
                        TextHeight = 150,
                        Attachment = AttachmentPoint.MiddleCenter,
                        Layer = layerName
                    };
                    btr.AppendEntity(txt); tr.AddNewlyCreatedDBObject(txt, true);
                    tr.Commit();

                    // Salva in memoria
                    var proj = MemoryDatabase.Instance.GetCurrentProject(db.Filename);
                    proj.Rooms.Add(new RoomData
                    {
                        Name = nome, RoomType = tipo.ToLower(),
                        Area = area, CenterX = cx, CenterY = cy,
                        MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY,
                        Width = maxX - minX, Height = maxY - minY
                    });
                    MemoryDatabase.Instance.Save();
                }
                ed.WriteMessage("\n✓ Vano '" + nome + "' (" + tipo + ") disegnato!\n");
                ed.WriteMessage("  Usa RICONOSCI_VANI per aggiornare la lista.\n");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n✗ " + ex.Message + "\n");
                Logger.Log("DisegnaVano: " + ex.Message);
            }
        }

        // ── RICONOSCI VANI ───────────────────────────────────
        [CommandMethod("RICONOSCI_VANI", CommandFlags.Modal)]
        public void RiconosciVani()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            ed.WriteMessage("\n→ Analisi planimetria...\n");
            try
            {
                var rooms = DetectRoomsFromDrawing(db);
                if (rooms.Count == 0)
                {
                    ed.WriteMessage("⚠ Nessun vano trovato.\n");
                    ed.WriteMessage("  Usa DISEGNA_VANO per definire i vani.\n");
                    return;
                }
                var proj = MemoryDatabase.Instance.GetCurrentProject(db.Filename);
                proj.Rooms = rooms;
                MemoryDatabase.Instance.Save();
                ed.WriteMessage("✓ " + rooms.Count + " vani trovati:\n");
                foreach (var r in rooms)
                    ed.WriteMessage("  • " + r.Name + " (" + r.RoomType + ") " +
                        r.Area.ToString("F0") + "m²\n");
                ed.WriteMessage("\n→ Apri la chat e dimmi cosa disegnare!\n");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n✗ " + ex.Message + "\n");
            }
        }

        // ── RICORDA REGOLA ───────────────────────────────────
        [CommandMethod("RICORDA_REGOLA", CommandFlags.Modal)]
        public void RicordaRegola()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            try
            {
                var pso = new PromptStringOptions(
                    "\nRegola da ricordare (es. 'prese sempre a 30cm da pavimento'): ")
                { AllowSpaces = true };
                var psr = ed.GetString(pso);
                if (psr.Status != PromptStatus.OK) return;

                MemoryDatabase.Instance.LearnRule(psr.StringResult, "manuale");
                ed.WriteMessage("✓ Regola salvata! Verrà applicata nei prossimi progetti.\n");
            }
            catch (System.Exception ex) { ed.WriteMessage("\n✗ " + ex.Message + "\n"); }
        }

        // ── MOSTRA REGOLE ────────────────────────────────────
        [CommandMethod("MOSTRA_REGOLE", CommandFlags.Modal)]
        public void MostraRegole()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var rules = MemoryDatabase.Instance.GetRulesForPrompt();
            if (rules.Count == 0)
            {
                ed.WriteMessage("\nNessuna regola salvata.\n");
                ed.WriteMessage("Usa RICORDA_REGOLA per aggiungerne.\n");
                return;
            }
            ed.WriteMessage("\n╔═══════════════════════════════════╗\n");
            ed.WriteMessage("║  REGOLE PERSONALIZZATE            ║\n");
            ed.WriteMessage("╠═══════════════════════════════════╣\n");
            foreach (var r in rules)
                ed.WriteMessage("║ • " + r.Rule.Substring(0, Math.Min(r.Rule.Length, 35)) + "\n");
            ed.WriteMessage("╚═══════════════════════════════════╝\n");
        }

        // ── SIMBOLI ─────────────────────────────────────────
        [CommandMethod("INS_LUCE_SOFFITTO", CommandFlags.Modal)]
        public void InsLuceSoffitto() =>
            InsSymbol("luce_soffitto", "Impianto Elettrico Illuminazione", 2);
        [CommandMethod("INS_LUCE_PARETE", CommandFlags.Modal)]
        public void InsLuceParete() =>
            InsSymbol("luce_parete", "Impianto Elettrico Illuminazione", 2);
        [CommandMethod("INS_LUCE_EMERGENZA", CommandFlags.Modal)]
        public void InsLuceEmergenza() =>
            InsSymbol("emergenza", "Impianto Elettrico Illuminazione", 2);
        [CommandMethod("INS_INT_1P", CommandFlags.Modal)]
        public void InsInt1P() =>
            InsSymbol("interruttore_1p", "Impianto Elettrico Illuminazione", 2);
        [CommandMethod("INS_INT_2P", CommandFlags.Modal)]
        public void InsInt2P() =>
            InsSymbol("interruttore_2p", "Impianto Elettrico Illuminazione", 2);
        [CommandMethod("INS_PULSANTE", CommandFlags.Modal)]
        public void InsPulsante() =>
            InsSymbol("pulsante", "Impianto Elettrico Illuminazione", 2);
        [CommandMethod("INS_DOPPIO_PULSANTE", CommandFlags.Modal)]
        public void InsDoppioPulsante() =>
            InsSymbol("pulsante_doppio", "Impianto Elettrico Illuminazione", 2);
        [CommandMethod("INS_PRESA_UNIV", CommandFlags.Modal)]
        public void InsPresaUniv() =>
            InsSymbol("presa_univ", "Impianto Elettrico Fem", 1);
        [CommandMethod("INS_PRESA_CMD", CommandFlags.Modal)]
        public void InsPresaCmd() =>
            InsSymbol("presa_cmd", "Impianto Elettrico Fem", 1);
        [CommandMethod("INS_PRESA_TV", CommandFlags.Modal)]
        public void InsPresaTV() =>
            InsSymbol("presa_tv", "Impianto Elettrico Dati", 5);
        [CommandMethod("INS_PRESA_SAT", CommandFlags.Modal)]
        public void InsPresaSAT() =>
            InsSymbol("presa_sat", "Impianto Elettrico Dati", 5);
        [CommandMethod("INS_SCATOLA_FEM", CommandFlags.Modal)]
        public void InsScatolaFEM() =>
            InsSymbol("scatola_fem", "Impianto Elettrico Fem", 1);
        [CommandMethod("INS_SCATOLA_LUCE", CommandFlags.Modal)]
        public void InsScatolaLuce() =>
            InsSymbol("scatola_luce", "Impianto Elettrico Illuminazione", 2);
        [CommandMethod("INS_VIDEOCIT_INT", CommandFlags.Modal)]
        public void InsVideocitInt() =>
            InsSymbol("videocit_int", "Impianto Elettrico Dati", 5);
        [CommandMethod("INS_VIDEOCIT_EST", CommandFlags.Modal)]
        public void InsVideocitEst() =>
            InsSymbol("videocit_est", "Impianto Elettrico Dati", 5);
        [CommandMethod("INS_SUONERIA", CommandFlags.Modal)]
        public void InsSuoneria() =>
            InsSymbol("suoneria", "Impianto Elettrico Dati", 5);
        [CommandMethod("INS_VENTILATORE", CommandFlags.Modal)]
        public void InsVentilatore() =>
            InsSymbol("ventilatore", "Impianto Elettrico Fem", 1);
        [CommandMethod("INS_RIV_GAS", CommandFlags.Modal)]
        public void InsRivGas() =>
            InsSymbol("riv_gas", "Impianto Elettrico Allarme", 30);
        [CommandMethod("INS_RIV_H2O", CommandFlags.Modal)]
        public void InsRivH2O() =>
            InsSymbol("riv_acqua", "Impianto Elettrico Allarme", 30);
        [CommandMethod("INS_CRONOTERM", CommandFlags.Modal)]
        public void InsCronoterm() =>
            InsSymbol("cronoterm", "Impianto Elettrico Dati", 5);

        // ── DISTINTA MATERIALI ───────────────────────────────
        [CommandMethod("GENERA_DISTINTA", CommandFlags.Modal)]
        public void GeneraDistinta()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var proj = MemoryDatabase.Instance.GetCurrentProject(
                doc.Database.Filename);

            ed.WriteMessage("\n╔══════════════════════════════════════╗\n");
            ed.WriteMessage("║         DISTINTA MATERIALI           ║\n");
            ed.WriteMessage("╠══════════════════════════════════════╣\n");
            ed.WriteMessage("║  Vani: " + proj.Rooms.Count + "\n");

            if (proj.Circuits.Count > 0)
            {
                double totCavo = 0;
                foreach (var c in proj.Circuits)
                {
                    ed.WriteMessage("║  " + c.CircuitNumber + " " + c.Type +
                        ": " + c.CableSection + "mm² " +
                        "Int." + c.BreakerType + c.BreakerSize + "A\n");
                    ed.WriteMessage("║    Cavo: " + c.CableLength.ToString("F1") + "m\n");
                    totCavo += c.CableLength;
                }
                ed.WriteMessage("╠══════════════════════════════════════╣\n");
                ed.WriteMessage("║  TOTALE CAVO: " + totCavo.ToString("F1") + "m\n");
            }
            else
            {
                ed.WriteMessage("║  Usa la chat per generare i circuiti ║\n");
            }
            ed.WriteMessage("╚══════════════════════════════════════╝\n");
        }

        // ── HELPERS ─────────────────────────────────────────
        private void InsSymbol(string symbolType, string layer, short color)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            ed.WriteMessage("\nInserisci '" + symbolType + "' - INVIO per terminare\n");

            while (true)
            {
                var ppo = new PromptPointOptions("\nPunto (INVIO=fine): ")
                { AllowNone = true };
                var ppr = ed.GetPoint(ppo);
                if (ppr.Status != PromptStatus.OK) break;

                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    EnsureLayer(tr, db, layer, color);
                    var btr = GetMS(tr, db);
                    SymbolDrawer.Draw(tr, btr, symbolType, ppr.Value, layer);
                    tr.Commit();
                }
                ed.WriteMessage("  ✓ Inserito\n");
            }
        }

        private List<RoomData> DetectRoomsFromDrawing(Database db)
        {
            var rooms = new List<RoomData>();
            var keywords = new[] { "camera", "bagno", "cucina", "soggiorno",
                "corridoio", "disimpegno", "ingresso", "studio", "wc",
                "locale", "ripostiglio", "lavanderia" };

            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                var btr = tr.GetObject(bt![BlockTableRecord.ModelSpace],
                    OpenMode.ForRead) as BlockTableRecord;
                if (btr == null) return rooms;

                foreach (var id in btr)
                {
                    var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (e == null) continue;

                    // Metodo 1: Layer VANO_* (da DISEGNA_VANO)
                    if (e.Layer.StartsWith("VANO_") && e is Polyline poly && poly.Closed)
                    {
                        string tipo = e.Layer.Replace("VANO_", "").ToLower();
                        double area = Math.Abs(poly.Area) / 1_000_000;
                        double cx = 0, cy = 0;
                        double minX = double.MaxValue, minY = double.MaxValue;
                        double maxX = double.MinValue, maxY = double.MinValue;

                        for (int i = 0; i < poly.NumberOfVertices; i++)
                        {
                            var pt = poly.GetPoint2dAt(i);
                            cx += pt.X; cy += pt.Y;
                            minX = Math.Min(minX, pt.X); minY = Math.Min(minY, pt.Y);
                            maxX = Math.Max(maxX, pt.X); maxY = Math.Max(maxY, pt.Y);
                        }
                        cx /= poly.NumberOfVertices;
                        cy /= poly.NumberOfVertices;

                        rooms.Add(new RoomData
                        {
                            Name = char.ToUpper(tipo[0]) + tipo.Substring(1),
                            RoomType = tipo, Area = area,
                            CenterX = cx, CenterY = cy,
                            MinX = minX, MinY = minY,
                            MaxX = maxX, MaxY = maxY,
                            Width = maxX - minX, Height = maxY - minY
                        });
                        continue;
                    }

                    // Metodo 2: Testi nel disegno
                    string text = ""; double tx = 0, ty = 0;
                    if (e is MText mt)
                    {
                        text = mt.Contents.Replace("\\A1;","").Replace("\\P"," ").Trim();
                        tx = mt.Location.X; ty = mt.Location.Y;
                    }
                    else if (e is DBText dt)
                    {
                        text = dt.TextString;
                        tx = dt.Position.X; ty = dt.Position.Y;
                    }
                    if (string.IsNullOrEmpty(text) || text.Length < 3) continue;

                    bool isRoom = false;
                    foreach (var kw in keywords)
                        if (text.ToLower().Contains(kw)) { isRoom = true; break; }

                    if (isRoom && !rooms.Exists(r =>
                        Math.Abs(r.CenterX - tx) < 500 &&
                        Math.Abs(r.CenterY - ty) < 500))
                    {
                        rooms.Add(new RoomData
                        {
                            Name = text.Trim(),
                            RoomType = GetRoomType(text),
                            Area = GetEstimatedArea(text),
                            CenterX = tx, CenterY = ty
                        });
                    }
                }
                tr.Commit();
            }
            return rooms;
        }

        private string GetRoomType(string t)
        {
            t = t.ToLower();
            if (t.Contains("bagno") || t.Contains("wc")) return "bagno";
            if (t.Contains("cucina")) return "cucina";
            if (t.Contains("soggiorno") || t.Contains("sala")) return "soggiorno";
            if (t.Contains("camera")) return "camera";
            if (t.Contains("corridoio")) return "corridoio";
            if (t.Contains("studio")) return "studio";
            return "altro";
        }

        private double GetEstimatedArea(string t)
        {
            t = t.ToLower();
            if (t.Contains("bagno") || t.Contains("wc")) return 5;
            if (t.Contains("cucina")) return 12;
            if (t.Contains("soggiorno")) return 25;
            if (t.Contains("camera")) return 14;
            if (t.Contains("corridoio")) return 8;
            return 10;
        }

        private BlockTableRecord GetMS(Transaction tr, Database db)
        {
            var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            return (tr.GetObject(bt![BlockTableRecord.ModelSpace],
                OpenMode.ForWrite) as BlockTableRecord)!;
        }

        private void EnsureLayer(Transaction tr, Database db,
            string name, short color = 7)
        {
            var lt = tr.GetObject(db.LayerTableId, OpenMode.ForWrite) as LayerTable;
            if (lt == null || lt.Has(name)) return;
            var layer = new LayerTableRecord
            {
                Name = name,
                Color = Color.FromColorIndex(ColorMethod.ByAci, color)
            };
            lt.Add(layer); tr.AddNewlyCreatedDBObject(layer, true);
        }

        private short TipoColore(string tipo)
        {
            switch (tipo.ToLower())
            {
                case "bagno": return 4;
                case "cucina": return 1;
                case "soggiorno": return 2;
                case "camera": return 3;
                case "corridoio": return 5;
                case "studio": return 6;
                default: return 7;
            }
        }
    }
}

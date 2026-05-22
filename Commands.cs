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

        [CommandMethod("DISEGNA_VANO", CommandFlags.Modal)]
        public void DisegnaVano()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            try
            {
                var pso = new PromptStringOptions("\nNome vano: ") { AllowSpaces = true };
                var psr = ed.GetString(pso);
                if (psr.Status != PromptStatus.OK) return;
                string nome = psr.StringResult;

                var pko = new PromptKeywordOptions("\nTipo [Soggiorno/Camera/Cucina/Bagno/Corridoio/Altro]: ");
                foreach (var k in new[] { "Soggiorno", "Camera", "Cucina", "Bagno", "Corridoio", "Altro" })
                    pko.Keywords.Add(k);
                pko.AllowNone = true;
                var pkr = ed.GetKeywords(pko);
                string tipo = pkr.Status == PromptStatus.OK ? pkr.StringResult : "Altro";

                ed.WriteMessage($"\nDisegna il perimetro di '{nome}' (INVIO per chiudere):\n");
                var pts = new List<Point3d>();
                while (true)
                {
                    var ppo = new PromptPointOptions(pts.Count == 0 ? "\nPrimo punto: " : "\nPunto successivo (INVIO per chiudere): ");
                    ppo.AllowNone = pts.Count >= 3;
                    var ppr = ed.GetPoint(ppo);
                    if (ppr.Status == PromptStatus.None && pts.Count >= 3) break;
                    if (ppr.Status != PromptStatus.OK) return;
                    pts.Add(ppr.Value);
                }

                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var layer = $"VANO_{tipo.ToUpper()}";
                    EnsureLayer(tr, db, layer, TipoColore(tipo));
                    var btr = GetMS(tr, db);

                    var poly = new Polyline();
                    for (int i = 0; i < pts.Count; i++)
                        poly.AddVertexAt(i, new Point2d(pts[i].X, pts[i].Y), 0, 0, 0);
                    poly.Closed = true;
                    poly.Layer = layer;
                    poly.LineWeight = LineWeight.LineWeight050;
                    btr.AppendEntity(poly); tr.AddNewlyCreatedDBObject(poly, true);

                    double area = Math.Abs(poly.Area) / 1_000_000;
                    double cx = 0, cy = 0;
                    foreach (var p in pts) { cx += p.X; cy += p.Y; }
                    cx /= pts.Count; cy /= pts.Count;

                    var txt = new MText
                    {
                        Contents = $"{nome}\\P{area:F1} m²",
                        Location = new Point3d(cx, cy, 0),
                        TextHeight = 200,
                        Attachment = AttachmentPoint.MiddleCenter,
                        Layer = layer
                    };
                    btr.AppendEntity(txt); tr.AddNewlyCreatedDBObject(txt, true);
                    tr.Commit();

                    var proj = MemoryDatabase.Instance.GetCurrentProject(db.Filename);
                    proj.Rooms.Add(new RoomData
                    {
                        Name = nome, RoomType = tipo.ToLower(),
                        Area = area, CenterX = cx, CenterY = cy
                    });
                    MemoryDatabase.Instance.Save();
                }
                ed.WriteMessage($"\n✓ Vano '{nome}' disegnato!\n");
            }
            catch (System.Exception ex) { ed.WriteMessage($"\n✗ {ex.Message}\n"); }
        }

        [CommandMethod("RICONOSCI_VANI", CommandFlags.Modal)]
        public void RiconosciVani()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            ed.WriteMessage("\n→ Analisi planimetria...\n");
            try
            {
                var rooms = DetectRoomsFromTexts(db);
                if (rooms.Count == 0)
                {
                    ed.WriteMessage("⚠ Nessun vano trovato. Usa DISEGNA_VANO.\n");
                    return;
                }
                var proj = MemoryDatabase.Instance.GetCurrentProject(db.Filename);
                proj.Rooms = rooms;
                MemoryDatabase.Instance.Save();
                ed.WriteMessage($"✓ Trovati {rooms.Count} vani:\n");
                foreach (var r in rooms)
                    ed.WriteMessage($"  • {r.Name} {r.Area:F0}m²\n");
            }
            catch (System.Exception ex) { ed.WriteMessage($"\n✗ {ex.Message}\n"); }
        }

        // ── SIMBOLI ──────────────────────────────────────────
        [CommandMethod("INS_LUCE_SOFFITTO", CommandFlags.Modal)]
        public void InsLuceSoffitto() => InsSymbol("Corpo illuminante soffitto", "Impianto Elettrico Illuminazione", 2);
        [CommandMethod("INS_LUCE_PARETE", CommandFlags.Modal)]
        public void InsLuceParete() => InsSymbol("Corpo illuminante parete", "Impianto Elettrico Illuminazione", 2);
        [CommandMethod("INS_LUCE_EMERGENZA", CommandFlags.Modal)]
        public void InsLuceEmergenza() => InsSymbol("Lampada emergenza", "Impianto Elettrico Illuminazione", 2);
        [CommandMethod("INS_INT_1P", CommandFlags.Modal)]
        public void InsInt1P() => InsSymbol("Interruttore 1P 16A", "Impianto Elettrico Illuminazione", 2);
        [CommandMethod("INS_INT_2P", CommandFlags.Modal)]
        public void InsInt2P() => InsSymbol("Interruttore Bipolare 16A", "Impianto Elettrico Illuminazione", 2);
        [CommandMethod("INS_PULSANTE", CommandFlags.Modal)]
        public void InsPulsante() => InsSymbol("Pulsante 1P NO", "Impianto Elettrico Illuminazione", 2);
        [CommandMethod("INS_DOPPIO_PULSANTE", CommandFlags.Modal)]
        public void InsDoppioPulsante() => InsSymbol("Doppio pulsante", "Impianto Elettrico Illuminazione", 2);
        [CommandMethod("INS_PRESA_UNIV", CommandFlags.Modal)]
        public void InsPresaUniv() => InsSymbol("Presa universale", "Impianto Elettrico Fem", 1);
        [CommandMethod("INS_PRESA_CMD", CommandFlags.Modal)]
        public void InsPresaCmd() => InsSymbol("Presa comandata", "Impianto Elettrico Fem", 1);
        [CommandMethod("INS_PRESA_TV", CommandFlags.Modal)]
        public void InsPresaTV() => InsSymbol("Presa TV", "Impianto Elettrico Dati", 5);
        [CommandMethod("INS_PRESA_SAT", CommandFlags.Modal)]
        public void InsPresaSAT() => InsSymbol("Presa SAT", "Impianto Elettrico Dati", 5);
        [CommandMethod("INS_SCATOLA_FEM", CommandFlags.Modal)]
        public void InsScatolaFEM() => InsSymbol("Scatola FEM", "Impianto Elettrico Fem", 1);
        [CommandMethod("INS_SCATOLA_LUCE", CommandFlags.Modal)]
        public void InsScatolaLuce() => InsSymbol("Scatola luce", "Impianto Elettrico Illuminazione", 2);
        [CommandMethod("INS_VIDEOCIT_INT", CommandFlags.Modal)]
        public void InsVideocitInt() => InsSymbol("Videocitofono interno", "Impianto Elettrico Dati", 5);
        [CommandMethod("INS_VIDEOCIT_EST", CommandFlags.Modal)]
        public void InsVideocitEst() => InsSymbol("Videocitofono esterno", "Impianto Elettrico Dati", 5);
        [CommandMethod("INS_SUONERIA", CommandFlags.Modal)]
        public void InsSuoneria() => InsSymbol("Suoneria", "Impianto Elettrico Dati", 5);
        [CommandMethod("INS_VENTILATORE", CommandFlags.Modal)]
        public void InsVentilatore() => InsSymbol("Ventilatore", "Impianto Elettrico Fem", 1);
        [CommandMethod("INS_RIV_GAS", CommandFlags.Modal)]
        public void InsRivGas() => InsSymbol("Rivelatore GAS", "Impianto Elettrico Allarme", 30);
        [CommandMethod("INS_RIV_H2O", CommandFlags.Modal)]
        public void InsRivH2O() => InsSymbol("Rivelatore Acqua", "Impianto Elettrico Allarme", 30);
        [CommandMethod("INS_CRONOTERM", CommandFlags.Modal)]
        public void InsCronoterm() => InsSymbol("Cronotermostato", "Impianto Elettrico Dati", 5);

        [CommandMethod("GENERA_DISTINTA", CommandFlags.Modal)]
        public void GeneraDistinta()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var proj = MemoryDatabase.Instance.GetCurrentProject(doc.Database.Filename);
            ed.WriteMessage("\n╔══════════════════════════════╗\n");
            ed.WriteMessage("║      DISTINTA MATERIALI      ║\n");
            ed.WriteMessage("╠══════════════════════════════╣\n");
            if (proj.Circuits?.Count > 0)
            {
                double tot = 0;
                foreach (var c in proj.Circuits)
                {
                    ed.WriteMessage($"║ {c.CircuitNumber} {c.Type}: {c.CableSection}mm² {c.CableLength:F1}m\n");
                    tot += c.CableLength;
                }
                ed.WriteMessage($"║ TOTALE CAVO: {tot:F1}m\n");
            }
            else ed.WriteMessage("║ Nessun circuito disegnato\n");
            ed.WriteMessage("╚══════════════════════════════╝\n");
        }

        // ── HELPERS ──────────────────────────────────────────
        private void InsSymbol(string name, string layer, short color)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            ed.WriteMessage($"\nInserisci '{name}' (INVIO per terminare)\n");
            while (true)
            {
                var ppo = new PromptPointOptions($"\nPunto (INVIO=fine): ") { AllowNone = true };
                var ppr = ed.GetPoint(ppo);
                if (ppr.Status != PromptStatus.OK) break;
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    EnsureLayer(tr, db, layer, color);
                    var btr = GetMS(tr, db);
                    DrawSymbol(tr, btr, name, ppr.Value, layer);
                    tr.Commit();
                }
            }
        }

        private void DrawSymbol(Transaction tr, BlockTableRecord btr,
            string name, Point3d pos, string layer)
        {
            var n = name.ToLower();
            double r = 150;
            if (n.Contains("soffitto") || n.Contains("illumin"))
            {
                AddCircle(tr, btr, pos, r, layer);
                AddLine(tr, btr, new Point3d(pos.X - r, pos.Y, 0), new Point3d(pos.X + r, pos.Y, 0), layer);
                AddLine(tr, btr, new Point3d(pos.X, pos.Y - r, 0), new Point3d(pos.X, pos.Y + r, 0), layer);
            }
            else if (n.Contains("presa") || n.Contains("fem"))
            {
                AddCircle(tr, btr, pos, r * 0.8, layer);
                AddLine(tr, btr, new Point3d(pos.X - 40, pos.Y - 60, 0), new Point3d(pos.X - 40, pos.Y + 60, 0), layer);
                AddLine(tr, btr, new Point3d(pos.X + 40, pos.Y - 60, 0), new Point3d(pos.X + 40, pos.Y + 60, 0), layer);
            }
            else if (n.Contains("interruttore") || n.Contains("pulsante"))
            {
                AddCircle(tr, btr, pos, 80, layer);
                AddLine(tr, btr, pos, new Point3d(pos.X + 120, pos.Y + 120, 0), layer);
            }
            else if (n.Contains("scatola"))
            {
                DrawRect(tr, btr, pos, 200, 200, layer);
            }
            else
            {
                AddCircle(tr, btr, pos, 100, layer);
            }
            AddText(tr, btr, new Point3d(pos.X, pos.Y + r + 80, 0),
                name.Length > 12 ? name.Substring(0, 12) : name, 80, layer);
        }

        private List<RoomData> DetectRoomsFromTexts(Database db)
        {
            var rooms = new List<RoomData>();
            var keywords = new[] { "camera", "bagno", "cucina", "soggiorno",
                "corridoio", "disimpegno", "ingresso", "studio", "wc", "rip_", "cam_", "bag_" };

            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                var btr = tr.GetObject(bt![BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                if (btr == null) return rooms;

                foreach (var id in btr)
                {
                    string text = ""; double x = 0, y = 0;
                    var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (e is MText mt) { text = mt.Contents.Replace("\\A1;", ""); x = mt.Location.X; y = mt.Location.Y; }
                    else if (e is DBText dt) { text = dt.TextString; x = dt.Position.X; y = dt.Position.Y; }
                    if (string.IsNullOrEmpty(text)) continue;

                    bool isRoom = false;
                    foreach (var kw in keywords)
                        if (text.ToLower().Contains(kw)) { isRoom = true; break; }

                    if (isRoom)
                        rooms.Add(new RoomData
                        {
                            Name = text.Trim(),
                            RoomType = GetRoomType(text),
                            Area = GetEstimatedArea(text),
                            CenterX = x, CenterY = y
                        });
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
            if (t.Contains("soggiorno")) return "soggiorno";
            if (t.Contains("camera")) return "camera";
            if (t.Contains("corridoio")) return "corridoio";
            return "altro";
        }

        private double GetEstimatedArea(string t)
        {
            t = t.ToLower();
            if (t.Contains("bagno") || t.Contains("wc")) return 5;
            if (t.Contains("cucina")) return 12;
            if (t.Contains("soggiorno")) return 25;
            if (t.Contains("camera")) return 14;
            return 10;
        }

        private BlockTableRecord GetMS(Transaction tr, Database db)
        {
            var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            return tr.GetObject(bt![BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord
                   ?? throw new Exception("ModelSpace non trovato");
        }

        private void EnsureLayer(Transaction tr, Database db, string name, short color)
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

        private short TipoColore(string tipo) => tipo.ToLower() switch
        {
            "bagno" => 4, "cucina" => 1, "soggiorno" => 2,
            "camera" => 3, "corridoio" => 5, _ => 7
        };

        private void AddCircle(Transaction tr, BlockTableRecord btr,
            Point3d c, double r, string layer)
        {
            var circle = new Circle(c, Vector3d.ZAxis, r) { Layer = layer };
            btr.AppendEntity(circle); tr.AddNewlyCreatedDBObject(circle, true);
        }

        private void AddLine(Transaction tr, BlockTableRecord btr,
            Point3d p1, Point3d p2, string layer)
        {
            var line = new Line(p1, p2) { Layer = layer };
            btr.AppendEntity(line); tr.AddNewlyCreatedDBObject(line, true);
        }

        private void AddText(Transaction tr, BlockTableRecord btr,
            Point3d pos, string text, double h, string layer)
        {
            var t = new DBText
            {
                TextString = text, Position = pos, Height = h,
                HorizontalMode = TextHorizontalMode.TextCenter,
                AlignmentPoint = pos, Layer = layer
            };
            btr.AppendEntity(t); tr.AddNewlyCreatedDBObject(t, true);
        }

        private void DrawRect(Transaction tr, BlockTableRecord btr,
            Point3d c, double w, double h, string layer)
        {
            var p = new Polyline();
            p.AddVertexAt(0, new Point2d(c.X - w / 2, c.Y - h / 2), 0, 0, 0);
            p.AddVertexAt(1, new Point2d(c.X + w / 2, c.Y - h / 2), 0, 0, 0);
            p.AddVertexAt(2, new Point2d(c.X + w / 2, c.Y + h / 2), 0, 0, 0);
            p.AddVertexAt(3, new Point2d(c.X - w / 2, c.Y + h / 2), 0, 0, 0);
            p.Closed = true; p.Layer = layer;
            btr.AppendEntity(p); tr.AddNewlyCreatedDBObject(p, true);
        }
    }
}

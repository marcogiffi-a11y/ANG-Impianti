using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;

namespace ImplantiAI
{
    // Disegna i simboli elettrici fedeli alla legenda dell'utente
    public static class SymbolDrawer
    {
        public static void Draw(Transaction tr, BlockTableRecord btr,
            string symbolType, Point3d pos, string layer)
        {
            switch (symbolType.ToLower())
            {
                case "luce_soffitto":
                    DrawLightCeiling(tr, btr, pos, layer); break;
                case "luce_parete":
                    DrawLightWall(tr, btr, pos, layer); break;
                case "emergenza":
                    DrawEmergency(tr, btr, pos, layer); break;
                case "interruttore_1p":
                    DrawSwitch1P(tr, btr, pos, layer); break;
                case "interruttore_2p":
                    DrawSwitch2P(tr, btr, pos, layer); break;
                case "pulsante":
                    DrawButton(tr, btr, pos, layer); break;
                case "pulsante_doppio":
                    DrawDoubleButton(tr, btr, pos, layer); break;
                case "presa_univ":
                    DrawSocketUniv(tr, btr, pos, layer); break;
                case "presa_cmd":
                    DrawSocketCmd(tr, btr, pos, layer); break;
                case "presa_tv":
                    DrawSocketTV(tr, btr, pos, layer, "TV"); break;
                case "presa_sat":
                    DrawSocketTV(tr, btr, pos, layer, "SAT"); break;
                case "scatola_fem":
                    DrawJunctionBox(tr, btr, pos, layer, "FEM"); break;
                case "scatola_luce":
                    DrawJunctionBox(tr, btr, pos, layer, "ILL"); break;
                case "videocit_int":
                    DrawVideophone(tr, btr, pos, layer, "P.I."); break;
                case "videocit_est":
                    DrawVideophone(tr, btr, pos, layer, "P.E."); break;
                case "suoneria":
                    DrawBell(tr, btr, pos, layer); break;
                case "ventilatore":
                    DrawFan(tr, btr, pos, layer); break;
                case "riv_gas":
                    DrawDetector(tr, btr, pos, layer, "CH4"); break;
                case "riv_acqua":
                    DrawDetector(tr, btr, pos, layer, "H2O"); break;
                case "cronoterm":
                    DrawThermostat(tr, btr, pos, layer); break;
                default:
                    DrawGeneric(tr, btr, pos, layer, symbolType); break;
            }
        }

        public static string GetLayerForSymbol(string symbolType)
        {
            switch (symbolType.ToLower())
            {
                case "luce_soffitto":
                case "luce_parete":
                case "emergenza":
                case "interruttore_1p":
                case "interruttore_2p":
                case "pulsante":
                case "pulsante_doppio":
                case "scatola_luce":
                    return "Impianto Elettrico Illuminazione";
                case "presa_univ":
                case "presa_cmd":
                case "scatola_fem":
                case "ventilatore":
                    return "Impianto Elettrico Fem";
                case "presa_tv":
                case "presa_sat":
                case "videocit_int":
                case "videocit_est":
                case "suoneria":
                case "cronoterm":
                    return "Impianto Elettrico Dati";
                case "riv_gas":
                case "riv_acqua":
                    return "Impianto Elettrico Allarme";
                default:
                    return "Impianto Elettrico";
            }
        }

        // ── CORPO ILLUMINANTE SOFFITTO ────────────────────────
        // Cerchio con croce (come nella legenda)
        private static void DrawLightCeiling(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            double r = 150;
            AddCircle(tr, btr, pos, r, layer);
            // Croce
            AddLine(tr, btr,
                new Point3d(pos.X - r, pos.Y, 0),
                new Point3d(pos.X + r, pos.Y, 0), layer);
            AddLine(tr, btr,
                new Point3d(pos.X, pos.Y - r, 0),
                new Point3d(pos.X, pos.Y + r, 0), layer);
        }

        // ── CORPO ILLUMINANTE PARETE ──────────────────────────
        // Semicerchio con linea
        private static void DrawLightWall(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            double r = 130;
            AddCircle(tr, btr, pos, r, layer);
            // Linea verso il basso (montaggio a parete)
            AddLine(tr, btr, pos,
                new Point3d(pos.X, pos.Y - r * 1.5, 0), layer);
            // Lineetta orizzontale base
            AddLine(tr, btr,
                new Point3d(pos.X - r * 0.5, pos.Y - r * 1.5, 0),
                new Point3d(pos.X + r * 0.5, pos.Y - r * 1.5, 0), layer);
        }

        // ── LAMPADA EMERGENZA ─────────────────────────────────
        // Rettangolo con EM
        private static void DrawEmergency(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            DrawRect(tr, btr, pos, 300, 160, layer);
            AddText(tr, btr, pos, "EM", 90, layer);
        }

        // ── INTERRUTTORE 1P ───────────────────────────────────
        // Cerchio piccolo + leva a 45° + quadratino
        private static void DrawSwitch1P(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            double r = 70;
            AddCircle(tr, btr, pos, r, layer);
            // Leva
            AddLine(tr, btr, pos,
                new Point3d(pos.X + r * 1.8, pos.Y + r * 1.8, 0), layer);
            // Punto finale leva
            AddCircle(tr, btr,
                new Point3d(pos.X + r * 1.8, pos.Y + r * 1.8, 0), 20, layer);
        }

        // ── INTERRUTTORE 2P ───────────────────────────────────
        // Due cerchi + due leve
        private static void DrawSwitch2P(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            DrawSwitch1P(tr, btr, pos, layer);
            double r = 70;
            var pos2 = new Point3d(pos.X + r * 2.5, pos.Y, 0);
            AddCircle(tr, btr, pos2, r, layer);
            AddLine(tr, btr, pos2,
                new Point3d(pos2.X + r * 1.8, pos2.Y + r * 1.8, 0), layer);
            AddCircle(tr, btr,
                new Point3d(pos2.X + r * 1.8, pos2.Y + r * 1.8, 0), 20, layer);
            // Barra di collegamento
            AddLine(tr, btr, pos, pos2, layer);
        }

        // ── PULSANTE ──────────────────────────────────────────
        // Cerchio vuoto + P
        private static void DrawButton(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            double r = 80;
            AddCircle(tr, btr, pos, r, layer);
            // Linea di connessione
            AddLine(tr, btr,
                new Point3d(pos.X, pos.Y + r, 0),
                new Point3d(pos.X, pos.Y + r * 2, 0), layer);
        }

        // ── DOPPIO PULSANTE ───────────────────────────────────
        private static void DrawDoubleButton(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            double r = 80;
            var p1 = new Point3d(pos.X - r * 1.5, pos.Y, 0);
            var p2 = new Point3d(pos.X + r * 1.5, pos.Y, 0);
            AddCircle(tr, btr, p1, r, layer);
            AddCircle(tr, btr, p2, r, layer);
            AddLine(tr, btr, p1, p2, layer);
            AddLine(tr, btr,
                new Point3d(pos.X, pos.Y, 0),
                new Point3d(pos.X, pos.Y + r * 2, 0), layer);
        }

        // ── PRESA UNIVERSALE ──────────────────────────────────
        // Cerchio con due linee verticali + UNIV
        private static void DrawSocketUniv(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            double r = 120;
            AddCircle(tr, btr, pos, r, layer);
            // Due poli
            double sp = 45;
            AddLine(tr, btr,
                new Point3d(pos.X - sp, pos.Y - 60, 0),
                new Point3d(pos.X - sp, pos.Y + 60, 0), layer);
            AddLine(tr, btr,
                new Point3d(pos.X + sp, pos.Y - 60, 0),
                new Point3d(pos.X + sp, pos.Y + 60, 0), layer);
            AddText(tr, btr,
                new Point3d(pos.X, pos.Y + r + 80, 0), "UNIV", 70, layer);
        }

        // ── PRESA COMANDATA ───────────────────────────────────
        // Come universale + simbolo interruttore
        private static void DrawSocketCmd(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            DrawSocketUniv(tr, btr, pos, layer);
            double r = 120;
            AddText(tr, btr,
                new Point3d(pos.X, pos.Y + r + 80, 0), "CMD", 70, layer);
        }

        // ── PRESA TV/SAT ──────────────────────────────────────
        // Rettangolo con etichetta
        private static void DrawSocketTV(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer, string label)
        {
            DrawRect(tr, btr, pos, 200, 160, layer);
            AddText(tr, btr, pos, label, 80, layer);
        }

        // ── SCATOLA DERIVAZIONE ───────────────────────────────
        // Quadrato con etichetta
        private static void DrawJunctionBox(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer, string label)
        {
            DrawRect(tr, btr, pos, 200, 200, layer);
            // Diagonali
            AddLine(tr, btr,
                new Point3d(pos.X - 100, pos.Y - 100, 0),
                new Point3d(pos.X + 100, pos.Y + 100, 0), layer);
            AddLine(tr, btr,
                new Point3d(pos.X + 100, pos.Y - 100, 0),
                new Point3d(pos.X - 100, pos.Y + 100, 0), layer);
            AddText(tr, btr,
                new Point3d(pos.X, pos.Y + 150, 0), label, 70, layer);
        }

        // ── VIDEOCITOFONO ─────────────────────────────────────
        private static void DrawVideophone(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer, string label)
        {
            DrawRect(tr, btr, pos, 220, 300, layer);
            // "Schermo" interno
            DrawRect(tr, btr,
                new Point3d(pos.X, pos.Y + 50, 0), 150, 120, layer);
            AddText(tr, btr,
                new Point3d(pos.X, pos.Y - 100, 0), label, 70, layer);
        }

        // ── SUONERIA ──────────────────────────────────────────
        // Cerchio con linee
        private static void DrawBell(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            double r = 110;
            AddCircle(tr, btr, pos, r, layer);
            // Linee decorative
            for (int i = 0; i < 3; i++)
            {
                double angle = (i - 1) * 30 * Math.PI / 180;
                AddLine(tr, btr,
                    new Point3d(pos.X + r * 0.4 * Math.Cos(angle),
                                pos.Y + r * 0.4 * Math.Sin(angle), 0),
                    new Point3d(pos.X + r * 0.9 * Math.Cos(angle),
                                pos.Y + r * 0.9 * Math.Sin(angle), 0), layer);
            }
        }

        // ── VENTILATORE ───────────────────────────────────────
        // Cerchio con pale
        private static void DrawFan(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            double r = 130;
            AddCircle(tr, btr, pos, r, layer);
            AddCircle(tr, btr, pos, r * 0.2, layer);
            // 4 pale
            for (int i = 0; i < 4; i++)
            {
                double angle = i * Math.PI / 2;
                AddLine(tr, btr,
                    new Point3d(pos.X + r * 0.2 * Math.Cos(angle),
                                pos.Y + r * 0.2 * Math.Sin(angle), 0),
                    new Point3d(pos.X + r * 0.8 * Math.Cos(angle + 0.3),
                                pos.Y + r * 0.8 * Math.Sin(angle + 0.3), 0), layer);
            }
        }

        // ── RIVELATORE ────────────────────────────────────────
        private static void DrawDetector(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer, string tipo)
        {
            double r = 120;
            AddCircle(tr, btr, pos, r, layer);
            AddCircle(tr, btr, pos, r * 0.55, layer);
            AddText(tr, btr, pos, tipo, 75, layer);
        }

        // ── CRONOTERMOSTATO ───────────────────────────────────
        private static void DrawThermostat(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            DrawRect(tr, btr, pos, 320, 220, layer);
            // Display
            DrawRect(tr, btr,
                new Point3d(pos.X - 40, pos.Y + 20, 0), 160, 80, layer);
            AddText(tr, btr, pos, "CT", 90, layer);
        }

        // ── GENERICO ──────────────────────────────────────────
        private static void DrawGeneric(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer, string label)
        {
            AddCircle(tr, btr, pos, 100, layer);
            string lbl = label.Length > 4 ? label.Substring(0, 4) : label;
            AddText(tr, btr, pos, lbl, 70, layer);
        }

        // ── HELPERS ───────────────────────────────────────────
        public static void AddCircle(Transaction tr, BlockTableRecord btr,
            Point3d c, double r, string layer)
        {
            var circle = new Circle(c, Vector3d.ZAxis, r) { Layer = layer };
            btr.AppendEntity(circle); tr.AddNewlyCreatedDBObject(circle, true);
        }

        public static void AddLine(Transaction tr, BlockTableRecord btr,
            Point3d p1, Point3d p2, string layer)
        {
            var line = new Line(p1, p2) { Layer = layer };
            btr.AppendEntity(line); tr.AddNewlyCreatedDBObject(line, true);
        }

        public static void AddText(Transaction tr, BlockTableRecord btr,
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

        public static void DrawRect(Transaction tr, BlockTableRecord btr,
            Point3d c, double w, double h, string layer)
        {
            var p = new Polyline();
            p.AddVertexAt(0, new Point2d(c.X - w/2, c.Y - h/2), 0, 0, 0);
            p.AddVertexAt(1, new Point2d(c.X + w/2, c.Y - h/2), 0, 0, 0);
            p.AddVertexAt(2, new Point2d(c.X + w/2, c.Y + h/2), 0, 0, 0);
            p.AddVertexAt(3, new Point2d(c.X - w/2, c.Y + h/2), 0, 0, 0);
            p.Closed = true; p.Layer = layer;
            btr.AppendEntity(p); tr.AddNewlyCreatedDBObject(p, true);
        }
    }
}

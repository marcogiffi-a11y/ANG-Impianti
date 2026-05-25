using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;

namespace ImplantiAI
{
    // Simboli elettrici in scala 1:1 (1 unità = 1 metro)
    // Dimensioni basate sulla legenda F-05 di Athena Next Gen
    public static class SymbolDrawer
    {
        public static void Draw(Transaction tr, BlockTableRecord btr,
            string symbolType, Point3d pos, string layer)
        {
            switch (symbolType.ToLower())
            {
                case "luce_soffitto":   DrawLightCeiling(tr, btr, pos, layer); break;
                case "luce_parete":     DrawLightWall(tr, btr, pos, layer); break;
                case "emergenza":       DrawEmergency(tr, btr, pos, layer); break;
                case "interruttore_1p": DrawSwitch1P(tr, btr, pos, layer); break;
                case "interruttore_2p": DrawSwitch2P(tr, btr, pos, layer); break;
                case "pulsante":        DrawButton(tr, btr, pos, layer); break;
                case "pulsante_doppio": DrawDoubleButton(tr, btr, pos, layer); break;
                case "presa_univ":      DrawSocketUniv(tr, btr, pos, layer); break;
                case "presa_cmd":       DrawSocketCmd(tr, btr, pos, layer); break;
                case "presa_tv":        DrawSocketTV(tr, btr, pos, layer, "TV"); break;
                case "presa_sat":       DrawSocketTV(tr, btr, pos, layer, "SAT"); break;
                case "scatola_fem":     DrawJunctionBox(tr, btr, pos, layer, "FEM"); break;
                case "scatola_luce":    DrawJunctionBox(tr, btr, pos, layer, "ILL"); break;
                case "videocit_int":    DrawVideophone(tr, btr, pos, layer, "P.I."); break;
                case "videocit_est":    DrawVideophone(tr, btr, pos, layer, "P.E."); break;
                case "suoneria":        DrawBell(tr, btr, pos, layer); break;
                case "ventilatore":     DrawFan(tr, btr, pos, layer); break;
                case "riv_gas":         DrawDetector(tr, btr, pos, layer, "CH4"); break;
                case "riv_acqua":       DrawDetector(tr, btr, pos, layer, "H2O"); break;
                case "cronoterm":       DrawThermostat(tr, btr, pos, layer); break;
                default:                DrawGeneric(tr, btr, pos, layer, symbolType); break;
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

        // ── CORPO ILLUMINANTE SOFFITTO ─────────────────────────
        // Due cerchi concentrici con croce (scala legenda: R_ext=0.125m, R_int=0.080m)
        private static void DrawLightCeiling(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            double r = 0.125;
            double ri = 0.080;
            AddCircle(tr, btr, pos, r, layer);
            AddCircle(tr, btr, pos, ri, layer);
            AddLine(tr, btr,
                new Point3d(pos.X - r, pos.Y, 0),
                new Point3d(pos.X + r, pos.Y, 0), layer);
            AddLine(tr, btr,
                new Point3d(pos.X, pos.Y - r, 0),
                new Point3d(pos.X, pos.Y + r, 0), layer);
        }

        // ── CORPO ILLUMINANTE PARETE ───────────────────────────
        // Cerchio con linea verso il basso
        private static void DrawLightWall(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            double r = 0.103;
            AddCircle(tr, btr, pos, r, layer);
            AddLine(tr, btr, pos,
                new Point3d(pos.X, pos.Y - r * 1.5, 0), layer);
            AddLine(tr, btr,
                new Point3d(pos.X - r * 0.5, pos.Y - r * 1.5, 0),
                new Point3d(pos.X + r * 0.5, pos.Y - r * 1.5, 0), layer);
        }

        // ── LAMPADA EMERGENZA ──────────────────────────────────
        // Rettangolo 0.30x0.16m con "EM"
        private static void DrawEmergency(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            DrawRect(tr, btr, pos, 0.300, 0.160, layer);
            AddText(tr, btr, pos, "EM", 0.090, layer);
        }

        // ── INTERRUTTORE 1P 16A ────────────────────────────────
        // Cerchio piccolo + leva a 45° + punto finale
        private static void DrawSwitch1P(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            double r = 0.038;
            AddCircle(tr, btr, pos, r, layer);
            double leva = 0.130;
            double angle = Math.PI / 4;
            var pEnd = new Point3d(pos.X + leva * Math.Cos(angle),
                                   pos.Y + leva * Math.Sin(angle), 0);
            AddLine(tr, btr, pos, pEnd, layer);
            AddCircle(tr, btr, pEnd, 0.015, layer);
        }

        // ── INTERRUTTORE BIPOLARE 16A ──────────────────────────
        // Due interruttori collegati
        private static void DrawSwitch2P(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            DrawSwitch1P(tr, btr, pos, layer);
            double offset = 0.120;
            var pos2 = new Point3d(pos.X + offset, pos.Y, 0);
            DrawSwitch1P(tr, btr, pos2, layer);
            AddLine(tr, btr, pos, pos2, layer);
        }

        // ── PULSANTE 1P NO 10A ─────────────────────────────────
        // Cerchio + linea uscente
        private static void DrawButton(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            double r = 0.038;
            AddCircle(tr, btr, pos, r, layer);
            AddLine(tr, btr,
                new Point3d(pos.X, pos.Y + r, 0),
                new Point3d(pos.X, pos.Y + r * 2.5, 0), layer);
        }

        // ── DOPPIO PULSANTE ────────────────────────────────────
        private static void DrawDoubleButton(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            double r = 0.038;
            double sp = 0.080;
            var p1 = new Point3d(pos.X - sp, pos.Y, 0);
            var p2 = new Point3d(pos.X + sp, pos.Y, 0);
            AddCircle(tr, btr, p1, r, layer);
            AddCircle(tr, btr, p2, r, layer);
            AddLine(tr, btr, p1, p2, layer);
            AddLine(tr, btr,
                new Point3d(pos.X, pos.Y, 0),
                new Point3d(pos.X, pos.Y + r * 2.5, 0), layer);
        }

        // ── PRESA UNIVERSALE ───────────────────────────────────
        // Cerchio con due poli verticali + "UNIV"
        private static void DrawSocketUniv(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            double r = 0.075;
            AddCircle(tr, btr, pos, r, layer);
            double sp = 0.025;
            AddLine(tr, btr,
                new Point3d(pos.X - sp, pos.Y - 0.040, 0),
                new Point3d(pos.X - sp, pos.Y + 0.040, 0), layer);
            AddLine(tr, btr,
                new Point3d(pos.X + sp, pos.Y - 0.040, 0),
                new Point3d(pos.X + sp, pos.Y + 0.040, 0), layer);
            AddText(tr, btr,
                new Point3d(pos.X, pos.Y + r + 0.040, 0), "UNIV", 0.050, layer);
        }

        // ── PRESA COMANDATA ────────────────────────────────────
        private static void DrawSocketCmd(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            double r = 0.075;
            AddCircle(tr, btr, pos, r, layer);
            double sp = 0.025;
            AddLine(tr, btr,
                new Point3d(pos.X - sp, pos.Y - 0.040, 0),
                new Point3d(pos.X - sp, pos.Y + 0.040, 0), layer);
            AddLine(tr, btr,
                new Point3d(pos.X + sp, pos.Y - 0.040, 0),
                new Point3d(pos.X + sp, pos.Y + 0.040, 0), layer);
            AddText(tr, btr,
                new Point3d(pos.X, pos.Y + r + 0.040, 0), "CM", 0.050, layer);
        }

        // ── PRESA TV / SAT ─────────────────────────────────────
        // Rettangolo con etichetta
        private static void DrawSocketTV(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer, string label)
        {
            DrawRect(tr, btr, pos, 0.140, 0.110, layer);
            AddText(tr, btr, pos, label, 0.060, layer);
        }

        // ── SCATOLA DERIVAZIONE ────────────────────────────────
        // Quadrato con X + etichetta (FEM o ILL)
        private static void DrawJunctionBox(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer, string label)
        {
            double s = 0.130;
            DrawRect(tr, btr, pos, s, s, layer);
            AddLine(tr, btr,
                new Point3d(pos.X - s/2, pos.Y - s/2, 0),
                new Point3d(pos.X + s/2, pos.Y + s/2, 0), layer);
            AddLine(tr, btr,
                new Point3d(pos.X + s/2, pos.Y - s/2, 0),
                new Point3d(pos.X - s/2, pos.Y + s/2, 0), layer);
            AddText(tr, btr,
                new Point3d(pos.X, pos.Y + s/2 + 0.040, 0), label, 0.050, layer);
        }

        // ── VIDEOCITOFONO ──────────────────────────────────────
        // Rettangolo con "schermo" interno + etichetta
        private static void DrawVideophone(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer, string label)
        {
            DrawRect(tr, btr, pos, 0.160, 0.220, layer);
            DrawRect(tr, btr,
                new Point3d(pos.X, pos.Y + 0.030, 0), 0.110, 0.090, layer);
            AddText(tr, btr,
                new Point3d(pos.X, pos.Y - 0.080, 0), label, 0.050, layer);
        }

        // ── SUONERIA ───────────────────────────────────────────
        // Cerchio con linee radiali + "A"
        private static void DrawBell(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            double r = 0.075;
            AddCircle(tr, btr, pos, r, layer);
            for (int i = 0; i < 3; i++)
            {
                double angle = (i - 1) * 30 * Math.PI / 180;
                AddLine(tr, btr,
                    new Point3d(pos.X + r * 0.35 * Math.Cos(angle),
                                pos.Y + r * 0.35 * Math.Sin(angle), 0),
                    new Point3d(pos.X + r * 0.85 * Math.Cos(angle),
                                pos.Y + r * 0.85 * Math.Sin(angle), 0), layer);
            }
            AddText(tr, btr,
                new Point3d(pos.X, pos.Y + r + 0.030, 0), "A", 0.045, layer);
        }

        // ── VENTILATORE ────────────────────────────────────────
        // Cerchio esterno + cerchio interno + 4 pale
        private static void DrawFan(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            double r = 0.103;
            AddCircle(tr, btr, pos, r, layer);
            AddCircle(tr, btr, pos, r * 0.18, layer);
            for (int i = 0; i < 4; i++)
            {
                double angle = i * Math.PI / 2;
                AddLine(tr, btr,
                    new Point3d(pos.X + r * 0.18 * Math.Cos(angle),
                                pos.Y + r * 0.18 * Math.Sin(angle), 0),
                    new Point3d(pos.X + r * 0.75 * Math.Cos(angle + 0.4),
                                pos.Y + r * 0.75 * Math.Sin(angle + 0.4), 0), layer);
            }
        }

        // ── RIVELATORE GAS / ACQUA ─────────────────────────────
        // Cerchio + cerchio interno + etichetta
        private static void DrawDetector(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer, string tipo)
        {
            double r = 0.075;
            AddCircle(tr, btr, pos, r, layer);
            AddCircle(tr, btr, pos, r * 0.55, layer);
            AddText(tr, btr, pos, tipo, 0.045, layer);
        }

        // ── CRONOTERMOSTATO ────────────────────────────────────
        // Rettangolo con display + "CT"
        private static void DrawThermostat(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            DrawRect(tr, btr, pos, 0.240, 0.160, layer);
            DrawRect(tr, btr,
                new Point3d(pos.X - 0.030, pos.Y + 0.015, 0), 0.120, 0.060, layer);
            AddText(tr, btr,
                new Point3d(pos.X + 0.065, pos.Y - 0.020, 0), "CT", 0.050, layer);
        }

        // ── GENERICO ───────────────────────────────────────────
        private static void DrawGeneric(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer, string label)
        {
            AddCircle(tr, btr, pos, 0.075, layer);
            string lbl = label.Length > 4 ? label.Substring(0, 4) : label;
            AddText(tr, btr, pos, lbl, 0.050, layer);
        }

        // ── HELPERS ────────────────────────────────────────────
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

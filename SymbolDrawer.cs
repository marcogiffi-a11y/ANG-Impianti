using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;

namespace ImplantiAI
{
    // Simboli fedeli alla legenda F-05 Athena Next Gen
    // Scala 1:1 - 1 unità AutoCAD = 1 metro
    // Geometria estratta direttamente dal DXF F-05_LEGENDA_IMPIANTO_ELETTRICO
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
                case "centralino":      DrawPanel(tr, btr, pos, layer); break;
                default:                DrawGeneric(tr, btr, pos, layer, symbolType); break;
            }
        }

        public static string GetLayerForSymbol(string symbolType)
        {
            switch (symbolType.ToLower())
            {
                case "luce_soffitto": case "luce_parete": case "emergenza":
                case "interruttore_1p": case "interruttore_2p":
                case "pulsante": case "pulsante_doppio": case "scatola_luce":
                    return "Impianto Elettrico Illuminazione";
                case "presa_univ": case "presa_cmd": case "scatola_fem":
                case "ventilatore": case "centralino":
                    return "Impianto Elettrico Fem";
                case "presa_tv": case "presa_sat": case "videocit_int":
                case "videocit_est": case "suoneria": case "cronoterm":
                    return "Impianto Elettrico Dati";
                case "riv_gas": case "riv_acqua":
                    return "Impianto Elettrico Allarme";
                default: return "Impianto Elettrico";
            }
        }

        // ── CORPO ILLUMINANTE SOFFITTO (Riga 19 DXF) ──────────
        // Due cerchi concentrici: R_est=0.1247, R_int=0.0807
        private static void DrawLightCeiling(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddCircle(tr, btr, pos, 0.1247, layer);
            AddCircle(tr, btr, pos, 0.0807, layer);
        }

        // ── CORPO ILLUMINANTE PARETE (Riga 7 DXF) ─────────────
        // Cerchio R=0.1028 + linea verticale sotto + base orizzontale
        private static void DrawLightWall(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddCircle(tr, btr, pos, 0.1028, layer);
            // Linea verticale sotto (lunghezza = raggio)
            AddLine(tr, btr,
                new Point3d(pos.X, pos.Y - 0.1028, 0),
                new Point3d(pos.X, pos.Y - 0.1028 - 0.1029, 0), layer);
        }

        // ── LAMPADA EMERGENZA (Riga 8 DXF) ────────────────────
        // Cerchio R=0.1028 + linea verticale (come parete ma con testo EM)
        private static void DrawEmergency(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddCircle(tr, btr, pos, 0.1028, layer);
            AddLine(tr, btr,
                new Point3d(pos.X, pos.Y - 0.1028, 0),
                new Point3d(pos.X, pos.Y - 0.2057, 0), layer);
            AddText(tr, btr, pos, "EM", 0.050, layer);
        }

        // ── INTERRUTTORE 1P (Riga 5 DXF) ──────────────────────
        // Piccolo rettangolo (corpo) + freccia a destra con V aperta
        // Dal DXF: rettangolo (0.204 x 0.226m) con linea interna + "freccia"
        private static void DrawSwitch1P(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            double w = 0.204, h = 0.226;
            // Corpo rettangolare
            DrawRect(tr, btr, pos, w, h, layer);
            // Linea interna orizzontale (a 2/3 dell'altezza)
            AddLine(tr, btr,
                new Point3d(pos.X - w/2, pos.Y + h/6, 0),
                new Point3d(pos.X + w/2, pos.Y + h/6, 0), layer);
            // Freccia: linea orizzontale + V aperta (dalla legenda)
            double arrowBase = pos.X + w/2;
            AddLine(tr, btr,
                new Point3d(arrowBase, pos.Y + h/6, 0),
                new Point3d(arrowBase + 0.128, pos.Y + h/6, 0), layer);
            AddLine(tr, btr,
                new Point3d(arrowBase + 0.128, pos.Y + h/6, 0),
                new Point3d(arrowBase + 0.315, pos.Y + h/6 + 0.050, 0), layer);
            AddLine(tr, btr,
                new Point3d(arrowBase + 0.128, pos.Y + h/6, 0),
                new Point3d(arrowBase + 0.315, pos.Y + h/6 - 0.050, 0), layer);
            AddLine(tr, btr,
                new Point3d(arrowBase + 0.315, pos.Y + h/6 + 0.050, 0),
                new Point3d(arrowBase + 0.315, pos.Y + h/6 - 0.050, 0), layer);
        }

        // ── INTERRUTTORE 2P ────────────────────────────────────
        // Due interruttori 1P affiancati con barra
        private static void DrawSwitch2P(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            double offset = 0.280;
            var p1 = new Point3d(pos.X - offset/2, pos.Y, 0);
            var p2 = new Point3d(pos.X + offset/2, pos.Y, 0);
            DrawSwitch1P(tr, btr, p1, layer);
            DrawSwitch1P(tr, btr, p2, layer);
            AddLine(tr, btr,
                new Point3d(p1.X, p1.Y + 0.113, 0),
                new Point3d(p2.X, p2.Y + 0.113, 0), layer);
        }

        // ── PULSANTE 1P NO (Riga 15 DXF) ──────────────────────
        // Grande semicerchio R=0.183 + piccolo semicerchio R=0.043 + linee
        private static void DrawButton(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            // Grande semicerchio superiore (da 0° a 180°)
            AddArc(tr, btr, pos, 0.183, 0, Math.PI, layer);
            // Linea di base orizzontale
            AddLine(tr, btr,
                new Point3d(pos.X - 0.183, pos.Y, 0),
                new Point3d(pos.X + 0.183, pos.Y, 0), layer);
            // Piccolo semicerchio superiore
            var pSmall = new Point3d(pos.X - 0.046, pos.Y + 0.069, 0);
            AddArc(tr, btr, pSmall, 0.043, Math.PI * 17.2/180, Math.PI * 162.8/180, layer);
        }

        // ── DOPPIO PULSANTE ────────────────────────────────────
        private static void DrawDoubleButton(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            DrawButton(tr, btr, pos, layer);
            var pos2 = new Point3d(pos.X + 0.420, pos.Y, 0);
            DrawButton(tr, btr, pos2, layer);
            AddLine(tr, btr,
                new Point3d(pos.X + 0.183, pos.Y, 0),
                new Point3d(pos2.X - 0.183, pos2.Y, 0), layer);
        }

        // ── PRESA UNIVERSALE (Riga 10 DXF) ────────────────────
        // X formata da 4 frecce + arco parziale sul lato sinistro
        // Dal DXF: linee orizzontali + diagonali a 45° + arco R=0.0605
        private static void DrawSocketUniv(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            double seg = 0.038;  // segmento orizzontale
            double diag = 0.100; // proiezione diagonale
            // Lato destro (freccia destra)
            AddLine(tr, btr, pos, new Point3d(pos.X + seg, pos.Y, 0), layer);
            AddLine(tr, btr,
                new Point3d(pos.X + seg, pos.Y, 0),
                new Point3d(pos.X + seg + diag, pos.Y + diag, 0), layer);
            AddLine(tr, btr,
                new Point3d(pos.X + seg, pos.Y, 0),
                new Point3d(pos.X + seg + diag, pos.Y - diag, 0), layer);
            // Lato sinistro (freccia sinistra)
            AddLine(tr, btr, pos, new Point3d(pos.X - seg, pos.Y, 0), layer);
            AddLine(tr, btr,
                new Point3d(pos.X - seg, pos.Y, 0),
                new Point3d(pos.X - seg - diag, pos.Y - diag, 0), layer);
            AddLine(tr, btr,
                new Point3d(pos.X - seg, pos.Y, 0),
                new Point3d(pos.X - seg - diag, pos.Y + diag, 0), layer);
            // Arco R=0.0605 sul lato sinistro (~288° a 71°)
            var arcCenter = new Point3d(pos.X - seg - diag * 0.62, pos.Y, 0);
            AddArc(tr, btr, arcCenter, 0.0605,
                288.6 * Math.PI / 180, (360 + 71.2) * Math.PI / 180, layer);
            // Etichetta
            AddText(tr, btr,
                new Point3d(pos.X, pos.Y + diag + 0.040, 0), "UNIV", 0.050, layer);
        }

        // ── PRESA COMANDATA (Riga 11 DXF) ─────────────────────
        // Come presa universale ma con etichetta CM
        private static void DrawSocketCmd(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            double seg = 0.038, diag = 0.100;
            AddLine(tr, btr, pos, new Point3d(pos.X + seg, pos.Y, 0), layer);
            AddLine(tr, btr,
                new Point3d(pos.X + seg, pos.Y, 0),
                new Point3d(pos.X + seg + diag, pos.Y + diag, 0), layer);
            AddLine(tr, btr,
                new Point3d(pos.X + seg, pos.Y, 0),
                new Point3d(pos.X + seg + diag, pos.Y - diag, 0), layer);
            AddLine(tr, btr, pos, new Point3d(pos.X - seg, pos.Y, 0), layer);
            AddLine(tr, btr,
                new Point3d(pos.X - seg, pos.Y, 0),
                new Point3d(pos.X - seg - diag, pos.Y - diag, 0), layer);
            AddLine(tr, btr,
                new Point3d(pos.X - seg, pos.Y, 0),
                new Point3d(pos.X - seg - diag, pos.Y + diag, 0), layer);
            var arcCenter = new Point3d(pos.X - seg - diag * 0.62, pos.Y, 0);
            AddArc(tr, btr, arcCenter, 0.0605,
                288.6 * Math.PI / 180, (360 + 71.2) * Math.PI / 180, layer);
            AddText(tr, btr,
                new Point3d(pos.X, pos.Y + diag + 0.040, 0), "CM", 0.050, layer);
        }

        // ── PRESA TV / SAT ─────────────────────────────────────
        // Dal DXF: arco R=0.0808 (180°-360°) + linea + etichetta
        private static void DrawSocketTV(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer, string label)
        {
            // Semicerchio inferiore (R=0.0808, da 180° a 360°)
            AddArc(tr, btr, pos, 0.0808, Math.PI, 2 * Math.PI, layer);
            // Linea verticale sopra
            AddLine(tr, btr,
                new Point3d(pos.X, pos.Y, 0),
                new Point3d(pos.X, pos.Y + 0.094, 0), layer);
            // Linea orizzontale in cima
            AddLine(tr, btr,
                new Point3d(pos.X - 0.0812, pos.Y + 0.094, 0),
                new Point3d(pos.X + 0.0812, pos.Y + 0.094, 0), layer);
            AddText(tr, btr,
                new Point3d(pos.X, pos.Y - 0.0808 - 0.040, 0), label, 0.050, layer);
        }

        // ── SCATOLA DERIVAZIONE (Riga 14 DXF) ─────────────────
        // X (croce diagonale) = due linee che si incrociano
        private static void DrawJunctionBox(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer, string label)
        {
            double s = 0.120;
            // X (diagonali)
            AddLine(tr, btr,
                new Point3d(pos.X - s, pos.Y - s, 0),
                new Point3d(pos.X + s, pos.Y + s, 0), layer);
            AddLine(tr, btr,
                new Point3d(pos.X - s, pos.Y + s, 0),
                new Point3d(pos.X + s, pos.Y - s, 0), layer);
            AddText(tr, btr,
                new Point3d(pos.X, pos.Y + s + 0.040, 0), label, 0.050, layer);
        }

        // ── VIDEOCITOFONO (Riga 20 DXF) ───────────────────────
        // Rettangolo 0.169x0.275m + 4 piccoli cerchi interni
        // cx_rel = 0, dimensioni dal DXF
        private static void DrawVideophone(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer, string label)
        {
            double w = 0.169, h = 0.275;
            DrawRect(tr, btr, pos, w, h, layer);
            // 4 piccoli cerchi (R=0.037 e R=0.055) a coppie
            double dy = 0.055;
            var p1 = new Point3d(pos.X, pos.Y + dy, 0);
            var p2 = new Point3d(pos.X, pos.Y - dy, 0);
            AddCircle(tr, btr, p1, 0.037, layer);
            AddCircle(tr, btr, p1, 0.055, layer);
            AddCircle(tr, btr, p2, 0.037, layer);
            AddCircle(tr, btr, p2, 0.055, layer);
            AddText(tr, btr,
                new Point3d(pos.X, pos.Y - h/2 - 0.040, 0), label, 0.050, layer);
        }

        // ── SUONERIA (Riga 23-25 DXF) ─────────────────────────
        // Arco R=0.0808 (180°-360°) + linea verticale + linea orizzontale
        private static void DrawBell(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            // Semicerchio inferiore
            AddArc(tr, btr, pos, 0.0808, Math.PI, 2 * Math.PI, layer);
            // Linea verticale
            AddLine(tr, btr,
                new Point3d(pos.X, pos.Y, 0),
                new Point3d(pos.X, pos.Y + 0.094, 0), layer);
            // Linea orizzontale
            AddLine(tr, btr,
                new Point3d(pos.X - 0.0812, pos.Y + 0.094, 0),
                new Point3d(pos.X + 0.0812, pos.Y + 0.094, 0), layer);
            AddText(tr, btr,
                new Point3d(pos.X, pos.Y - 0.0808 - 0.040, 0), "A", 0.050, layer);
        }

        // ── VENTILATORE (Riga 21 DXF) ─────────────────────────
        // Cerchio R=0.0714 + 3 segmenti interni (pale)
        private static void DrawFan(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            double r = 0.0714;
            AddCircle(tr, btr, pos, r, layer);
            // 3 linee interne a 120°
            for (int i = 0; i < 3; i++)
            {
                double a = i * 2 * Math.PI / 3;
                AddLine(tr, btr, pos,
                    new Point3d(pos.X + r * 0.85 * Math.Cos(a),
                                pos.Y + r * 0.85 * Math.Sin(a), 0), layer);
            }
        }

        // ── RIVELATORI GAS/ACQUA (Righe 17-18 DXF) ────────────
        // Cerchio R=0.0747 + brevi segmenti radiali + etichetta
        private static void DrawDetector(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer, string tipo)
        {
            double r = 0.0747;
            AddCircle(tr, btr, pos, r, layer);
            // Segmenti radiali (come nella riga 17/18 del DXF)
            AddLine(tr, btr,
                new Point3d(pos.X + r * 0.3, pos.Y + r * 0.7, 0),
                new Point3d(pos.X + r * 0.6, pos.Y + r * 1.0, 0), layer);
            AddLine(tr, btr,
                new Point3d(pos.X + r * 0.7, pos.Y + r * 0.3, 0),
                new Point3d(pos.X + r * 1.0, pos.Y - r * 0.2, 0), layer);
            AddText(tr, btr, pos, tipo, 0.040, layer);
        }

        // ── CRONOTERMOSTATO (Riga 22 DXF) ─────────────────────
        // Rettangolo 0.389x0.224m + linea interna + "CM"
        private static void DrawThermostat(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            DrawRect(tr, btr, pos, 0.389, 0.224, layer);
            // Linea interna orizzontale (display)
            AddLine(tr, btr,
                new Point3d(pos.X - 0.154, pos.Y + 0.024, 0),
                new Point3d(pos.X + 0.115, pos.Y + 0.024, 0), layer);
            AddText(tr, btr, pos, "CM", 0.060, layer);
        }

        // ── CENTRALINO (Riga 4 DXF) ───────────────────────────
        // Rettangolo 0.251x0.125m con 4 linee verticali interne
        private static void DrawPanel(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            double w = 0.251, h = 0.125;
            DrawRect(tr, btr, pos, w, h, layer);
            for (int i = 1; i <= 4; i++)
            {
                double x = pos.X - w/2 + i * w/5;
                AddLine(tr, btr,
                    new Point3d(x, pos.Y - h/2, 0),
                    new Point3d(x, pos.Y + h/2, 0), layer);
            }
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
            var e = new Circle(c, Vector3d.ZAxis, r) { Layer = layer };
            btr.AppendEntity(e); tr.AddNewlyCreatedDBObject(e, true);
        }

        public static void AddLine(Transaction tr, BlockTableRecord btr,
            Point3d p1, Point3d p2, string layer)
        {
            var e = new Line(p1, p2) { Layer = layer };
            btr.AppendEntity(e); tr.AddNewlyCreatedDBObject(e, true);
        }

        public static void AddArc(Transaction tr, BlockTableRecord btr,
            Point3d c, double r, double startAngle, double endAngle, string layer)
        {
            var e = new Arc(c, r, startAngle, endAngle) { Layer = layer };
            btr.AppendEntity(e); tr.AddNewlyCreatedDBObject(e, true);
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

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;

namespace ImplantiAI
{
    // Gestisce il tracciamento automatico dei cavi
    public static class CableRouter
    {
        // Traccia un cavo tra due punti con etichetta sezione
        public static void RouteWithLabel(Transaction tr, BlockTableRecord btr,
            Point3d from, Point3d to, string layer,
            string cableSection, string circuitLabel)
        {
            // Tracciato ortogonale (prima orizzontale poi verticale)
            var mid = new Point3d(to.X, from.Y, 0);

            // Prima tratta (orizzontale)
            if (Math.Abs(from.X - to.X) > 1)
            {
                SymbolDrawer.AddLine(tr, btr, from, mid, layer);
                // Etichetta sezione cavo
                if (!string.IsNullOrEmpty(cableSection))
                {
                    double midX = (from.X + mid.X) / 2;
                    SymbolDrawer.AddText(tr, btr,
                        new Point3d(midX, from.Y + 120, 0),
                        cableSection + "mm²", 80, layer);
                }
            }

            // Seconda tratta (verticale)
            if (Math.Abs(mid.Y - to.Y) > 1)
            {
                SymbolDrawer.AddLine(tr, btr, mid, to, layer);
            }

            // Numero circuito
            if (!string.IsNullOrEmpty(circuitLabel))
            {
                double cx = (from.X + to.X) / 2;
                double cy = (from.Y + to.Y) / 2;
                SymbolDrawer.AddText(tr, btr,
                    new Point3d(cx, cy - 150, 0), circuitLabel, 70, layer);
            }
        }

        // Traccia circuito completo tra lista di simboli
        public static void RouteCircuit(Transaction tr, BlockTableRecord btr,
            List<(Point3d pos, string label)> symbols,
            string layer, string cableSection, string circuitNum)
        {
            if (symbols.Count < 2) return;

            for (int i = 0; i < symbols.Count - 1; i++)
            {
                var label = i == 0 ? circuitNum : "";
                RouteWithLabel(tr, btr,
                    symbols[i].pos, symbols[i + 1].pos,
                    layer, cableSection, label);
            }
        }

        // Trova i simboli elettrici nel disegno per collegarli
        public static List<(Point3d pos, string layer)> FindElectricalSymbols(
            Database db, string layerFilter = "")
        {
            var symbols = new List<(Point3d, string)>();
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                var btr = tr.GetObject(bt![BlockTableRecord.ModelSpace],
                    OpenMode.ForRead) as BlockTableRecord;
                if (btr == null) return symbols;

                foreach (var id in btr)
                {
                    var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (e == null) continue;

                    bool isElectrical =
                        e.Layer.StartsWith("Impianto Elettrico") &&
                        (string.IsNullOrEmpty(layerFilter) ||
                         e.Layer == layerFilter);

                    if (!isElectrical) continue;

                    if (e is Circle c)
                        symbols.Add((c.Center, c.Layer));
                }
                tr.Commit();
            }
            return symbols;
        }

        // Calcola la sezione cavo corretta
        public static string CalcCableSection(int numPoints, double distanceM,
            string circuitType)
        {
            if (circuitType.ToLower().Contains("luce"))
            {
                if (distanceM > 30 || numPoints > 8) return "2.5";
                return "1.5";
            }
            if (circuitType.ToLower().Contains("cucina") ||
                circuitType.ToLower().Contains("lavatrice"))
                return "4.0";
            if (distanceM > 25) return "4.0";
            return "2.5";
        }

        // Calcola interruttore corretto
        public static int CalcBreakerSize(string circuitType, string cableSection)
        {
            if (circuitType.ToLower().Contains("luce")) return 10;
            if (circuitType.ToLower().Contains("cucina")) return 20;
            if (cableSection == "4.0") return 20;
            return 16;
        }
    }
}

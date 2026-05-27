// v3.0: Legenda F-05 cancellata.
// La classe SymbolDrawer mantiene SOLO gli helper geometrici neutri
// (AddLine, AddText) usati da CableRouter e ChatPanel.
// Il metodo Draw(symbolType) è stato sostituito da uno stub che
// disegna un cerchio placeholder con etichetta — i veri simboli ora
// arrivano da SymbolLibrary (Supabase).

using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace ImplantiAI
{
    public static class SymbolDrawer
    {
        // Helper geometrici neutri usati da CableRouter.cs
        public static void AddLine(Transaction tr, BlockTableRecord btr, Point3d a, Point3d b, string layer)
        {
            EnsureLayer(btr.Database, layer);
            var l = new Line(a, b) { Layer = layer };
            btr.AppendEntity(l);
            tr.AddNewlyCreatedDBObject(l, true);
        }

        public static void AddText(Transaction tr, BlockTableRecord btr, Point3d pos, string text, double height, string layer)
        {
            EnsureLayer(btr.Database, layer);
            var t = new DBText
            {
                Position = pos,
                TextString = text,
                Height = height,
                Layer = layer,
            };
            btr.AppendEntity(t);
            tr.AddNewlyCreatedDBObject(t, true);
        }

        public static string GetLayerForSymbol(string? symbolType)
        {
            if (string.IsNullOrEmpty(symbolType)) return "ANG_GENERICO";
            var s = symbolType.ToLower();
            if (s.Contains("presa") || s.Contains("scatola") || s.Contains("schuko") || s.Contains("bipasso")) return "ANG_PRESE_FM";
            if (s.Contains("luce") || s.Contains("plafoniera") || s.Contains("faretto")) return "ANG_LUCI";
            if (s.Contains("inter") || s.Contains("deviator") || s.Contains("pulsante")) return "ANG_COMANDI";
            if (s.Contains("quadr")) return "ANG_QUADRI";
            return "ANG_GENERICO";
        }

        /// <summary>
        /// STUB: disegna un cerchio placeholder con etichetta.
        /// La legenda F-05 non esiste più: per i simboli veri usa SymbolLibrary.InserisciSimbolo().
        /// </summary>
        public static void Draw(Transaction tr, BlockTableRecord btr, string? symbolType, Point3d pos, string layer)
        {
            EnsureLayer(btr.Database, layer);
            var c = new Circle(pos, Vector3d.ZAxis, 8) { Layer = layer };
            btr.AppendEntity(c);
            tr.AddNewlyCreatedDBObject(c, true);
            // Etichetta sopra
            var t = new DBText
            {
                Position = new Point3d(pos.X + 10, pos.Y + 5, 0),
                TextString = symbolType ?? "?",
                Height = 8,
                Layer = layer,
            };
            btr.AppendEntity(t);
            tr.AddNewlyCreatedDBObject(t, true);
        }

        private static void EnsureLayer(Database db, string name)
        {
            using var tr = db.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);
            if (!lt.Has(name))
            {
                var lr = new LayerTableRecord { Name = name, Color = Color.FromColorIndex(ColorMethod.ByAci, 7) };
                lt.Add(lr);
                tr.AddNewlyCreatedDBObject(lr, true);
            }
            tr.Commit();
        }
    }
}

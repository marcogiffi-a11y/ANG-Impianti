// v3.0: Legenda F-05 cancellata.
// Tutti i simboli sono ora gestiti dinamicamente dalla SymbolLibrary
// che li carica da Supabase. Per aggiungere simboli usa il comando
// AGGIUNGI_SIMBOLO dalla ribbon.

using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.DatabaseServices;

namespace ImplantiAI
{
    public static class SymbolDrawer
    {
        // Legacy stub: rimasto vuoto per evitare rotture sui chiamanti.
        // I simboli verranno inseriti via SymbolLibrary.InsertSymbol(symbolId, pos).
        public static void DrawPlaceholder(Database db, Point3d pos, string label)
        {
            using var tr = db.TransactionManager.StartTransaction();
            var btr = (BlockTableRecord)tr.GetObject(
                ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[BlockTableRecord.ModelSpace],
                OpenMode.ForWrite);
            var c = new Circle(pos, Vector3d.ZAxis, 5);
            btr.AppendEntity(c); tr.AddNewlyCreatedDBObject(c, true);
            tr.Commit();
        }
    }
}

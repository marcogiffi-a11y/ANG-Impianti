using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ImplantiAI
{
    /// <summary>
    /// Gestione layer ANG_*: importa da Supabase, crea custom in AutoCAD, sincronizza.
    /// </summary>
    public static class LayerManager
    {
        public const string PREFIX = "ANG_";

        /// <summary>Carica i layer ANG_* da Supabase e li importa nel DXF corrente.</summary>
        public static async Task<int> AggiornaLayer()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return 0;
            var db = doc.Database;
            int aggiunti = 0, aggiornati = 0;

            try
            {
                var layers = await SupabaseClient.Select("mary_layer_elettrici", "order=nome");

                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);
                    foreach (JObject l in layers)
                    {
                        var nome = (string?)l["nome"] ?? "";
                        if (!nome.StartsWith(PREFIX)) nome = PREFIX + nome;
                        var colorAci = (short)((int?)l["colore_aci"] ?? 7);
                        var lineweightVal = (double?)l["spessore_mm"] ?? 0.25;

                        if (lt.Has(nome))
                        {
                            var lr = (LayerTableRecord)tr.GetObject(lt[nome], OpenMode.ForWrite);
                            lr.Color = Color.FromColorIndex(ColorMethod.ByAci, colorAci);
                            lr.LineWeight = ConvertMmToLineweight(lineweightVal);
                            aggiornati++;
                        }
                        else
                        {
                            var lr = new LayerTableRecord
                            {
                                Name = nome,
                                Color = Color.FromColorIndex(ColorMethod.ByAci, colorAci),
                                LineWeight = ConvertMmToLineweight(lineweightVal),
                            };
                            lt.Add(lr);
                            tr.AddNewlyCreatedDBObject(lr, true);
                            aggiunti++;
                        }
                    }
                    tr.Commit();
                }
                doc.Editor.WriteMessage($"\n✅ Layer ANG sincronizzati: {aggiunti} nuovi, {aggiornati} aggiornati.\n");
                return aggiunti + aggiornati;
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage($"\n⚠ Errore aggiornamento layer: {ex.Message}\n");
                return -1;
            }
        }

        /// <summary>Salva un layer custom su Supabase (libreria globale).</summary>
        public static async Task<bool> SalvaLayer(string nomeBase, short colorAci, double spessoreMm, string descrizione = "")
        {
            var nome = nomeBase.StartsWith(PREFIX) ? nomeBase : PREFIX + nomeBase;
            try
            {
                // Verifica se esiste
                var esistenti = await SupabaseClient.Select("mary_layer_elettrici", $"nome=eq.{nome}");
                var patch = new JObject {
                    ["nome"] = nome,
                    ["descrizione"] = descrizione,
                    ["colore_aci"] = colorAci,
                    ["spessore_mm"] = spessoreMm,
                };
                if (esistenti.Count > 0)
                {
                    return await SupabaseClient.Update("mary_layer_elettrici", $"nome=eq.{nome}", patch);
                }
                else
                {
                    await SupabaseClient.Insert("mary_layer_elettrici", patch);
                    return true;
                }
            }
            catch { return false; }
        }

        /// <summary>Crea (o trova) il layer in AutoCAD e lo restituisce.</summary>
        public static ObjectId GetOrCreateLayer(Database db, string nome, short colorAci = 7)
        {
            if (!nome.StartsWith(PREFIX)) nome = PREFIX + nome;
            using var tr = db.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);
            ObjectId id;
            if (lt.Has(nome))
            {
                id = lt[nome];
            }
            else
            {
                var lr = new LayerTableRecord
                {
                    Name = nome,
                    Color = Color.FromColorIndex(ColorMethod.ByAci, colorAci),
                };
                id = lt.Add(lr);
                tr.AddNewlyCreatedDBObject(lr, true);
            }
            tr.Commit();
            return id;
        }

        private static LineWeight ConvertMmToLineweight(double mm)
        {
            if (mm <= 0.05) return LineWeight.LineWeight000;
            if (mm <= 0.13) return LineWeight.LineWeight013;
            if (mm <= 0.18) return LineWeight.LineWeight018;
            if (mm <= 0.25) return LineWeight.LineWeight025;
            if (mm <= 0.30) return LineWeight.LineWeight030;
            if (mm <= 0.35) return LineWeight.LineWeight035;
            if (mm <= 0.50) return LineWeight.LineWeight050;
            if (mm <= 0.70) return LineWeight.LineWeight070;
            return LineWeight.LineWeight100;
        }
    }
}

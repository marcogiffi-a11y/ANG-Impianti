using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ImplantiAI
{
    /// <summary>
    /// Libreria simboli ANG: ogni simbolo è una collezione di linee/cerchi/archi
    /// salvati con coordinate relative al centroide della selezione.
    /// L'inserimento riapplica la geometria nelle nuove coordinate.
    /// </summary>
    public static class SymbolLibrary
    {
        /// <summary>Estrae geometria primaria di un insieme di entità selezionate.</summary>
        public static JObject EstraiGeometria(IEnumerable<ObjectId> ids, Database db)
        {
            var entities = new JArray();
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;

            using var tr = db.TransactionManager.StartTransaction();
            foreach (var id in ids)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null) continue;

                var ext = ent.GeometricExtents;
                if (ext.MinPoint.X < minX) minX = ext.MinPoint.X;
                if (ext.MinPoint.Y < minY) minY = ext.MinPoint.Y;
                if (ext.MaxPoint.X > maxX) maxX = ext.MaxPoint.X;
                if (ext.MaxPoint.Y > maxY) maxY = ext.MaxPoint.Y;
            }
            double cx = (minX + maxX) / 2.0, cy = (minY + maxY) / 2.0;

            foreach (var id in ids)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null) continue;
                JObject? e = null;
                if (ent is Line l)
                {
                    e = new JObject {
                        ["type"] = "Line",
                        ["x1"] = l.StartPoint.X - cx, ["y1"] = l.StartPoint.Y - cy,
                        ["x2"] = l.EndPoint.X - cx,   ["y2"] = l.EndPoint.Y - cy,
                    };
                }
                else if (ent is Circle c)
                {
                    e = new JObject {
                        ["type"] = "Circle",
                        ["x"] = c.Center.X - cx, ["y"] = c.Center.Y - cy,
                        ["r"] = c.Radius,
                    };
                }
                else if (ent is Arc a)
                {
                    e = new JObject {
                        ["type"] = "Arc",
                        ["x"] = a.Center.X - cx, ["y"] = a.Center.Y - cy,
                        ["r"] = a.Radius,
                        ["start"] = a.StartAngle, ["end"] = a.EndAngle,
                    };
                }
                else if (ent is Autodesk.AutoCAD.DatabaseServices.Polyline pl)
                {
                    // Polyline 2D (LWPolyline, comando RECTANG o PLINE).
                    // Salviamo i vertici come array di {x, y, bulge}: il bulge >0
                    // significa arco (mezza-tangente = tan(angolo/4)), evita di
                    // perdere informazione su rettangoli arrotondati / archi inseriti.
                    var verts = new JArray();
                    for (int i = 0; i < pl.NumberOfVertices; i++)
                    {
                        var p = pl.GetPoint2dAt(i);
                        verts.Add(new JObject {
                            ["x"] = p.X - cx,
                            ["y"] = p.Y - cy,
                            ["bulge"] = pl.GetBulgeAt(i),
                        });
                    }
                    e = new JObject {
                        ["type"] = "Polyline",
                        ["closed"] = pl.Closed,
                        ["vertices"] = verts,
                    };
                }
                if (e != null) entities.Add(e);
            }
            tr.Commit();

            return new JObject {
                ["entities"] = entities,
                ["bbox_w"] = maxX - minX,
                ["bbox_h"] = maxY - minY,
                ["count"] = entities.Count,
            };
        }

        /// <summary>Salva un nuovo simbolo su Supabase.</summary>
        public static async Task<bool> SalvaSimbolo(string nome, string categoria, JObject geometria, string layerNome)
        {
            try
            {
                var payload = new JObject {
                    ["nome"] = nome,
                    ["categoria"] = categoria,
                    ["geometria"] = geometria,
                    ["layer_nome"] = layerNome,
                    ["bbox_w_cm"] = geometria["bbox_w"]?.Value<double>() ?? 0,
                    ["bbox_h_cm"] = geometria["bbox_h"]?.Value<double>() ?? 0,
                    ["num_entities"] = geometria["count"]?.Value<int>() ?? 0,
                };
                await SupabaseClient.Insert("mary_simboli", payload);
                return true;
            }
            catch (System.Exception ex)
            {
                Logger.Log("SalvaSimbolo: " + ex.Message);
                return false;
            }
        }

        /// <summary>Carica TUTTI i simboli dalla libreria.</summary>
        public static async Task<JArray> CaricaSimboli()
        {
            try { return await SupabaseClient.Select("mary_simboli", "order=categoria,nome"); }
            catch { return new JArray(); }
        }

        /// <summary>Inserisce un simbolo a una coordinata data (replicando la geometria salvata).</summary>
        public static void InserisciSimbolo(JObject simbolo, Point3d pos, double rotazione = 0)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var db = doc.Database;

            var layerNome = (string?)simbolo["layer_nome"] ?? "ANG_GENERICO";
            LayerManager.GetOrCreateLayer(db, layerNome);

            var entities = simbolo["geometria"]?["entities"] as JArray ?? new JArray();

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                foreach (JObject e in entities)
                {
                    Entity? ent = null;
                    var type = (string?)e["type"];
                    if (type == "Line")
                    {
                        var p1 = Rotate(new Point3d((double)e["x1"]!, (double)e["y1"]!, 0), rotazione) + pos.GetAsVector();
                        var p2 = Rotate(new Point3d((double)e["x2"]!, (double)e["y2"]!, 0), rotazione) + pos.GetAsVector();
                        ent = new Line(p1, p2);
                    }
                    else if (type == "Circle")
                    {
                        var c = Rotate(new Point3d((double)e["x"]!, (double)e["y"]!, 0), rotazione) + pos.GetAsVector();
                        ent = new Circle(c, Vector3d.ZAxis, (double)e["r"]!);
                    }
                    else if (type == "Arc")
                    {
                        var c = Rotate(new Point3d((double)e["x"]!, (double)e["y"]!, 0), rotazione) + pos.GetAsVector();
                        ent = new Arc(c, (double)e["r"]!, (double)e["start"]! + rotazione, (double)e["end"]! + rotazione);
                    }
                    else if (type == "Polyline")
                    {
                        var pl = new Autodesk.AutoCAD.DatabaseServices.Polyline();
                        var verts = e["vertices"] as JArray ?? new JArray();
                        for (int i = 0; i < verts.Count; i++)
                        {
                            var v = (JObject)verts[i];
                            var p3 = Rotate(new Point3d((double)v["x"]!, (double)v["y"]!, 0), rotazione) + pos.GetAsVector();
                            pl.AddVertexAt(i, new Point2d(p3.X, p3.Y), (double?)v["bulge"] ?? 0.0, 0, 0);
                        }
                        pl.Closed = (bool?)e["closed"] ?? false;
                        ent = pl;
                    }
                    if (ent != null)
                    {
                        ent.Layer = layerNome.StartsWith("ANG_") ? layerNome : "ANG_" + layerNome;
                        btr.AppendEntity(ent);
                        tr.AddNewlyCreatedDBObject(ent, true);
                    }
                }
                tr.Commit();
            }
        }

        private static Point3d Rotate(Point3d p, double angle)
        {
            if (angle == 0) return p;
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            return new Point3d(p.X * cos - p.Y * sin, p.X * sin + p.Y * cos, p.Z);
        }
    }
}

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
                ConvertEntity(ent, cx, cy, entities);
            }
            tr.Commit();

            return new JObject {
                ["entities"] = entities,
                ["bbox_w"] = maxX - minX,
                ["bbox_h"] = maxY - minY,
                ["count"] = entities.Count,
            };
        }

        /// <summary>
        /// Converte una singola entità in un JObject e la appende ad `out`.
        /// Per BlockReference esplode (a livello logico, copia in memoria) e
        /// ricorre sui figli — così frecce, blocchi-simbolo, riferimenti
        /// annidati diventano geometria piatta nel simbolo salvato.
        /// </summary>
        private static void ConvertEntity(Entity ent, double cx, double cy, JArray output)
        {
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
            else if (ent is DBText txt)
            {
                // TEXT singola riga: sigle, etichette, valori (es. 'KWh', 'F1', '16A').
                e = new JObject {
                    ["type"] = "Text",
                    ["x"] = txt.Position.X - cx,
                    ["y"] = txt.Position.Y - cy,
                    ["content"] = txt.TextString ?? "",
                    ["height"] = txt.Height,
                    ["rotation"] = txt.Rotation,
                };
            }
            else if (ent is MText mtxt)
            {
                // MText: prendiamo solo il testo "raw" (senza i codici di formattazione
                // {\fXXX;}). Per i simboli quasi sempre basta — etichette brevi.
                var raw = mtxt.Contents ?? "";
                // Rimuove i codici di formattazione tipici (\\fArial; \\C1; \\L \\l ecc.)
                raw = System.Text.RegularExpressions.Regex.Replace(raw, @"\\[A-Za-z][^;]*;", "");
                raw = raw.Replace("\\P", "\n").Replace("{", "").Replace("}", "");
                e = new JObject {
                    ["type"] = "Text",
                    ["x"] = mtxt.Location.X - cx,
                    ["y"] = mtxt.Location.Y - cy,
                    ["content"] = raw,
                    ["height"] = mtxt.TextHeight,
                    ["rotation"] = mtxt.Rotation,
                };
            }
            else if (ent is BlockReference br)
            {
                // Esplode il blocco virtualmente: produce una collezione in-memory
                // di entità "child" non-database, già trasformate nelle coordinate
                // world del blocco. Le processiamo ricorsivamente.
                try
                {
                    var children = new DBObjectCollection();
                    br.Explode(children);
                    foreach (DBObject obj in children)
                    {
                        if (obj is Entity childEnt) ConvertEntity(childEnt, cx, cy, output);
                        obj.Dispose();
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.Log("ConvertEntity BlockReference: " + ex.Message);
                }
                return;  // nessun JObject diretto: i child sono già stati aggiunti
            }
            if (e != null) output.Add(e);
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
                    else if (type == "Text")
                    {
                        var p = Rotate(new Point3d((double)e["x"]!, (double)e["y"]!, 0), rotazione) + pos.GetAsVector();
                        var txt = new DBText
                        {
                            Position = p,
                            TextString = (string?)e["content"] ?? "",
                            Height = (double?)e["height"] ?? 2.5,
                            Rotation = ((double?)e["rotation"] ?? 0.0) + rotazione,
                        };
                        ent = txt;
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

        // ================================================================
        //  RENDER PREVIEW — converte la geometria salvata in una BitmapSource
        //  WPF da usare come icona nel bottone ribbon. Dimensione tipica 32x32
        //  per RibbonItemSize.Standard; ridimensionata automaticamente al bbox.
        //
        //  Coordinate AutoCAD: Y verso l'alto. WPF: Y verso il basso. Quindi
        //  applichiamo ScaleY = -scale per riflettere verticalmente.
        // ================================================================
        public static BitmapSource? RenderPreview(JObject simbolo, int size = 96)
        {
            try
            {
                var entities = simbolo["geometria"]?["entities"] as JArray;
                if (entities == null || entities.Count == 0)
                {
                    Logger.Log("RenderPreview: nessuna entità");
                    return null;
                }

                var bboxW = (double?)simbolo["geometria"]?["bbox_w"] ?? 1.0;
                var bboxH = (double?)simbolo["geometria"]?["bbox_h"] ?? 1.0;
                var maxDim = Math.Max(bboxW, bboxH);
                if (maxDim <= 0.0001) maxDim = 1.0;

                double padding = size * 0.10;
                double scale = (size - 2 * padding) / maxDim;

                Logger.Log($"RenderPreview '{(string?)simbolo["nome"]}': size={size}, bbox=({bboxW:F2}×{bboxH:F2}), maxDim={maxDim:F2}, scale={scale:F4}, entities={entities.Count}");

                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    // Pen spessore fisso a 2px nello spazio finale (canvas).
                    // Dato che il transform applica scale, lo spessore in unità
                    // del coordinate space del simbolo va diviso per scale.
                    double penWidth = 2.0 / Math.Max(scale, 0.0001);
                    var pen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.White, penWidth)
                    {
                        StartLineCap = PenLineCap.Round,
                        EndLineCap = PenLineCap.Round,
                        LineJoin = PenLineJoin.Round,
                    };
                    pen.Freeze();

                    // Trasformazione: centra in (size/2, size/2), scala, riflette Y.
                    var tg = new TransformGroup();
                    tg.Children.Add(new ScaleTransform(scale, -scale));
                    tg.Children.Add(new TranslateTransform(size / 2.0, size / 2.0));
                    dc.PushTransform(tg);

                    foreach (JObject e in entities)
                    {
                        var type = (string?)e["type"];
                        try
                        {
                            if (type == "Line")
                            {
                                dc.DrawLine(pen,
                                    new System.Windows.Point((double)e["x1"]!, (double)e["y1"]!),
                                    new System.Windows.Point((double)e["x2"]!, (double)e["y2"]!));
                            }
                            else if (type == "Circle")
                            {
                                dc.DrawEllipse(null, pen,
                                    new System.Windows.Point((double)e["x"]!, (double)e["y"]!),
                                    (double)e["r"]!, (double)e["r"]!);
                            }
                            else if (type == "Arc")
                            {
                                // Approssimazione: 12 segmenti
                                double cx = (double)e["x"]!, cy = (double)e["y"]!, r = (double)e["r"]!;
                                double a0 = (double)e["start"]!, a1 = (double)e["end"]!;
                                if (a1 < a0) a1 += 2 * Math.PI;
                                int seg = 12;
                                for (int i = 0; i < seg; i++)
                                {
                                    double t1 = a0 + (a1 - a0) * i / seg;
                                    double t2 = a0 + (a1 - a0) * (i + 1) / seg;
                                    dc.DrawLine(pen,
                                        new System.Windows.Point(cx + r * Math.Cos(t1), cy + r * Math.Sin(t1)),
                                        new System.Windows.Point(cx + r * Math.Cos(t2), cy + r * Math.Sin(t2)));
                                }
                            }
                            else if (type == "Polyline")
                            {
                                var verts = e["vertices"] as JArray;
                                if (verts != null && verts.Count >= 2)
                                {
                                    for (int i = 0; i < verts.Count - 1; i++)
                                    {
                                        var v1 = (JObject)verts[i]!;
                                        var v2 = (JObject)verts[i + 1]!;
                                        dc.DrawLine(pen,
                                            new System.Windows.Point((double)v1["x"]!, (double)v1["y"]!),
                                            new System.Windows.Point((double)v2["x"]!, (double)v2["y"]!));
                                    }
                                    if ((bool?)e["closed"] == true && verts.Count >= 3)
                                    {
                                        var vF = (JObject)verts[0]!;
                                        var vL = (JObject)verts[verts.Count - 1]!;
                                        dc.DrawLine(pen,
                                            new System.Windows.Point((double)vL["x"]!, (double)vL["y"]!),
                                            new System.Windows.Point((double)vF["x"]!, (double)vF["y"]!));
                                    }
                                }
                            }
                            else if (type == "Text")
                            {
                                // Render del testo. Lo disegniamo con scala già applicata
                                // dal TransformGroup esterno, quindi qui usiamo size in
                                // unità AutoCAD originali. FormattedText con FlowDirection
                                // standard, riflesso poi dal ScaleY negativo del transform.
                                var content = (string?)e["content"] ?? "";
                                if (string.IsNullOrEmpty(content)) continue;
                                var h = (double?)e["height"] ?? 2.5;
                                if (h <= 0) h = 2.5;
                                var ft = new FormattedText(
                                    content,
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    System.Windows.FlowDirection.LeftToRight,
                                    new Typeface("Arial"),
                                    h,
                                    System.Windows.Media.Brushes.White,
                                    1.0);
                                // Il transform esterno ha ScaleY=-scale, quindi il testo
                                // verrebbe disegnato a testa in giù. Compensiamo con un
                                // sub-transform locale che riflette di nuovo (così appare dritto).
                                dc.PushTransform(new ScaleTransform(1, -1,
                                    (double)e["x"]!, (double)e["y"]!));
                                dc.DrawText(ft, new System.Windows.Point(
                                    (double)e["x"]!, (double)e["y"]! - h));  // y - h: baseline AutoCAD
                                dc.Pop();
                            }
                        }
                        catch { /* salta entità malformata */ }
                    }
                    dc.Pop();
                }

                var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(dv);
                rtb.Freeze();
                return rtb;
            }
            catch (System.Exception ex)
            {
                Logger.Log("RenderPreview: " + ex.Message);
                return null;
            }
        }
    }
}

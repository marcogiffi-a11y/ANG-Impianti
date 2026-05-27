using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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
                // TEXT singola riga. In AutoCAD, se HorizontalMode != Left o
                // VerticalMode != Base, l'anchor visivo è AlignmentPoint, non
                // Position. Salviamo l'anchor effettivo + i flag di allineamento
                // così la ricostruzione in altri contesti è fedele.
                bool aligned = txt.HorizontalMode != TextHorizontalMode.TextLeft
                            || txt.VerticalMode != TextVerticalMode.TextBase;
                var anchor = aligned ? txt.AlignmentPoint : txt.Position;
                e = new JObject {
                    ["type"] = "Text",
                    ["x"] = anchor.X - cx,
                    ["y"] = anchor.Y - cy,
                    ["content"] = txt.TextString ?? "",
                    ["height"] = txt.Height,
                    ["rotation"] = txt.Rotation,
                    ["halign"] = (int)txt.HorizontalMode,  // 0=Left 1=Center 2=Right 3=Aligned 4=Middle 5=Fit
                    ["valign"] = (int)txt.VerticalMode,    // 0=Base 1=Bottom 2=Mid 3=Top
                };
            }
            else if (ent is MText mtxt)
            {
                // MText: prendiamo solo il testo "raw" (senza i codici di formattazione
                // {\fXXX;}). Per i simboli quasi sempre basta — etichette brevi.
                var raw = mtxt.Contents ?? "";
                raw = System.Text.RegularExpressions.Regex.Replace(raw, @"\\[A-Za-z][^;]*;", "");
                raw = raw.Replace("\\P", "\n").Replace("{", "").Replace("}", "");
                e = new JObject {
                    ["type"] = "Text",
                    ["x"] = mtxt.Location.X - cx,
                    ["y"] = mtxt.Location.Y - cy,
                    ["content"] = raw,
                    ["height"] = mtxt.TextHeight,
                    ["rotation"] = mtxt.Rotation,
                    ["attachment"] = (int)mtxt.Attachment,  // TopLeft=1 TopCenter=2 MiddleCenter=5 ecc
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
        public static async Task<bool> SalvaSimbolo(string nome, string categoria, JObject geometria, string layerNome, string? previewUrl = null)
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
                if (!string.IsNullOrEmpty(previewUrl)) payload["preview_url"] = previewUrl;
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
                        var halign = (int?)e["halign"] ?? 0;
                        var valign = (int?)e["valign"] ?? 0;
                        var txt = new DBText
                        {
                            TextString = (string?)e["content"] ?? "",
                            Height = (double?)e["height"] ?? 2.5,
                            Rotation = ((double?)e["rotation"] ?? 0.0) + rotazione,
                            HorizontalMode = (TextHorizontalMode)halign,
                            VerticalMode = (TextVerticalMode)valign,
                        };
                        // Se il testo è centrato, l'anchor visivo è AlignmentPoint.
                        // Position deve comunque essere assegnata (AutoCAD la usa
                        // come fallback) ma l'AlignmentPoint determina la posizione effettiva.
                        if (halign != 0 || valign != 0)
                        {
                            txt.Position = p;
                            txt.AlignmentPoint = p;
                        }
                        else
                        {
                            txt.Position = p;
                        }
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
        //  GENERATE THUMBNAIL — strategia "save & reload":
        //  1) Wblock le entità in un Database temporaneo
        //  2) SaveAs su file .dwg → AutoCAD genera la ThumbnailBitmap auto
        //  3) ReadDwgFile da un nuovo Database → leggiamo Thumbnail
        //  4) Cleanup file temp
        //
        //  Tentato prima BlockTableRecord.PreviewIcon: ritorna null per
        //  blocchi appena creati in sessione (AutoCAD genera le preview
        //  solo al save del DWG). Quindi usiamo questo trick documentato.
        // ================================================================
        public static byte[]? GenerateThumbnail(IEnumerable<ObjectId> ids, Database db)
        {
            var tmpFile = Path.Combine(Path.GetTempPath(),
                "ang_preview_" + Guid.NewGuid().ToString("N") + ".dwg");
            Bitmap? thumb = null;
            object? oldThumbsave = null;

            try
            {
                Logger.Log("GenerateThumbnail: START, tmpFile=" + tmpFile);

                // Force DWGTHUMBSAVE=1: senza questo AutoCAD può saltare
                // la generazione della thumbnail nei save automatici.
                try
                {
                    oldThumbsave = Application.GetSystemVariable("DWGTHUMBSAVE");
                    Application.SetSystemVariable("DWGTHUMBSAVE", (short)1);
                    Logger.Log("GenerateThumbnail: DWGTHUMBSAVE was=" + oldThumbsave + ", now=1");
                }
                catch (System.Exception ex) { Logger.Log("DWGTHUMBSAVE setup: " + ex.Message); }

                // 1+2: wblock e salva su disco
                var idsList = ids.ToList();
                Logger.Log($"GenerateThumbnail: wblock di {idsList.Count} entità");
                using (var wDb = db.Wblock(new ObjectIdCollection(idsList.ToArray()), Point3d.Origin))
                {
                    Logger.Log("GenerateThumbnail: wblock OK, SaveAs...");
                    wDb.SaveAs(tmpFile, DwgVersion.Current);
                    var sz = new FileInfo(tmpFile).Length;
                    Logger.Log($"GenerateThumbnail: file salvato {sz} bytes");
                }

                // 3: riapri il file e leggi ThumbnailBitmap
                using (var rDb = new Database(false, true))
                {
                    rDb.ReadDwgFile(tmpFile, FileShare.ReadWrite, false, null);
                    if (rDb.ThumbnailBitmap != null)
                    {
                        thumb = new Bitmap(rDb.ThumbnailBitmap);
                        Logger.Log($"GenerateThumbnail: ThumbnailBitmap OK {thumb.Width}×{thumb.Height}");
                    }
                    else
                    {
                        Logger.Log("GenerateThumbnail: ThumbnailBitmap STILL NULL after reload");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.Log("GenerateThumbnail wblock+save EXCEPTION: " + ex.GetType().Name + " " + ex.Message);
            }
            finally
            {
                // Ripristina DWGTHUMBSAVE
                try
                {
                    if (oldThumbsave != null)
                        Application.SetSystemVariable("DWGTHUMBSAVE", oldThumbsave);
                }
                catch { }
                try { if (File.Exists(tmpFile)) File.Delete(tmpFile); } catch { }
            }

            if (thumb == null) return null;

            try
            {
                using var ms = new MemoryStream();
                thumb.Save(ms, ImageFormat.Png);
                var bytes = ms.ToArray();
                Logger.Log($"GenerateThumbnail: PNG encoded {bytes.Length} bytes");
                thumb.Dispose();
                return bytes;
            }
            catch (System.Exception ex)
            {
                Logger.Log("GenerateThumbnail PNG encode: " + ex.Message);
                return null;
            }
        }


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
                    // Pen sottile per leggibilità su simboli con molti dettagli.
                    // 1.0px nel canvas finale: nelle unità AutoCAD = 1.0/scale.
                    double penWidth = 1.0 / Math.Max(scale, 0.0001);
                    var pen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.White, penWidth)
                    {
                        StartLineCap = PenLineCap.Round,
                        EndLineCap = PenLineCap.Round,
                        LineJoin = PenLineJoin.Miter,
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
                                // Render del testo centrato sul suo anchor point.
                                // Default: centro orizzontale + verticale (caso più comune
                                // nei simboli — "KWh", "F1", etc dentro un riquadro).
                                // Se halign/valign salvati: rispetta esattamente.
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

                                // Determina allineamento:
                                // - DBText: halign (0=Left, 1=Center, 2=Right)
                                //           valign (0=Base, 1=Bot, 2=Mid, 3=Top)
                                // - MText:  attachment (1=TL 2=TC 3=TR 4=ML 5=MC 6=MR 7=BL 8=BC 9=BR)
                                // Default per simboli: centrato MC.
                                int halign = (int?)e["halign"] ?? -1;
                                int valign = (int?)e["valign"] ?? -1;
                                int attach = (int?)e["attachment"] ?? -1;

                                bool hCenter = true, hRight = false;
                                bool vCenter = true, vTop = false;
                                if (halign >= 0)
                                {
                                    hCenter = halign == 1 || halign == 4;
                                    hRight = halign == 2;
                                }
                                if (valign >= 0)
                                {
                                    vCenter = valign == 2;
                                    vTop = valign == 3;
                                }
                                if (attach >= 0)
                                {
                                    hCenter = attach == 2 || attach == 5 || attach == 8;
                                    hRight = attach == 3 || attach == 6 || attach == 9;
                                    vTop = attach >= 1 && attach <= 3;
                                    vCenter = attach >= 4 && attach <= 6;
                                }

                                ft.TextAlignment = hCenter ? System.Windows.TextAlignment.Center
                                                : hRight  ? System.Windows.TextAlignment.Right
                                                          : System.Windows.TextAlignment.Left;

                                double ax = (double)e["x"]!, ay = (double)e["y"]!;
                                // Offset Y per allineamento verticale (in unità AutoCAD,
                                // dove Y cresce verso l'alto). FormattedText disegna
                                // dall'alto verso il basso, quindi:
                                //  - vTop:    il punto e' alto del testo  -> origin.y = ay
                                //  - vCenter: il punto e' centro          -> origin.y = ay - h/2
                                //  - vBottom: il punto e' baseline/bottom -> origin.y = ay - h
                                double oy = vTop ? ay : vCenter ? ay - h * 0.5 : ay - h;

                                // Riflettiamo Y attorno all'anchor per compensare il
                                // ScaleY=-scale esterno (testo dritto, non specchiato).
                                dc.PushTransform(new ScaleTransform(1, -1, ax, ay));
                                dc.DrawText(ft, new System.Windows.Point(ax, oy));
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

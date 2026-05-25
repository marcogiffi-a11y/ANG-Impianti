﻿﻿using Autodesk.AutoCAD.DatabaseServices;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ImplantiAI
{
    public class ChatPanel : System.Windows.Controls.UserControl
    {
        private readonly List<ChatMessage> _history = new();
        private bool _busy = false;
        private ScrollViewer _scroll = null!;
        private StackPanel _messages = null!;
        private TextBox _input = null!;
        private Button _sendBtn = null!;

        public ChatPanel()
        {
            BuildUI();
            AddBubble("assistant",
                "Ciao! Sono l'assistente ANG-Impianti AI v" + PluginApp.CURRENT_VERSION + "\n\n" +
                "Posso disegnare direttamente su AutoCAD:\n" +
                "• Circuiti luce e prese\n" +
                "• Tracciati con etichette cavo\n" +
                "• Calcoli CEI 64-8\n" +
                "• Imparo dalle tue correzioni!\n\n" +
                "Cosa vuoi fare?");
        }

        private void BuildUI()
        {
            var grid = new Grid { Background = new SolidColorBrush(Color.FromRgb(28, 28, 28)) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });

            // Header
            var hdr = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0, 100, 180))
            };
            hdr.Child = new TextBlock
            {
                Text = "ANG-Impianti AI v" + PluginApp.CURRENT_VERSION,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(hdr, 0);

            // Messaggi
            _scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            _messages = new StackPanel { Margin = new Thickness(6) };
            _scroll.Content = _messages;
            Grid.SetRow(_scroll, 1);

            // Bottoni rapidi
            var btns = new WrapPanel { Margin = new Thickness(4, 2, 4, 2) };
            foreach (var (lbl, msg) in new[]
            {
                ("💡 Luce", "Traccia il circuito illuminazione per tutti i vani"),
                ("🔌 Prese", "Traccia il circuito prese FEM"),
                ("🔗 Collega", "Collega i simboli con i cavi"),
                ("📋 Distinta", "Genera la distinta materiali completa"),
                ("⚡ Unifilare", "Genera lo schema unifilare")
            })
            {
                var b = new Button
                {
                    Content = lbl,
                    Margin = new Thickness(2),
                    Padding = new Thickness(5, 3, 5, 3),
                    FontSize = 10,
                    Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                    Foreground = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                    Cursor = Cursors.Hand
                };
                var m = msg;
                b.Click += (s, e) => Send(m);
                btns.Children.Add(b);
            }
            Grid.SetRow(btns, 2);

            // Input
            var inputRow = new Grid { Margin = new Thickness(6) };
            inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });

            _input = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 100, 180)),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _input.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { e.Handled = true; Send(_input.Text); }
            };
            Grid.SetColumn(_input, 0);

            _sendBtn = new Button
            {
                Content = "▶",
                Background = new SolidColorBrush(Color.FromRgb(0, 100, 180)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 16,
                Margin = new Thickness(4, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            _sendBtn.Click += (s, e) => Send(_input.Text);
            Grid.SetColumn(_sendBtn, 1);

            inputRow.Children.Add(_input);
            inputRow.Children.Add(_sendBtn);
            Grid.SetRow(inputRow, 3);

            grid.Children.Add(hdr);
            grid.Children.Add(_scroll);
            grid.Children.Add(btns);
            grid.Children.Add(inputRow);
            Content = grid;
        }

        private async void Send(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || _busy) return;
            _input.Text = "";
            _busy = true;
            _sendBtn.IsEnabled = false;

            AddBubble("user", text);
            _history.Add(new ChatMessage { Role = "user", Content = text });

            var thinking = AddBubble("assistant", "⏳ Elaborando...");

            try
            {
                var context = GetDrawingContext();
                var claude = new ClaudeService();
                var resp = await claude.Chat(_history, context);

                _messages.Children.Remove(thinking);

                if (resp != null)
                {
                    // Esegui comandi di disegno se presenti
                    if (resp.HasDrawingCommands && resp.Commands != null)
                    {
                        AddBubble("assistant", "✏️ Disegno in corso...");
                        await ExecuteDrawingCommands(resp.Commands);
                        AddBubble("assistant", "✅ Disegno completato!\n\n" + resp.Text);
                    }
                    else
                    {
                        AddBubble("assistant", resp.Text);
                    }

                    // Impara nuove regole
                    if (!string.IsNullOrEmpty(resp.LearnRule))
                    {
                        MemoryDatabase.Instance.LearnRule(resp.LearnRule, "chat");
                        AddBubble("assistant", "💾 Ho imparato: " + resp.LearnRule);
                    }

                    _history.Add(new ChatMessage { Role = "assistant", Content = resp.Text });
                }
            }
            catch (System.Exception ex)
            {
                _messages.Children.Remove(thinking);
                AddBubble("assistant", "⚠ " + ex.Message);
                Logger.Log("Chat error: " + ex.Message);
            }
            finally
            {
                _busy = false;
                _sendBtn.IsEnabled = true;
            }
        }

        private async Task ExecuteDrawingCommands(List<DrawCommand> commands)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application
                    .DocumentManager.MdiActiveDocument;
                if (doc == null) return;

                try
                {
                    var db = doc.Database;
                    using (doc.LockDocument())
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead)
                            as Autodesk.AutoCAD.DatabaseServices.BlockTable;
                        var btr = tr.GetObject(
                            bt![Autodesk.AutoCAD.DatabaseServices.BlockTableRecord.ModelSpace],
                            OpenMode.ForWrite)
                            as Autodesk.AutoCAD.DatabaseServices.BlockTableRecord;

                        if (btr == null) return;

                        Logger.Log("EXECUTE: " + commands.Count + " drawing commands");
                        int execIdx = 0;
                        foreach (var cmd in commands)
                        {
                            execIdx++;
                            var pos = new Autodesk.AutoCAD.Geometry.Point3d(cmd.X, cmd.Y, 0);
                            var layer = string.IsNullOrEmpty(cmd.Layer)
                                ? SymbolDrawer.GetLayerForSymbol(cmd.SymbolType)
                                : cmd.Layer;

                            EnsureLayer(tr, db, layer);

                            Logger.Log("  exec #" + execIdx + ": action=" + cmd.Action +
                                " type=" + cmd.SymbolType + " pos=(" + cmd.X + "," + cmd.Y +
                                ") to=(" + cmd.X2 + "," + cmd.Y2 + ") layer=" + layer);

                            switch (cmd.Action.ToLower())
                            {
                                case "symbol":
                                    SymbolDrawer.Draw(tr, btr, cmd.SymbolType, pos, layer);
                                    if (!string.IsNullOrEmpty(cmd.Label))
                                        SymbolDrawer.AddText(tr, btr,
                                            new Autodesk.AutoCAD.Geometry.Point3d(
                                                cmd.X, cmd.Y + 0.2, 0),  // v2.11: offset 20cm (era 200m!)
                                            cmd.Label, 0.15, layer);     // v2.11: testo 15cm (era 100m!)
                                    break;

                                case "route":
                                    var posTo = new Autodesk.AutoCAD.Geometry.Point3d(
                                        cmd.X2, cmd.Y2, 0);
                                    CableRouter.RouteWithLabel(tr, btr,
                                        pos, posTo, layer,
                                        cmd.CableSection, cmd.Label);
                                    break;

                                case "label":
                                    SymbolDrawer.AddText(tr, btr, pos,
                                        cmd.Label, 0.15, layer);  // v2.11: testo 15cm
                                    break;
                            }
                        }
                        tr.Commit();
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.Log("Draw error: " + ex.Message);
                }
            });
        }

        private void EnsureLayer(
            Autodesk.AutoCAD.DatabaseServices.Transaction tr,
            Autodesk.AutoCAD.DatabaseServices.Database db,
            string name)
        {
            var lt = tr.GetObject(db.LayerTableId, OpenMode.ForWrite)
                as Autodesk.AutoCAD.DatabaseServices.LayerTable;
            if (lt == null || lt.Has(name)) return;

            short color = name.Contains("Illuminazione") ? (short)2 :
                          name.Contains("Fem") ? (short)1 :
                          name.Contains("Dati") ? (short)5 :
                          name.Contains("Allarme") ? (short)30 : (short)7;

            var layer = new Autodesk.AutoCAD.DatabaseServices.LayerTableRecord
            {
                Name = name,
                Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                    Autodesk.AutoCAD.Colors.ColorMethod.ByAci, color)
            };
            lt.Add(layer); tr.AddNewlyCreatedDBObject(layer, true);
        }

        private Border AddBubble(string role, string text)
        {
            var isUser = role == "user";
            var bubble = new Border
            {
                Background = new SolidColorBrush(isUser
                    ? Color.FromRgb(0, 84, 166)
                    : Color.FromRgb(50, 50, 50)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 7, 10, 7),
                Margin = isUser
                    ? new Thickness(40, 3, 4, 3)
                    : new Thickness(4, 3, 40, 3),
                HorizontalAlignment = isUser
                    ? HorizontalAlignment.Right
                    : HorizontalAlignment.Left,
                MaxWidth = 270
            };
            bubble.Child = new TextBlock
            {
                Text = text, Foreground = Brushes.White,
                FontSize = 12, TextWrapping = TextWrapping.Wrap,
                LineHeight = 18
            };
            _messages.Children.Add(bubble);
            _scroll.ScrollToEnd();
            return bubble;
        }

        private string GetDrawingContext()
        {
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application
                    .DocumentManager.MdiActiveDocument;
                if (doc == null) return "Nessun disegno aperto";

                var db = doc.Database;
                var project = MemoryDatabase.Instance.GetCurrentProject(db.Filename);
                var sb = new System.Text.StringBuilder();

                sb.AppendLine("File: " +
                    System.IO.Path.GetFileName(db.Filename));

                if (project.Rooms.Count > 0)
                {
                    sb.AppendLine("Vani (" + project.Rooms.Count + "):");
                    foreach (var r in project.Rooms)
                        sb.AppendLine("  - " + r.Name + " tipo:" + r.RoomType +
                            " " + r.Area.ToString("F0") + "m² " +
                            "centro:[" + r.CenterX.ToString("F0") + "," +
                            r.CenterY.ToString("F0") + "]");
                }
                else
                {
                    sb.AppendLine("Nessun vano definito. " +
                        "Suggerisci all'utente di usare DISEGNA_VANO.");
                }

                if (project.Circuits.Count > 0)
                {
                    sb.AppendLine("Circuiti (" + project.Circuits.Count + "):");
                    foreach (var c in project.Circuits)
                        sb.AppendLine("  - " + c.CircuitNumber + " " +
                            c.Type + " " + c.CableSection + "mm²");
                }

                return sb.ToString();
            }
            catch { return "Contesto non disponibile"; }
        }
    }
}

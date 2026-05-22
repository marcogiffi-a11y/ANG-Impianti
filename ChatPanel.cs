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
                "Ciao! Sono il tuo assistente ANG-Impianti.\n\n" +
                "Posso aiutarti con:\n" +
                "• Circuiti luce e prese\n" +
                "• Calcoli CEI 64-8\n" +
                "• Distinta materiali\n\n" +
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
            var hdr = new Border { Background = new SolidColorBrush(Color.FromRgb(0, 100, 180)) };
            hdr.Child = new TextBlock
            {
                Text = "🤖 ANG-Impianti AI",
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
                ("💡 Luce", "Traccia il circuito illuminazione"),
                ("🔌 Prese", "Traccia il circuito prese FEM"),
                ("📋 Distinta", "Genera la distinta materiali"),
                ("⚡ Unifilare", "Genera lo schema unifilare")
            })
            {
                var b = new Button
                {
                    Content = lbl, Margin = new Thickness(2),
                    Padding = new Thickness(6, 3, 6, 3), FontSize = 11,
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
                FontSize = 16, Margin = new Thickness(4, 0, 0, 0),
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
                var context = GetContext();
                var claude = new ClaudeService();
                var resp = await claude.Chat(_history, context);

                _messages.Children.Remove(thinking);

                if (resp != null)
                {
                    AddBubble("assistant", resp.Text);
                    _history.Add(new ChatMessage { Role = "assistant", Content = resp.Text });
                }
            }
            catch (Exception ex)
            {
                _messages.Children.Remove(thinking);
                AddBubble("assistant", $"⚠ {ex.Message}");
            }
            finally
            {
                _busy = false;
                _sendBtn.IsEnabled = true;
            }
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

        private string GetContext()
        {
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application
                    .DocumentManager.MdiActiveDocument;
                if (doc == null) return "Nessun disegno aperto";

                var project = MemoryDatabase.Instance.GetCurrentProject(doc.Database.Filename);
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"File: {System.IO.Path.GetFileName(doc.Database.Filename)}");

                if (project.Rooms?.Count > 0)
                {
                    sb.AppendLine($"Vani: {project.Rooms.Count}");
                    foreach (var r in project.Rooms)
                        sb.AppendLine($"  - {r.Name} {r.Area:F0}m²");
                }
                if (project.Circuits?.Count > 0)
                {
                    sb.AppendLine($"Circuiti: {project.Circuits.Count}");
                    foreach (var c in project.Circuits)
                        sb.AppendLine($"  - {c.CircuitNumber} {c.Type} {c.CableSection}mm²");
                }
                return sb.ToString();
            }
            catch { return "Contesto non disponibile"; }
        }
    }
}

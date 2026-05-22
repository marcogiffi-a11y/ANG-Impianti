using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using System;
using System.Drawing;
using System.Windows.Forms.Integration;

[assembly: CommandClass(typeof(ImplantiAI.Commands))]
[assembly: ExtensionApplication(typeof(ImplantiAI.PluginApp))]

namespace ImplantiAI
{
    public class PluginApp : IExtensionApplication
    {
        public static PaletteSet? Palette { get; private set; }
        public static ChatPanel? Chat { get; private set; }

        public void Initialize()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application
                .DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            try
            {
                MemoryDatabase.Instance.Initialize();

                // Crea palette chat
                Palette = new PaletteSet("ANG-Impianti AI",
                    new Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890"));
                Chat = new ChatPanel();
                var host = new ElementHost
                {
                    Child = Chat,
                    Dock = System.Windows.Forms.DockStyle.Fill
                };
                Palette.Add("Chat AI", host);
                Palette.Style = PaletteSetStyles.ShowCloseButton |
                                PaletteSetStyles.ShowAutoHideButton |
                                PaletteSetStyles.Snappable;
                Palette.MinimumSize = new Size(300, 400);
                Palette.Size = new Size(350, 600);
                Palette.DockEnabled = DockSides.Left | DockSides.Right;
                Palette.Dock = DockSides.Right;
                Palette.Visible = true;

                // Crea ribbon
                Autodesk.AutoCAD.ApplicationServices.Application.Idle += OnIdle;

                doc.Editor.WriteMessage(
                    "\n╔══════════════════════════╗\n" +
                    "║  ANG-Impianti AI v2.0    ║\n" +
                    "║  Ribbon e Chat pronti!   ║\n" +
                    "╚══════════════════════════╝\n");
            }
            catch (System.Exception ex)
            {
                doc?.Editor.WriteMessage($"\n✗ Errore avvio: {ex.Message}\n");
            }
        }

        private void OnIdle(object? sender, EventArgs e)
        {
            Autodesk.AutoCAD.ApplicationServices.Application.Idle -= OnIdle;
            try { RibbonManager.CreateRibbon(); } catch { }
        }

        public void Terminate() => MemoryDatabase.Instance.Save();
    }
}

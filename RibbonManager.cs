using Autodesk.Windows;
using System;

namespace ImplantiAI
{
    public static class RibbonManager
    {
        public static void CreateRibbon()
        {
            var rc = ComponentManager.Ribbon;
            if (rc == null) return;

            // Rimuovi tab esistente
            RibbonTab? old = null;
            foreach (var t in rc.Tabs)
                if (t.Id == "ANG_TAB") { old = t; break; }
            if (old != null) rc.Tabs.Remove(old);

            var tab = new RibbonTab { Title = "ANG-Impianti AI", Id = "ANG_TAB" };

            // VANI
            tab.Panels.Add(MkPanel("Vani",
                MkBig("Disegna\nVano", "DISEGNA_VANO"),
                MkBig("Riconosci\nVani", "RICONOSCI_VANI")));

            // CHAT
            tab.Panels.Add(MkPanel("AI",
                MkBig("Chat\nAI", "APRI_CHAT")));

            // ILLUMINAZIONE
            tab.Panels.Add(MkPanel("Illuminazione",
                MkSm("Corpo soffitto", "INS_LUCE_SOFFITTO"),
                MkSm("Corpo parete", "INS_LUCE_PARETE"),
                MkSm("Emergenza", "INS_LUCE_EMERGENZA"),
                new RibbonSeparator(),
                MkSm("Interruttore 1P", "INS_INT_1P"),
                MkSm("Interruttore 2P", "INS_INT_2P"),
                MkSm("Pulsante", "INS_PULSANTE"),
                MkSm("Doppio Pulsante", "INS_DOPPIO_PULSANTE")));

            // PRESE
            tab.Panels.Add(MkPanel("Prese",
                MkSm("Presa Universale", "INS_PRESA_UNIV"),
                MkSm("Presa Comandata", "INS_PRESA_CMD"),
                MkSm("Presa TV", "INS_PRESA_TV"),
                MkSm("Presa SAT", "INS_PRESA_SAT"),
                new RibbonSeparator(),
                MkSm("Scatola FEM", "INS_SCATOLA_FEM"),
                MkSm("Scatola Luce", "INS_SCATOLA_LUCE")));

            // SPECIALI
            tab.Panels.Add(MkPanel("Speciali",
                MkSm("Videocitofono Int.", "INS_VIDEOCIT_INT"),
                MkSm("Videocitofono Est.", "INS_VIDEOCIT_EST"),
                MkSm("Suoneria", "INS_SUONERIA"),
                MkSm("Ventilatore", "INS_VENTILATORE"),
                new RibbonSeparator(),
                MkSm("Rivelatore GAS", "INS_RIV_GAS"),
                MkSm("Rivelatore H2O", "INS_RIV_H2O"),
                MkSm("Cronotermostato", "INS_CRONOTERM")));

            // CIRCUITI
            tab.Panels.Add(MkPanel("Circuiti",
                MkBig("Distinta\nMateriali", "GENERA_DISTINTA")));

            rc.Tabs.Add(tab);
            tab.IsActive = true;
        }

        private static RibbonPanel MkPanel(string title, params RibbonItem[] items)
        {
            var p = new RibbonPanel { Source = new RibbonPanelSource { Title = title } };
            foreach (var item in items) p.Source.Items.Add(item);
            return p;
        }

        private static RibbonButton MkBig(string text, string cmd) => new RibbonButton
        {
            Text = text, Size = RibbonItemSize.Large, ShowText = true,
            Orientation = System.Windows.Controls.Orientation.Vertical,
            CommandHandler = new RibbonCmd(cmd)
        };

        private static RibbonButton MkSm(string text, string cmd) => new RibbonButton
        {
            Text = text, Size = RibbonItemSize.Standard, ShowText = true,
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            CommandHandler = new RibbonCmd(cmd)
        };
    }

    public class RibbonCmd : System.Windows.Input.ICommand
    {
        private readonly string _cmd;
        public RibbonCmd(string cmd) { _cmd = cmd; }
        public bool CanExecute(object? p) => true;
        public void Execute(object? p)
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application
                .DocumentManager.MdiActiveDocument;
            doc?.SendStringToExecute(_cmd + "\n", true, false, false);
        }
        public event EventHandler? CanExecuteChanged;
    }
}

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;

namespace ImplantiAI
{
    // ====================================================================
    //  SymbolDrawer v2.6
    //  Simboli fedeli alla legenda F-05 Athena Next Gen Impianto Elettrico.
    //  Tutte le geometrie sono estratte direttamente dal DXF di riferimento
    //  e centrate sull'origine del simbolo (pos = punto di inserimento).
    //  Unita: 1 unita AutoCAD = 1 metro (scala 1:1).
    // ====================================================================
    public static class SymbolDrawer
    {
        private const double DEG = Math.PI / 180.0;
        private static Point3d P(double x, double y) => new Point3d(x, y, 0);

        // --- Dispatch principale ---------------------------------------
        public static void Draw(Transaction tr, BlockTableRecord btr,
            string symbolType, Point3d pos, string layer)
        {
            switch ((symbolType ?? "").ToLowerInvariant())
            {
                case "centralino": DrawPanel(tr, btr, pos, layer); break;
                case "contatore": DrawEnergyMeter(tr, btr, pos, layer); break;
                case "scatola_fem": DrawJunctionBoxFEM(tr, btr, pos, layer); break;
                case "scatola_luce": DrawJunctionBoxIll(tr, btr, pos, layer); break;
                case "scatola_all": DrawJunctionBoxAll(tr, btr, pos, layer); break;
                case "pozzetto_terra": DrawGroundPit(tr, btr, pos, layer); break;
                case "videocit_int": DrawVideophoneInt(tr, btr, pos, layer); break;
                case "videocit_est": DrawVideophoneExt(tr, btr, pos, layer); break;
                case "ventilatore": DrawFan(tr, btr, pos, layer); break;
                case "emergenza_inc": DrawEmergencyInc(tr, btr, pos, layer); break;
                case "suoneria": DrawBell(tr, btr, pos, layer); break;
                case "pulsante": DrawButton(tr, btr, pos, layer); break;
                case "interruttore_1p": DrawSwitch1P(tr, btr, pos, layer); break;
                case "pulsante_doppio": DrawDoubleButton(tr, btr, pos, layer); break;
                case "interruttore_2p": DrawSwitch2P(tr, btr, pos, layer); break;
                case "pulsante_targh": DrawButtonPlate(tr, btr, pos, layer); break;
                case "presa_univ": DrawSocketUniv(tr, btr, pos, layer); break;
                case "bipresa": DrawDoubleSocket(tr, btr, pos, layer); break;
                case "predisp_6prese": DrawSocket6Prep(tr, btr, pos, layer); break;
                case "presa_cmd": DrawSocketCmd(tr, btr, pos, layer); break;
                case "presa_tv": DrawSocketTV(tr, btr, pos, layer); break;
                case "presa_sat": DrawSocketSAT(tr, btr, pos, layer); break;
                case "luce_soffitto": DrawLightCeiling(tr, btr, pos, layer); break;
                case "luce_parete": DrawLightWall(tr, btr, pos, layer); break;
                case "passafilo": DrawCableGland(tr, btr, pos, layer); break;
                case "var_quota": DrawHeightChange(tr, btr, pos, layer); break;
                case "riv_gas": DrawDetectorGas(tr, btr, pos, layer); break;
                case "riv_acqua": DrawDetectorWater(tr, btr, pos, layer); break;
                case "elettrovalv": DrawSolenoidValve(tr, btr, pos, layer); break;
                case "elettrovalv_no": DrawSolenoidValveNO(tr, btr, pos, layer); break;
                case "elettrovalv_nc": DrawSolenoidValveNC(tr, btr, pos, layer); break;
                case "cronoterm": DrawThermostat(tr, btr, pos, layer); break;
                case "inseritore_all": DrawAlarmInserter(tr, btr, pos, layer); break;
                case "centrale_all": DrawAlarmPanel(tr, btr, pos, layer); break;
                case "sirena_est": DrawSirenExt(tr, btr, pos, layer); break;
                case "sirena_int": DrawSirenInt(tr, btr, pos, layer); break;
                case "contatto_mag": DrawMagContact(tr, btr, pos, layer); break;
                case "sensore_ir": DrawIRSensor(tr, btr, pos, layer); break;
                default: DrawGeneric(tr, btr, pos, layer, symbolType); break;
            }
        }

        // --- Layer di default per ciascun simbolo ----------------------
        public static string GetLayerForSymbol(string symbolType)
        {
            switch ((symbolType ?? "").ToLowerInvariant())
            {
                case "centralino": return "Impianto Elettrico Fem";
                case "contatore": return "Impianto Elettrico Fem";
                case "scatola_fem": return "Impianto Elettrico Fem";
                case "scatola_luce": return "Impianto Elettrico Illuminazione";
                case "scatola_all": return "Impianto Elettrico Allarme";
                case "pozzetto_terra": return "Impianto Elettrico Terra";
                case "videocit_int": return "Impianto Elettrico Dati";
                case "videocit_est": return "Impianto Elettrico Dati";
                case "ventilatore": return "Impianto Elettrico Fem";
                case "emergenza_inc": return "Impianto Elettrico Illuminazione";
                case "suoneria": return "Impianto Elettrico Dati";
                case "pulsante": return "Impianto Elettrico Illuminazione";
                case "interruttore_1p": return "Impianto Elettrico Illuminazione";
                case "pulsante_doppio": return "Impianto Elettrico Illuminazione";
                case "interruttore_2p": return "Impianto Elettrico Illuminazione";
                case "pulsante_targh": return "Impianto Elettrico Dati";
                case "presa_univ": return "Impianto Elettrico Fem";
                case "bipresa": return "Impianto Elettrico Fem";
                case "predisp_6prese": return "Impianto Elettrico Fem";
                case "presa_cmd": return "Impianto Elettrico Fem";
                case "presa_tv": return "Impianto Elettrico Dati";
                case "presa_sat": return "Impianto Elettrico Dati";
                case "luce_soffitto": return "Impianto Elettrico Illuminazione";
                case "luce_parete": return "Impianto Elettrico Illuminazione";
                case "passafilo": return "Impianto Elettrico Fem";
                case "var_quota": return "Impianto Elettrico Fem";
                case "riv_gas": return "Impianto Elettrico Allarme";
                case "riv_acqua": return "Impianto Elettrico Allarme";
                case "elettrovalv": return "Impianto Elettrico Allarme";
                case "elettrovalv_no": return "Impianto Elettrico Allarme";
                case "elettrovalv_nc": return "Impianto Elettrico Allarme";
                case "cronoterm": return "Impianto Elettrico Dati";
                case "inseritore_all": return "Impianto Elettrico Allarme";
                case "centrale_all": return "Impianto Elettrico Allarme";
                case "sirena_est": return "Impianto Elettrico Allarme";
                case "sirena_int": return "Impianto Elettrico Allarme";
                case "contatto_mag": return "Impianto Elettrico Allarme";
                case "sensore_ir": return "Impianto Elettrico Allarme";
                default: return "Impianto Elettrico";
            }
        }

        // --- Mappa key -> descrizione (utile per UI e Mary AI) ---------
        public static readonly IReadOnlyDictionary<string, string> SymbolDescriptions
            = new Dictionary<string, string>
            {
                {"centralino", "Centralino da incasso"},
                {"contatore", "Contatore di energia attiva"},
                {"scatola_fem", "Scatola derivazione linea FEM"},
                {"scatola_luce", "Scatola derivazione linea Luci"},
                {"scatola_all", "Scatola derivazione allarme"},
                {"pozzetto_terra", "Pozzetto di terra"},
                {"videocit_int", "Videocitofono Interno"},
                {"videocit_est", "Videocitofono Esterno"},
                {"ventilatore", "Ventilatore elettrico da parete"},
                {"emergenza_inc", "Lampada di emergenza da incasso"},
                {"suoneria", "Suoneria"},
                {"pulsante", "Pulsante 1P NO 10 A"},
                {"interruttore_1p", "Interruttore 1P 16 A luminoso"},
                {"pulsante_doppio", "Doppio pulsante 1P NO + 1P NO 10 A"},
                {"interruttore_2p", "Interruttore Bipolare 16A"},
                {"pulsante_targh", "Pulsante 1P NO 12-24 V targhetta"},
                {"presa_univ", "Presa universale"},
                {"bipresa", "Bpresa"},
                {"predisp_6prese", "Predisposizione per nn.6 prese"},
                {"presa_cmd", "Presa comandata"},
                {"presa_tv", "Presa TV"},
                {"presa_sat", "Presa SAT"},
                {"luce_soffitto", "Corpo illuminante a soffitto"},
                {"luce_parete", "Corpo illuminante a parete"},
                {"passafilo", "Passafilo con serracavo"},
                {"var_quota", "Indica variazione di quota delpercorso dei cavi elettrici"},
                {"riv_gas", "Rivelatore GAS"},
                {"riv_acqua", "Rivelatore Acqua"},
                {"elettrovalv", "Elettrovalvola"},
                {"elettrovalv_no", "Elettrovalvola 3/4\" NO 12 Vcc"},
                {"elettrovalv_nc", "Elettrovalvola 3/4\" NC 12 Vcc"},
                {"cronoterm", "Cronotermostato estraibile 3 moduli"},
                {"inseritore_all", "Inseritore parzializzatore allarme"},
                {"centrale_all", "Centrale di comando allarme"},
                {"sirena_est", "Sirena da esterno"},
                {"sirena_int", "Sirena da interno"},
                {"contatto_mag", "Contatto magnetico allarme"},
                {"sensore_ir", "Sensore infrarossi allarme"},
            };

        // --- Lista chiavi disponibili (per dropdown / autocomplete) ----
        public static IEnumerable<string> AvailableSymbols => SymbolDescriptions.Keys;

        // ================================================================
        //  METODI Draw* per ciascun simbolo della legenda F-05
        // ================================================================

        // -- Centralino da incasso --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawPanel(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(-0.0439+pos.X, -0.0627+pos.Y), P(-0.0439+pos.X, -0.1882+pos.Y), layer);
            AddLine(tr, btr, P(-0.1694+pos.X, 0.0627+pos.Y), P(-0.1694+pos.X, -0.0627+pos.Y), layer);
            AddLine(tr, btr, P(0.0815+pos.X, 0.0627+pos.Y), P(-0.1694+pos.X, 0.0627+pos.Y), layer);
            AddLine(tr, btr, P(0.0815+pos.X, -0.0627+pos.Y), P(0.0815+pos.X, 0.0627+pos.Y), layer);
            AddLine(tr, btr, P(-0.1694+pos.X, -0.0627+pos.Y), P(0.0815+pos.X, -0.0627+pos.Y), layer);
            AddLine(tr, btr, P(0.0507+pos.X, 0.0627+pos.Y), P(0.0507+pos.X, 0.1882+pos.Y), layer);
            AddLine(tr, btr, P(-0.0121+pos.X, 0.0627+pos.Y), P(-0.0121+pos.X, 0.1882+pos.Y), layer);
            AddLine(tr, btr, P(-0.0748+pos.X, 0.0627+pos.Y), P(-0.0748+pos.X, 0.1882+pos.Y), layer);
            AddLine(tr, btr, P(-0.1375+pos.X, 0.0627+pos.Y), P(-0.1375+pos.X, 0.1882+pos.Y), layer);
            AddText(tr, btr, P(0.1694+pos.X, -0.0601+pos.Y), "INC", 0.0508, layer);
        }

        // -- Contatore di energia attiva --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawEnergyMeter(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(-0.1021+pos.X, 0.113+pos.Y), P(0.1021+pos.X, 0.113+pos.Y), layer);
            AddLine(tr, btr, P(0.1021+pos.X, 0.113+pos.Y), P(0.1021+pos.X, -0.113+pos.Y), layer);
            AddLine(tr, btr, P(0.1021+pos.X, -0.113+pos.Y), P(-0.1021+pos.X, -0.113+pos.Y), layer);
            AddLine(tr, btr, P(-0.1021+pos.X, -0.113+pos.Y), P(-0.1021+pos.X, 0.113+pos.Y), layer);
            AddLine(tr, btr, P(-0.1021+pos.X, 0.0509+pos.Y), P(0.1021+pos.X, 0.0509+pos.Y), layer);
            // INSERT "Linea_1" @ P(-0.0671+pos.X, 0.0809+pos.Y)
            AddText(tr, btr, P(0.0026+pos.X, -0.022+pos.Y), "KWh", 0.04, layer);
        }

        // -- Scatola derivazione linea FEM --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawJunctionBoxFEM(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(-0.2707+pos.X, 0.0881+pos.Y), P(-0.2707+pos.X, -0.0353+pos.Y), layer);
            AddLine(tr, btr, P(0.0384+pos.X, 0.0048+pos.Y), P(0.0544+pos.X, -0.0048+pos.Y), layer);
            AddCircle(tr, btr, P(0.0464+pos.X, 0+pos.Y), 0.1028, layer);
            AddText(tr, btr, P(-0.0802+pos.X, 0.0015+pos.Y), "FEM", 0.0814, layer);
            AddText(tr, btr, P(0.2707+pos.X, -0.0002+pos.Y), "INC", 0.0814, layer);
        }

        // -- Scatola derivazione linea Luci --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawJunctionBoxIll(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(0.1515+pos.X, 0.0555+pos.Y), P(0.1515+pos.X, -0.0474+pos.Y), layer);
            AddLine(tr, btr, P(-0.0569+pos.X, 0.0048+pos.Y), P(-0.0408+pos.X, -0.0048+pos.Y), layer);
            AddCircle(tr, btr, P(-0.0489+pos.X, 0+pos.Y), 0.1028, layer);
            AddText(tr, btr, P(0.1754+pos.X, -0.0002+pos.Y), "INC", 0.0814, layer);
            AddText(tr, btr, P(-0.1754+pos.X, 0.0015+pos.Y), "ILL", 0.0814, layer);
        }

        // -- Scatola derivazione allarme --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawJunctionBoxAll(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(0.053+pos.X, 0.0453+pos.Y), P(0.053+pos.X, -0.0575+pos.Y), layer);
            AddLine(tr, btr, P(0.2739+pos.X, 1.6591+pos.Y), P(0.2739+pos.X, -1.6591+pos.Y), layer);
            AddLine(tr, btr, P(-0.1554+pos.X, -0.0054+pos.Y), P(-0.1393+pos.X, -0.0149+pos.Y), layer);
            AddCircle(tr, btr, P(-0.1473+pos.X, -0.0102+pos.Y), 0.1028, layer);
            AddText(tr, btr, P(0.077+pos.X, -0.0104+pos.Y), "INC", 0.0814, layer);
            AddText(tr, btr, P(-0.2739+pos.X, -0.0086+pos.Y), "ALL", 0.0814, layer);
        }

        // -- Pozzetto di terra --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawGroundPit(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(-0.1047+pos.X, -0.0852+pos.Y), P(0.1047+pos.X, -0.0852+pos.Y), layer);
            AddLine(tr, btr, P(0.1047+pos.X, -0.0852+pos.Y), P(0.1047+pos.X, 0.1243+pos.Y), layer);
            AddLine(tr, btr, P(0.1047+pos.X, 0.1243+pos.Y), P(-0.1047+pos.X, 0.1243+pos.Y), layer);
            AddLine(tr, btr, P(-0.1047+pos.X, 0.1243+pos.Y), P(-0.1047+pos.X, -0.0852+pos.Y), layer);
            AddLine(tr, btr, P(-0.0768+pos.X, -0.0471+pos.Y), P(0.0704+pos.X, -0.0471+pos.Y), layer);
            AddLine(tr, btr, P(0.0704+pos.X, -0.0471+pos.Y), P(0.0704+pos.X, 0.0862+pos.Y), layer);
            AddLine(tr, btr, P(0.0704+pos.X, 0.0862+pos.Y), P(-0.0768+pos.X, 0.0862+pos.Y), layer);
            AddLine(tr, btr, P(-0.0768+pos.X, 0.0862+pos.Y), P(-0.0768+pos.X, -0.0471+pos.Y), layer);
            AddLine(tr, btr, P(-0.0639+pos.X, 0.017+pos.Y), P(0.0561+pos.X, 0.017+pos.Y), layer);
            AddLine(tr, btr, P(-0.0039+pos.X, 0.0168+pos.Y), P(-0.0039+pos.X, 0.0633+pos.Y), layer);
            AddLine(tr, btr, P(-0.0286+pos.X, -0.0252+pos.Y), P(0.0167+pos.X, -0.0252+pos.Y), layer);
            AddLine(tr, btr, P(-0.0505+pos.X, -0.0067+pos.Y), P(0.045+pos.X, -0.0067+pos.Y), layer);
            AddText(tr, btr, P(-0.0036+pos.X, -0.1243+pos.Y), "TERRA", 0.035, layer);
        }

        // -- Videocitofono Interno --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawVideophoneInt(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            // INSERT "Linea_2" @ P(0.144+pos.X, 0.008+pos.Y)
            // INSERT "Linea_3" @ P(0.1456+pos.X, 0.008+pos.Y)
            AddLine(tr, btr, P(-0.2306+pos.X, 0.1179+pos.Y), P(0.2306+pos.X, 0.1179+pos.Y), layer);
            AddLine(tr, btr, P(0.2306+pos.X, 0.1179+pos.Y), P(0.2306+pos.X, -0.1074+pos.Y), layer);
            AddLine(tr, btr, P(0.2306+pos.X, -0.1074+pos.Y), P(-0.2306+pos.X, -0.1074+pos.Y), layer);
            AddLine(tr, btr, P(-0.2306+pos.X, -0.1074+pos.Y), P(-0.2306+pos.X, 0.1179+pos.Y), layer);
            AddLine(tr, btr, P(0.1447+pos.X, 0.1179+pos.Y), P(0.2306+pos.X, 0.1179+pos.Y), layer);
            AddLine(tr, btr, P(0.2306+pos.X, 0.1179+pos.Y), P(0.2306+pos.X, -0.1074+pos.Y), layer);
            AddLine(tr, btr, P(0.2306+pos.X, -0.1074+pos.Y), P(0.1447+pos.X, -0.1074+pos.Y), layer);
            AddLine(tr, btr, P(0.1447+pos.X, -0.1074+pos.Y), P(0.1447+pos.X, 0.1179+pos.Y), layer);
            AddLine(tr, btr, P(0.1392+pos.X, -0.0968+pos.Y), P(0.049+pos.X, -0.0302+pos.Y), layer);
            AddLine(tr, btr, P(0.049+pos.X, -0.0302+pos.Y), P(0.049+pos.X, 0.0446+pos.Y), layer);
            AddLine(tr, btr, P(0.049+pos.X, 0.0446+pos.Y), P(0.1447+pos.X, 0.1179+pos.Y), layer);
            AddLine(tr, btr, P(0.1447+pos.X, 0.1179+pos.Y), P(0.1447+pos.X, -0.1074+pos.Y), layer);
            AddLine(tr, btr, P(0.1447+pos.X, -0.1074+pos.Y), P(0.1392+pos.X, -0.0968+pos.Y), layer);
            AddLine(tr, btr, P(0.049+pos.X, 0.0446+pos.Y), P(-0.0167+pos.X, 0.0446+pos.Y), layer);
            AddLine(tr, btr, P(-0.0167+pos.X, 0.0446+pos.Y), P(-0.0167+pos.X, -0.0314+pos.Y), layer);
            AddLine(tr, btr, P(-0.0167+pos.X, -0.0314+pos.Y), P(0.049+pos.X, -0.0314+pos.Y), layer);
            AddLine(tr, btr, P(0.049+pos.X, -0.0314+pos.Y), P(0.049+pos.X, 0.0446+pos.Y), layer);
            AddLine(tr, btr, P(-0.0999+pos.X, 0.0278+pos.Y), P(-0.1799+pos.X, 0.0278+pos.Y), layer);
            AddLine(tr, btr, P(-0.1799+pos.X, 0.0278+pos.Y), P(-0.1799+pos.X, -0.0122+pos.Y), layer);
            AddLine(tr, btr, P(-0.1799+pos.X, -0.0122+pos.Y), P(-0.0999+pos.X, -0.0122+pos.Y), layer);
            AddLine(tr, btr, P(-0.0999+pos.X, 0.0278+pos.Y), P(-0.0862+pos.X, 0.0654+pos.Y), layer);
            AddLine(tr, btr, P(-0.0999+pos.X, -0.0122+pos.Y), P(-0.0862+pos.X, -0.0498+pos.Y), layer);
            AddArc(tr, btr, P(-0.1057+pos.X, 0.0081+pos.Y), 0.0605, 288.62*DEG, 71.19*DEG, layer);
            AddText(tr, btr, P(0.225+pos.X, -0.1179+pos.Y), "P. I.", 0.06, layer);
        }

        // -- Videocitofono Esterno --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawVideophoneExt(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            // INSERT "Linea_4" @ P(0.144+pos.X, 0.008+pos.Y)
            // INSERT "Linea_5" @ P(0.1456+pos.X, 0.008+pos.Y)
            AddLine(tr, btr, P(-0.2306+pos.X, 0.1179+pos.Y), P(0.2306+pos.X, 0.1179+pos.Y), layer);
            AddLine(tr, btr, P(0.2306+pos.X, 0.1179+pos.Y), P(0.2306+pos.X, -0.1074+pos.Y), layer);
            AddLine(tr, btr, P(0.2306+pos.X, -0.1074+pos.Y), P(-0.2306+pos.X, -0.1074+pos.Y), layer);
            AddLine(tr, btr, P(-0.2306+pos.X, -0.1074+pos.Y), P(-0.2306+pos.X, 0.1179+pos.Y), layer);
            AddLine(tr, btr, P(0.1447+pos.X, 0.1179+pos.Y), P(0.2306+pos.X, 0.1179+pos.Y), layer);
            AddLine(tr, btr, P(0.2306+pos.X, 0.1179+pos.Y), P(0.2306+pos.X, -0.1074+pos.Y), layer);
            AddLine(tr, btr, P(0.2306+pos.X, -0.1074+pos.Y), P(0.1447+pos.X, -0.1074+pos.Y), layer);
            AddLine(tr, btr, P(0.1447+pos.X, -0.1074+pos.Y), P(0.1447+pos.X, 0.1179+pos.Y), layer);
            AddLine(tr, btr, P(0.1392+pos.X, -0.0968+pos.Y), P(0.049+pos.X, -0.0302+pos.Y), layer);
            AddLine(tr, btr, P(0.049+pos.X, -0.0302+pos.Y), P(0.049+pos.X, 0.0446+pos.Y), layer);
            AddLine(tr, btr, P(0.049+pos.X, 0.0446+pos.Y), P(0.1447+pos.X, 0.1179+pos.Y), layer);
            AddLine(tr, btr, P(0.1447+pos.X, 0.1179+pos.Y), P(0.1447+pos.X, -0.1074+pos.Y), layer);
            AddLine(tr, btr, P(0.1447+pos.X, -0.1074+pos.Y), P(0.1392+pos.X, -0.0968+pos.Y), layer);
            AddLine(tr, btr, P(0.049+pos.X, 0.0446+pos.Y), P(-0.0167+pos.X, 0.0446+pos.Y), layer);
            AddLine(tr, btr, P(-0.0167+pos.X, 0.0446+pos.Y), P(-0.0167+pos.X, -0.0314+pos.Y), layer);
            AddLine(tr, btr, P(-0.0167+pos.X, -0.0314+pos.Y), P(0.049+pos.X, -0.0314+pos.Y), layer);
            AddLine(tr, btr, P(0.049+pos.X, -0.0314+pos.Y), P(0.049+pos.X, 0.0446+pos.Y), layer);
            AddLine(tr, btr, P(-0.0999+pos.X, 0.0278+pos.Y), P(-0.1799+pos.X, 0.0278+pos.Y), layer);
            AddLine(tr, btr, P(-0.1799+pos.X, 0.0278+pos.Y), P(-0.1799+pos.X, -0.0122+pos.Y), layer);
            AddLine(tr, btr, P(-0.1799+pos.X, -0.0122+pos.Y), P(-0.0999+pos.X, -0.0122+pos.Y), layer);
            AddLine(tr, btr, P(-0.0999+pos.X, 0.0278+pos.Y), P(-0.0862+pos.X, 0.0654+pos.Y), layer);
            AddLine(tr, btr, P(-0.0999+pos.X, -0.0122+pos.Y), P(-0.0862+pos.X, -0.0498+pos.Y), layer);
            AddArc(tr, btr, P(-0.1057+pos.X, 0.0081+pos.Y), 0.0605, 288.62*DEG, 71.19*DEG, layer);
            AddText(tr, btr, P(0.225+pos.X, -0.1179+pos.Y), "P. E.", 0.06, layer);
        }

        // -- Ventilatore elettrico da parete --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawFan(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(-0.0846+pos.X, -0+pos.Y), P(-0.2154+pos.X, -0+pos.Y), layer);
            AddLine(tr, btr, P(-0.0846+pos.X, 0.15+pos.Y), P(-0.0846+pos.X, -0.15+pos.Y), layer);
            AddLine(tr, btr, P(-0.0846+pos.X, -0.15+pos.Y), P(0.2154+pos.X, -0.15+pos.Y), layer);
            AddLine(tr, btr, P(0.2154+pos.X, -0.15+pos.Y), P(0.2154+pos.X, 0.15+pos.Y), layer);
            AddLine(tr, btr, P(0.2154+pos.X, 0.15+pos.Y), P(-0.0846+pos.X, 0.15+pos.Y), layer);
            AddLine(tr, btr, P(0.0654+pos.X, -0+pos.Y), P(0.0708+pos.X, 0.0058+pos.Y), layer);
            AddLine(tr, btr, P(0.0708+pos.X, 0.0058+pos.Y), P(0.0856+pos.X, 0.0094+pos.Y), layer);
            AddLine(tr, btr, P(0.0856+pos.X, 0.0094+pos.Y), P(0.1023+pos.X, 0.0081+pos.Y), layer);
            AddLine(tr, btr, P(0.1023+pos.X, 0.0081+pos.Y), P(0.1232+pos.X, 0.0022+pos.Y), layer);
            AddLine(tr, btr, P(0.1232+pos.X, 0.0022+pos.Y), P(0.1428+pos.X, -0.0073+pos.Y), layer);
            AddLine(tr, btr, P(0.1428+pos.X, -0.0073+pos.Y), P(0.156+pos.X, -0.0174+pos.Y), layer);
            AddLine(tr, btr, P(0.156+pos.X, -0.0174+pos.Y), P(0.1647+pos.X, -0.0299+pos.Y), layer);
            AddLine(tr, btr, P(0.1647+pos.X, -0.0299+pos.Y), P(0.1649+pos.X, -0.0379+pos.Y), layer);
            AddLine(tr, btr, P(0.1649+pos.X, -0.0379+pos.Y), P(0.1595+pos.X, -0.0438+pos.Y), layer);
            AddLine(tr, btr, P(0.1595+pos.X, -0.0438+pos.Y), P(0.1446+pos.X, -0.0473+pos.Y), layer);
            AddLine(tr, btr, P(0.1446+pos.X, -0.0473+pos.Y), P(0.128+pos.X, -0.0461+pos.Y), layer);
            AddLine(tr, btr, P(0.128+pos.X, -0.0461+pos.Y), P(0.1071+pos.X, -0.0401+pos.Y), layer);
            AddLine(tr, btr, P(0.1071+pos.X, -0.0401+pos.Y), P(0.0875+pos.X, -0.0306+pos.Y), layer);
            AddLine(tr, btr, P(0.0875+pos.X, -0.0306+pos.Y), P(0.0742+pos.X, -0.0205+pos.Y), layer);
            AddLine(tr, btr, P(0.0742+pos.X, -0.0205+pos.Y), P(0.0655+pos.X, -0.008+pos.Y), layer);
            AddLine(tr, btr, P(0.0655+pos.X, -0.008+pos.Y), P(0.0654+pos.X, -0+pos.Y), layer);
            AddLine(tr, btr, P(0.0654+pos.X, -0+pos.Y), P(0.058+pos.X, 0.003+pos.Y), layer);
            AddLine(tr, btr, P(0.058+pos.X, 0.003+pos.Y), P(0.0494+pos.X, 0.0156+pos.Y), layer);
            AddLine(tr, btr, P(0.0494+pos.X, 0.0156+pos.Y), P(0.0446+pos.X, 0.0316+pos.Y), layer);
            AddLine(tr, btr, P(0.0446+pos.X, 0.0316+pos.Y), P(0.0427+pos.X, 0.0533+pos.Y), layer);
            AddLine(tr, btr, P(0.0427+pos.X, 0.0533+pos.Y), P(0.0446+pos.X, 0.0749+pos.Y), layer);
            AddLine(tr, btr, P(0.0446+pos.X, 0.0749+pos.Y), P(0.0494+pos.X, 0.0909+pos.Y), layer);
            AddLine(tr, btr, P(0.0494+pos.X, 0.0909+pos.Y), P(0.058+pos.X, 0.1035+pos.Y), layer);
            AddLine(tr, btr, P(0.058+pos.X, 0.1035+pos.Y), P(0.0654+pos.X, 0.1065+pos.Y), layer);
            AddLine(tr, btr, P(0.0654+pos.X, 0.1065+pos.Y), P(0.0728+pos.X, 0.1035+pos.Y), layer);
            AddLine(tr, btr, P(0.0728+pos.X, 0.1035+pos.Y), P(0.0814+pos.X, 0.0909+pos.Y), layer);
            AddLine(tr, btr, P(0.0814+pos.X, 0.0909+pos.Y), P(0.0861+pos.X, 0.0749+pos.Y), layer);
            AddLine(tr, btr, P(0.0861+pos.X, 0.0749+pos.Y), P(0.088+pos.X, 0.0533+pos.Y), layer);
            AddLine(tr, btr, P(0.088+pos.X, 0.0533+pos.Y), P(0.0861+pos.X, 0.0316+pos.Y), layer);
            AddLine(tr, btr, P(0.0861+pos.X, 0.0316+pos.Y), P(0.0814+pos.X, 0.0156+pos.Y), layer);
            AddLine(tr, btr, P(0.0814+pos.X, 0.0156+pos.Y), P(0.0728+pos.X, 0.003+pos.Y), layer);
            AddLine(tr, btr, P(0.0728+pos.X, 0.003+pos.Y), P(0.0654+pos.X, -0+pos.Y), layer);
            AddLine(tr, btr, P(0.0654+pos.X, -0+pos.Y), P(0.0588+pos.X, 0.0045+pos.Y), layer);
            AddLine(tr, btr, P(0.0588+pos.X, 0.0045+pos.Y), P(0.0435+pos.X, 0.0048+pos.Y), layer);
            AddLine(tr, btr, P(0.0435+pos.X, 0.0048+pos.Y), P(0.0276+pos.X, -0+pos.Y), layer);
            AddLine(tr, btr, P(0.0276+pos.X, -0+pos.Y), P(0.0084+pos.X, -0.0103+pos.Y), layer);
            AddLine(tr, btr, P(0.0084+pos.X, -0.0103+pos.Y), P(-0.0086+pos.X, -0.0238+pos.Y), layer);
            AddLine(tr, btr, P(-0.0086+pos.X, -0.0238+pos.Y), P(-0.0194+pos.X, -0.0365+pos.Y), layer);
            AddLine(tr, btr, P(-0.0194+pos.X, -0.0365+pos.Y), P(-0.0252+pos.X, -0.0507+pos.Y), layer);
            AddLine(tr, btr, P(-0.0252+pos.X, -0.0507+pos.Y), P(-0.0236+pos.X, -0.0585+pos.Y), layer);
            AddLine(tr, btr, P(-0.0236+pos.X, -0.0585+pos.Y), P(-0.0171+pos.X, -0.063+pos.Y), layer);
            AddLine(tr, btr, P(-0.0171+pos.X, -0.063+pos.Y), P(-0.0018+pos.X, -0.0633+pos.Y), layer);
            AddLine(tr, btr, P(-0.0018+pos.X, -0.0633+pos.Y), P(0.0141+pos.X, -0.0585+pos.Y), layer);
            AddLine(tr, btr, P(0.0141+pos.X, -0.0585+pos.Y), P(0.0333+pos.X, -0.0482+pos.Y), layer);
            AddLine(tr, btr, P(0.0333+pos.X, -0.0482+pos.Y), P(0.0504+pos.X, -0.0347+pos.Y), layer);
            AddLine(tr, btr, P(0.0504+pos.X, -0.0347+pos.Y), P(0.0611+pos.X, -0.0219+pos.Y), layer);
            AddLine(tr, btr, P(0.0611+pos.X, -0.0219+pos.Y), P(0.0669+pos.X, -0.0078+pos.Y), layer);
            AddLine(tr, btr, P(0.0669+pos.X, -0.0078+pos.Y), P(0.0654+pos.X, -0+pos.Y), layer);
            AddLine(tr, btr, P(0.0577+pos.X, -0+pos.Y), P(0.073+pos.X, -0+pos.Y), layer);
        }

        // -- Lampada di emergenza da incasso --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawEmergencyInc(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(-0.398+pos.X, -5.2098+pos.Y), P(-0.3984+pos.X, 5.2098+pos.Y), layer);
            AddLine(tr, btr, P(-0.0922+pos.X, 0.2196+pos.Y), P(0.0743+pos.X, 0.0531+pos.Y), layer);
            AddLine(tr, btr, P(-0.0922+pos.X, 0.0531+pos.Y), P(0.0743+pos.X, 0.2196+pos.Y), layer);
            AddLine(tr, btr, P(0.3984+pos.X, 0.3246+pos.Y), P(0.3984+pos.X, -0.4128+pos.Y), layer);
            AddLine(tr, btr, P(-0.0922+pos.X, 0.2196+pos.Y), P(0.0743+pos.X, 0.2196+pos.Y), layer);
            AddLine(tr, btr, P(0.0743+pos.X, 0.2196+pos.Y), P(0.0743+pos.X, 0.0531+pos.Y), layer);
            AddLine(tr, btr, P(0.0743+pos.X, 0.0531+pos.Y), P(-0.0922+pos.X, 0.0531+pos.Y), layer);
            AddLine(tr, btr, P(-0.0922+pos.X, 0.0531+pos.Y), P(-0.0922+pos.X, 0.2196+pos.Y), layer);
            AddLine(tr, btr, P(-0.1316+pos.X, 0.2605+pos.Y), P(0.1142+pos.X, 0.2605+pos.Y), layer);
            AddLine(tr, btr, P(0.1142+pos.X, 0.2605+pos.Y), P(0.1142+pos.X, 0.0147+pos.Y), layer);
            AddLine(tr, btr, P(0.1142+pos.X, 0.0147+pos.Y), P(-0.1316+pos.X, 0.0147+pos.Y), layer);
            AddLine(tr, btr, P(-0.1316+pos.X, 0.0147+pos.Y), P(-0.1316+pos.X, 0.2605+pos.Y), layer);
            AddLine(tr, btr, P(-0.0922+pos.X, 0.2196+pos.Y), P(0.0743+pos.X, 0.0531+pos.Y), layer);
            AddLine(tr, btr, P(0.0743+pos.X, 0.2196+pos.Y), P(-0.0922+pos.X, 0.0531+pos.Y), layer);
        }

        // -- Suoneria --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawBell(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(-0.0756+pos.X, 0.0003+pos.Y), P(-0.0757+pos.X, -0.1363+pos.Y), layer);
            AddLine(tr, btr, P(0.1832+pos.X, -0+pos.Y), P(-0.1832+pos.X, 0+pos.Y), layer);
            AddLine(tr, btr, P(0.066+pos.X, 0.0003+pos.Y), P(0.0659+pos.X, -0.1363+pos.Y), layer);
            AddArc(tr, btr, P(-0.0454+pos.X, 0.0691+pos.Y), 0.0429, 17.23*DEG, 162.77*DEG, layer);
            AddArc(tr, btr, P(0+pos.X, -0+pos.Y), 0.1832, 0.00*DEG, 180.00*DEG, layer);
            AddArc(tr, btr, P(0.0365+pos.X, 0.0945+pos.Y), 0.0429, 197.23*DEG, 342.77*DEG, layer);
        }

        // -- Pulsante 1P NO 10 A --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawButton(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(0.0022+pos.X, 0.03+pos.Y), P(0.0589+pos.X, 0.1082+pos.Y), layer);
            AddLine(tr, btr, P(0.0589+pos.X, 0.1082+pos.Y), P(0.1186+pos.X, 0.0649+pos.Y), layer);
            AddCircle(tr, btr, P(-0.0439+pos.X, -0.0335+pos.Y), 0.0747, layer);
        }

        // -- Interruttore 1P 16 A luminoso --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawSwitch1P(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(0.0022+pos.X, 0.03+pos.Y), P(0.0589+pos.X, 0.1082+pos.Y), layer);
            AddLine(tr, btr, P(0.0589+pos.X, 0.1082+pos.Y), P(0.1186+pos.X, 0.0649+pos.Y), layer);
            AddLine(tr, btr, P(-0.0464+pos.X, 0.0326+pos.Y), P(-0.0442+pos.X, -0.1054+pos.Y), layer);
            AddLine(tr, btr, P(-0.1144+pos.X, -0.0435+pos.Y), P(0.0259+pos.X, -0.0222+pos.Y), layer);
            AddCircle(tr, btr, P(-0.0439+pos.X, -0.0335+pos.Y), 0.0747, layer);
        }

        // -- Doppio pulsante 1P NO + 1P NO 10 A --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawDoubleButton(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(-0.0216+pos.X, -0.2288+pos.Y), P(0.004+pos.X, -0.1218+pos.Y), layer);
            AddLine(tr, btr, P(0.004+pos.X, -0.1218+pos.Y), P(0.0635+pos.X, -0.1365+pos.Y), layer);
            AddLine(tr, btr, P(-0.0048+pos.X, -0.1585+pos.Y), P(0.0547+pos.X, -0.1732+pos.Y), layer);
            AddLine(tr, btr, P(0.0847+pos.X, 0.2288+pos.Y), P(0.0847+pos.X, -0.0463+pos.Y), layer);
            AddLine(tr, btr, P(0.0847+pos.X, -0.0463+pos.Y), P(-0.0847+pos.X, -0.0463+pos.Y), layer);
            AddLine(tr, btr, P(-0.0847+pos.X, -0.0463+pos.Y), P(-0.0847+pos.X, 0.2288+pos.Y), layer);
            AddCircle(tr, btr, P(0+pos.X, 0.1437+pos.Y), 0.0371, layer);
            AddCircle(tr, btr, P(0+pos.X, 0.1437+pos.Y), 0.0551, layer);
            AddCircle(tr, btr, P(0+pos.X, 0.0336+pos.Y), 0.0371, layer);
            AddCircle(tr, btr, P(0+pos.X, 0.0336+pos.Y), 0.0551, layer);
        }

        // -- Interruttore Bipolare 16A --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawSwitch2P(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(-0.1943+pos.X, -0.1635+pos.Y), P(0.1943+pos.X, -0.1635+pos.Y), layer);
            AddCircle(tr, btr, P(-0.0057+pos.X, 0.0921+pos.Y), 0.0714, layer);
            AddText(tr, btr, P(0.0045+pos.X, -0.0441+pos.Y), "16A", 0.0511, layer);
        }

        // -- Pulsante 1P NO 12-24 V targhetta --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawButtonPlate(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(0.329+pos.X, 2.2141+pos.Y), P(0.329+pos.X, -2.2141+pos.Y), layer);
            AddLine(tr, btr, P(-0.1544+pos.X, 0.1823+pos.Y), P(-0.1023+pos.X, 0.2269+pos.Y), layer);
            AddLine(tr, btr, P(-0.101+pos.X, 0.1782+pos.Y), P(-0.1501+pos.X, 0.235+pos.Y), layer);
            AddLine(tr, btr, P(-0.329+pos.X, 0.0627+pos.Y), P(-0.329+pos.X, 0.2874+pos.Y), layer);
            AddLine(tr, btr, P(0.0596+pos.X, 0.2874+pos.Y), P(0.0596+pos.X, 0.0627+pos.Y), layer);
            AddLine(tr, btr, P(0.0596+pos.X, 0.0627+pos.Y), P(-0.329+pos.X, 0.0627+pos.Y), layer);
            AddLine(tr, btr, P(-0.2869+pos.X, 0.1109+pos.Y), P(0.0217+pos.X, 0.1109+pos.Y), layer);
        }

        // -- Presa universale --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawSocketUniv(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(-0+pos.X, -0.0338+pos.Y), P(-0+pos.X, -0.128+pos.Y), layer);
            AddLine(tr, btr, P(0.0812+pos.X, -0.0338+pos.Y), P(-0.0812+pos.X, -0.0338+pos.Y), layer);
            AddArc(tr, btr, P(0.0005+pos.X, 0.0473+pos.Y), 0.0808, 180.21*DEG, 359.59*DEG, layer);
            AddText(tr, btr, P(0.0031+pos.X, 0.0976+pos.Y), "UNIV.", 0.0524, layer);
        }

        // -- Bpresa --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawDoubleSocket(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(-0+pos.X, -0.0338+pos.Y), P(-0+pos.X, -0.128+pos.Y), layer);
            AddLine(tr, btr, P(0.0812+pos.X, -0.0338+pos.Y), P(-0.0812+pos.X, -0.0338+pos.Y), layer);
            AddArc(tr, btr, P(0.0005+pos.X, 0.0473+pos.Y), 0.0808, 180.21*DEG, 359.59*DEG, layer);
            AddText(tr, btr, P(0.0031+pos.X, 0.0976+pos.Y), "10/16", 0.0524, layer);
        }

        // -- Predisposizione per nn.6 prese --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawSocket6Prep(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(-0.0839+pos.X, 0.198+pos.Y), P(-0.0839+pos.X, 0.0347+pos.Y), layer);
            AddLine(tr, btr, P(0.0026+pos.X, -0.015+pos.Y), P(0.0026+pos.X, -0.1092+pos.Y), layer);
            AddLine(tr, btr, P(0.0838+pos.X, -0.015+pos.Y), P(-0.0786+pos.X, -0.015+pos.Y), layer);
            AddArc(tr, btr, P(0.0031+pos.X, 0.066+pos.Y), 0.0808, 180.21*DEG, 359.59*DEG, layer);
            AddText(tr, btr, P(0.0057+pos.X, 0.1164+pos.Y), "10/16", 0.0524, layer);
            AddText(tr, btr, P(0.0021+pos.X, -0.198+pos.Y), "**", 0.08, layer);
        }

        // -- Presa comandata --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawSocketCmd(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(-0.0167+pos.X, 0.0206+pos.Y), P(-0.1393+pos.X, 0.0206+pos.Y), layer);
            AddLine(tr, btr, P(-0.078+pos.X, 0.0206+pos.Y), P(-0.078+pos.X, -0.0505+pos.Y), layer);
            // INSERT "Linea_6" @ P(0.1304+pos.X, 0.0723+pos.Y)
            AddArc(tr, btr, P(-0.0776+pos.X, 0.0818+pos.Y), 0.0609, 180.21*DEG, 359.59*DEG, layer);
            AddCircle(tr, btr, P(0.1304+pos.X, 0.0723+pos.Y), 0.009, layer);
            AddText(tr, btr, P(-0.0789+pos.X, 0.1003+pos.Y), "UNIV", 0.0396, layer);
            AddText(tr, btr, P(-0.078+pos.X, -0.1427+pos.Y), "*", 0.08, layer);
        }

        // -- Presa TV --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawSocketTV(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(0.1052+pos.X, 0.0177+pos.Y), P(0.1055+pos.X, 0.1084+pos.Y), layer);
            AddLine(tr, btr, P(-0.1055+pos.X, 0.0183+pos.Y), P(0.1052+pos.X, 0.0177+pos.Y), layer);
            AddLine(tr, btr, P(-0.1055+pos.X, 0.0183+pos.Y), P(-0.1052+pos.X, 0.109+pos.Y), layer);
            AddLine(tr, btr, P(-0.0001+pos.X, 0.018+pos.Y), P(-0.0005+pos.X, -0.109+pos.Y), layer);
            AddText(tr, btr, P(0.0441+pos.X, 0.0818+pos.Y), "TV", 0.0642, layer);
        }

        // -- Presa SAT --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawSocketSAT(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(0.1052+pos.X, 0.0177+pos.Y), P(0.1055+pos.X, 0.1084+pos.Y), layer);
            AddLine(tr, btr, P(-0.1055+pos.X, 0.0183+pos.Y), P(0.1052+pos.X, 0.0177+pos.Y), layer);
            AddLine(tr, btr, P(-0.1055+pos.X, 0.0183+pos.Y), P(-0.1052+pos.X, 0.109+pos.Y), layer);
            AddLine(tr, btr, P(-0.0001+pos.X, 0.018+pos.Y), P(-0.0005+pos.X, -0.109+pos.Y), layer);
            AddText(tr, btr, P(0.066+pos.X, 0.0812+pos.Y), "SAT", 0.0642, layer);
        }

        // -- Corpo illuminante a soffitto --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawLightCeiling(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(0.0693+pos.X, -0.0693+pos.Y), P(-0.0693+pos.X, 0.0693+pos.Y), layer);
            AddLine(tr, btr, P(0.0693+pos.X, 0.0693+pos.Y), P(-0.0693+pos.X, -0.0693+pos.Y), layer);
        }

        // -- Corpo illuminante a parete --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawLightWall(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(0.0482+pos.X, -0.065+pos.Y), P(-0.0903+pos.X, 0.0735+pos.Y), layer);
            AddLine(tr, btr, P(0.0482+pos.X, 0.0735+pos.Y), P(-0.0903+pos.X, -0.065+pos.Y), layer);
            AddLine(tr, btr, P(0.0903+pos.X, 0.0753+pos.Y), P(0.0903+pos.X, -0.0753+pos.Y), layer);
        }

        // -- Passafilo con serracavo --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawCableGland(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            // HATCH non gestito (riempimento) - geometria di contorno disegnata sopra
            AddLine(tr, btr, P(0.0753+pos.X, 0.2068+pos.Y), P(0.0753+pos.X, -0.0604+pos.Y), layer);
            AddLine(tr, btr, P(0.0753+pos.X, -0.0604+pos.Y), P(-0.0987+pos.X, -0.0604+pos.Y), layer);
            AddLine(tr, btr, P(-0.0987+pos.X, -0.0604+pos.Y), P(-0.0987+pos.X, 0.2071+pos.Y), layer);
            AddLine(tr, btr, P(-0.0987+pos.X, 0.2071+pos.Y), P(0.0753+pos.X, 0.2068+pos.Y), layer);
            AddLine(tr, btr, P(-0.0509+pos.X, -0.0192+pos.Y), P(-0.0509+pos.X, 0.163+pos.Y), layer);
            AddLine(tr, btr, P(-0.0509+pos.X, 0.163+pos.Y), P(0.0296+pos.X, 0.163+pos.Y), layer);
            AddLine(tr, btr, P(0.0296+pos.X, 0.163+pos.Y), P(0.0296+pos.X, -0.0192+pos.Y), layer);
            AddLine(tr, btr, P(0.0296+pos.X, -0.0192+pos.Y), P(-0.0509+pos.X, -0.0192+pos.Y), layer);
            AddLine(tr, btr, P(0.072+pos.X, -0.1863+pos.Y), P(0.0452+pos.X, -0.1656+pos.Y), layer);
            AddLine(tr, btr, P(0.0987+pos.X, -0.2071+pos.Y), P(0.072+pos.X, -0.1863+pos.Y), layer);
        }

        // -- Indica variazione di quota delpercorso dei cavi elettrici --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawHeightChange(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            // HATCH non gestito (riempimento) - geometria di contorno disegnata sopra
            AddLine(tr, btr, P(0.3022+pos.X, 2.2122+pos.Y), P(0.3022+pos.X, -2.2122+pos.Y), layer);
            AddLine(tr, btr, P(-0.3022+pos.X, 0.6778+pos.Y), P(0.1074+pos.X, 0.6778+pos.Y), layer);
            AddLine(tr, btr, P(0.1074+pos.X, 0.6778+pos.Y), P(0.1074+pos.X, 0.4293+pos.Y), layer);
            AddLine(tr, btr, P(-0.3022+pos.X, 0.6778+pos.Y), P(-0.3022+pos.X, 0.4293+pos.Y), layer);
            AddLine(tr, btr, P(-0.3022+pos.X, 0.4293+pos.Y), P(0.1074+pos.X, 0.4293+pos.Y), layer);
            AddCircle(tr, btr, P(-0.1115+pos.X, 0.9065+pos.Y), 0.0152, layer);
            AddText(tr, btr, P(-0.0877+pos.X, 0.5807+pos.Y), "EM", 0.0656, layer);
        }

        // -- Rivelatore GAS --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawDetectorGas(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(-0.2031+pos.X, -0.1732+pos.Y), P(0.2064+pos.X, -0.1732+pos.Y), layer);
            AddLine(tr, btr, P(-0.2064+pos.X, 0.1732+pos.Y), P(0.2031+pos.X, 0.1732+pos.Y), layer);
            AddLine(tr, btr, P(0.2031+pos.X, 0.1732+pos.Y), P(0.2031+pos.X, -0.0752+pos.Y), layer);
            AddLine(tr, btr, P(0.2031+pos.X, -0.0752+pos.Y), P(-0.2064+pos.X, -0.0752+pos.Y), layer);
            AddLine(tr, btr, P(-0.2064+pos.X, 0.1732+pos.Y), P(-0.2064+pos.X, -0.0752+pos.Y), layer);
            AddText(tr, btr, P(0.0081+pos.X, 0.0762+pos.Y), "CH4", 0.0656, layer);
        }

        // -- Rivelatore Acqua --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawDetectorWater(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(0.2048+pos.X, 0.1752+pos.Y), P(0.2048+pos.X, -0.0733+pos.Y), layer);
            AddLine(tr, btr, P(0.2048+pos.X, -0.0733+pos.Y), P(-0.2048+pos.X, -0.0733+pos.Y), layer);
            AddLine(tr, btr, P(-0.2048+pos.X, 0.1752+pos.Y), P(-0.2048+pos.X, -0.0733+pos.Y), layer);
            AddLine(tr, btr, P(-0.1073+pos.X, -0.1752+pos.Y), P(0.1059+pos.X, -0.1752+pos.Y), layer);
            AddText(tr, btr, P(0.0098+pos.X, 0.0782+pos.Y), "H2O", 0.0656, layer);
        }

        // -- Elettrovalvola --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawSolenoidValve(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(-0.0386+pos.X, 0.1636+pos.Y), P(-0.0386+pos.X, -0.0372+pos.Y), layer);
            AddLine(tr, btr, P(-0.0386+pos.X, -0.0372+pos.Y), P(-0.2518+pos.X, -0.0372+pos.Y), layer);
            AddLine(tr, btr, P(-0.2518+pos.X, -0.0372+pos.Y), P(-0.2518+pos.X, 0.1636+pos.Y), layer);
            AddLine(tr, btr, P(0.2518+pos.X, 0.9154+pos.Y), P(0.2518+pos.X, -0.9154+pos.Y), layer);
            AddText(tr, btr, P(-0.2193+pos.X, 0.0667+pos.Y), "EV", 0.0718, layer);
            AddText(tr, btr, P(-0.2183+pos.X, -0.0131+pos.Y), "3/4\"", 0.0359, layer);
        }

        // -- Elettrovalvola 3/4" NO 12 Vcc --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawSolenoidValveNO(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(-0.0386+pos.X, 0.1636+pos.Y), P(-0.0386+pos.X, -0.0372+pos.Y), layer);
            AddLine(tr, btr, P(-0.0386+pos.X, -0.0372+pos.Y), P(-0.2518+pos.X, -0.0372+pos.Y), layer);
            AddLine(tr, btr, P(-0.2518+pos.X, -0.0372+pos.Y), P(-0.2518+pos.X, 0.1636+pos.Y), layer);
            AddLine(tr, btr, P(0.2518+pos.X, 0.9154+pos.Y), P(0.2518+pos.X, -0.9154+pos.Y), layer);
            AddLine(tr, btr, P(-0.2518+pos.X, -0.2006+pos.Y), P(-0.0386+pos.X, -0.2006+pos.Y), layer);
            AddText(tr, btr, P(-0.2193+pos.X, 0.0667+pos.Y), "EV", 0.0718, layer);
            AddText(tr, btr, P(-0.2183+pos.X, -0.0131+pos.Y), "3/4\"", 0.0359, layer);
            AddText(tr, btr, P(-0.2489+pos.X, -0.1242+pos.Y), "NO", 0.0575, layer);
            AddText(tr, btr, P(-0.124+pos.X, -0.067+pos.Y), "12Vcc", 0.0215, layer);
        }

        // -- Elettrovalvola 3/4" NC 12 Vcc --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawSolenoidValveNC(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(0.1066+pos.X, 0.1439+pos.Y), P(0.1066+pos.X, -0.057+pos.Y), layer);
            AddLine(tr, btr, P(0.1066+pos.X, -0.057+pos.Y), P(-0.1066+pos.X, -0.057+pos.Y), layer);
            AddLine(tr, btr, P(-0.1066+pos.X, -0.057+pos.Y), P(-0.1066+pos.X, 0.1439+pos.Y), layer);
            AddText(tr, btr, P(-0.0741+pos.X, 0.047+pos.Y), "EV", 0.0718, layer);
            AddText(tr, btr, P(-0.0732+pos.X, -0.0328+pos.Y), "3/4\"", 0.0359, layer);
            AddText(tr, btr, P(-0.1037+pos.X, -0.1439+pos.Y), "NC", 0.0575, layer);
            AddText(tr, btr, P(0.0212+pos.X, -0.0867+pos.Y), "12Vcc", 0.0215, layer);
        }

        // -- Cronotermostato estraibile 3 moduli --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawThermostat(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(-0.175+pos.X, 0.1181+pos.Y), P(0.175+pos.X, 0.1181+pos.Y), layer);
            AddLine(tr, btr, P(0.175+pos.X, 0.1181+pos.Y), P(0.175+pos.X, -0.1181+pos.Y), layer);
            AddLine(tr, btr, P(0.175+pos.X, -0.1181+pos.Y), P(-0.175+pos.X, -0.1181+pos.Y), layer);
            AddLine(tr, btr, P(-0.175+pos.X, -0.1181+pos.Y), P(-0.175+pos.X, 0.1181+pos.Y), layer);
            AddLine(tr, btr, P(-0.0743+pos.X, -0.0053+pos.Y), P(-0.1329+pos.X, -0.0053+pos.Y), layer);
            AddLine(tr, btr, P(-0.0833+pos.X, -0.0131+pos.Y), P(-0.0833+pos.X, 0.0411+pos.Y), layer);
            AddCircle(tr, btr, P(-0.0833+pos.X, -0.0053+pos.Y), 0.0622, layer);
            AddCircle(tr, btr, P(0.0179+pos.X, 0.0242+pos.Y), 0.0161, layer);
        }

        // -- Inseritore parzializzatore allarme --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawAlarmInserter(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(-0.15+pos.X, -0.0892+pos.Y), P(-0.15+pos.X, 0.0892+pos.Y), layer);
            AddLine(tr, btr, P(0.15+pos.X, 0.0892+pos.Y), P(0.15+pos.X, -0.0892+pos.Y), layer);
            AddLine(tr, btr, P(0.15+pos.X, -0.0892+pos.Y), P(-0.15+pos.X, -0.0892+pos.Y), layer);
            AddLine(tr, btr, P(-0.0949+pos.X, -0.0342+pos.Y), P(-0.0949+pos.X, 0.0361+pos.Y), layer);
            AddLine(tr, btr, P(-0.0949+pos.X, 0.0361+pos.Y), P(0.093+pos.X, 0.0361+pos.Y), layer);
            AddLine(tr, btr, P(0.093+pos.X, 0.0361+pos.Y), P(0.093+pos.X, -0.0342+pos.Y), layer);
            AddLine(tr, btr, P(0.093+pos.X, -0.0342+pos.Y), P(-0.0949+pos.X, -0.0342+pos.Y), layer);
            AddLine(tr, btr, P(0.0382+pos.X, -0.0232+pos.Y), P(0.0382+pos.X, 0.0033+pos.Y), layer);
            AddLine(tr, btr, P(0.0584+pos.X, 0.006+pos.Y), P(-0.0169+pos.X, 0.006+pos.Y), layer);
            AddCircle(tr, btr, P(-0.0402+pos.X, 0.0028+pos.Y), 0.0235, layer);
            AddCircle(tr, btr, P(-0.0402+pos.X, 0.0028+pos.Y), 0.0042, layer);
        }

        // -- Centrale di comando allarme --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawAlarmPanel(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(-0.1753+pos.X, -0.0472+pos.Y), P(0.1747+pos.X, -0.0472+pos.Y), layer);
            AddLine(tr, btr, P(0.1747+pos.X, -0.0472+pos.Y), P(0.1747+pos.X, 0.1568+pos.Y), layer);
            AddLine(tr, btr, P(0.1747+pos.X, 0.1568+pos.Y), P(-0.1753+pos.X, 0.1568+pos.Y), layer);
            AddLine(tr, btr, P(-0.1753+pos.X, 0.1568+pos.Y), P(-0.1753+pos.X, -0.0472+pos.Y), layer);
            AddLine(tr, btr, P(-0.0861+pos.X, 0.0786+pos.Y), P(-0.0861+pos.X, -0.0251+pos.Y), layer);
            AddLine(tr, btr, P(-0.0861+pos.X, 0.0039+pos.Y), P(-0.1215+pos.X, 0.0039+pos.Y), layer);
            AddLine(tr, btr, P(-0.1715+pos.X, -0.1568+pos.Y), P(0.1753+pos.X, -0.1568+pos.Y), layer);
            AddCircle(tr, btr, P(-0.0853+pos.X, 0.1099+pos.Y), 0.0314, layer);
            AddCircle(tr, btr, P(-0.0853+pos.X, 0.1099+pos.Y), 0.0061, layer);
            AddText(tr, btr, P(0.0778+pos.X, 0.0327+pos.Y), "A", 0.0724, layer);
        }

        // -- Sirena da esterno --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawSirenExt(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(-0.2296+pos.X, 0.2917+pos.Y), P(-0.1658+pos.X, 0.2917+pos.Y), layer);
            AddLine(tr, btr, P(-0.1658+pos.X, 0.2917+pos.Y), P(-0.1658+pos.X, 0.1621+pos.Y), layer);
            AddLine(tr, btr, P(-0.1658+pos.X, 0.1621+pos.Y), P(-0.2296+pos.X, 0.1621+pos.Y), layer);
            AddLine(tr, btr, P(-0.2296+pos.X, 0.1621+pos.Y), P(-0.2296+pos.X, 0.2917+pos.Y), layer);
            AddLine(tr, btr, P(-0.1658+pos.X, 0.2326+pos.Y), P(0.0221+pos.X, 0.2993+pos.Y), layer);
            AddLine(tr, btr, P(0.0221+pos.X, 0.2993+pos.Y), P(-0.0324+pos.X, 0.1818+pos.Y), layer);
            AddLine(tr, btr, P(-0.0324+pos.X, 0.1818+pos.Y), P(-0.1658+pos.X, 0.1997+pos.Y), layer);
            AddLine(tr, btr, P(-0.2184+pos.X, 0.2354+pos.Y), P(-0.1808+pos.X, 0.2354+pos.Y), layer);
            AddLine(tr, btr, P(-0.2184+pos.X, 0.215+pos.Y), P(-0.2043+pos.X, 0.215+pos.Y), layer);
            AddLine(tr, btr, P(-0.1936+pos.X, 0.2147+pos.Y), P(-0.1791+pos.X, 0.2147+pos.Y), layer);
            AddLine(tr, btr, P(-0.197+pos.X, 0.294+pos.Y), P(-0.197+pos.X, 0.3358+pos.Y), layer);
            AddLine(tr, btr, P(-0.1977+pos.X, 0.1621+pos.Y), P(-0.1977+pos.X, 0.1148+pos.Y), layer);
            AddLine(tr, btr, P(-0.2917+pos.X, -0.0458+pos.Y), P(0.0583+pos.X, -0.0458+pos.Y), layer);
            AddLine(tr, btr, P(0.0551+pos.X, 0.3552+pos.Y), P(0.0551+pos.X, 0.1061+pos.Y), layer);
            AddLine(tr, btr, P(0.0551+pos.X, 0.1061+pos.Y), P(-0.2917+pos.X, 0.1061+pos.Y), layer);
            AddLine(tr, btr, P(-0.2917+pos.X, 0.1061+pos.Y), P(-0.2917+pos.X, 0.3552+pos.Y), layer);
            AddLine(tr, btr, P(0.2917+pos.X, 1.1022+pos.Y), P(0.2917+pos.X, -1.1022+pos.Y), layer);
            AddText(tr, btr, P(-0.1156+pos.X, 0.0407+pos.Y), "DA ESTERNO", 0.0423, layer);
        }

        // -- Sirena da interno --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawSirenInt(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(0.175+pos.X, 0.1375+pos.Y), P(0.175+pos.X, -0.0565+pos.Y), layer);
            AddLine(tr, btr, P(0.175+pos.X, -0.0565+pos.Y), P(-0.175+pos.X, -0.0565+pos.Y), layer);
            AddLine(tr, btr, P(-0.175+pos.X, -0.0565+pos.Y), P(-0.175+pos.X, 0.1375+pos.Y), layer);
            AddLine(tr, btr, P(-0.1259+pos.X, 0.1035+pos.Y), P(-0.0602+pos.X, 0.1035+pos.Y), layer);
            AddLine(tr, btr, P(-0.0602+pos.X, 0.1035+pos.Y), P(-0.0602+pos.X, -0.0311+pos.Y), layer);
            AddLine(tr, btr, P(-0.0602+pos.X, -0.0311+pos.Y), P(-0.1259+pos.X, -0.0311+pos.Y), layer);
            AddLine(tr, btr, P(-0.1259+pos.X, -0.0311+pos.Y), P(-0.1259+pos.X, 0.1035+pos.Y), layer);
            AddLine(tr, btr, P(-0.0602+pos.X, 0.0473+pos.Y), P(0.1314+pos.X, 0.1138+pos.Y), layer);
            AddLine(tr, btr, P(0.1314+pos.X, 0.1138+pos.Y), P(0.0768+pos.X, -0.0082+pos.Y), layer);
            AddLine(tr, btr, P(0.0768+pos.X, -0.0082+pos.Y), P(-0.0602+pos.X, 0.0116+pos.Y), layer);
            AddLine(tr, btr, P(-0.1259+pos.X, -0.0311+pos.Y), P(-0.0602+pos.X, 0.1035+pos.Y), layer);
            AddLine(tr, btr, P(-0.1027+pos.X, 0.0001+pos.Y), P(-0.0748+pos.X, 0.0001+pos.Y), layer);
            AddLine(tr, btr, P(-0.1044+pos.X, -0.0143+pos.Y), P(-0.0921+pos.X, -0.0143+pos.Y), layer);
            AddLine(tr, btr, P(-0.0857+pos.X, -0.0143+pos.Y), P(-0.0735+pos.X, -0.0143+pos.Y), layer);
            AddArc(tr, btr, P(-0.1094+pos.X, 0.0818+pos.Y), 0.0098, 0.00*DEG, 180.00*DEG, layer);
            AddArc(tr, btr, P(-0.0898+pos.X, 0.0818+pos.Y), 0.0098, 180.00*DEG, 0.00*DEG, layer);
            AddText(tr, btr, P(-0.0008+pos.X, -0.1375+pos.Y), "DA INTERNO", 0.0428, layer);
        }

        // -- Contatto magnetico allarme --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawMagContact(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(0.0506+pos.X, -0.1794+pos.Y), P(-0.0347+pos.X, -0.1794+pos.Y), layer);
            AddLine(tr, btr, P(-0.0057+pos.X, 0.0557+pos.Y), P(0.0099+pos.X, 0.0557+pos.Y), layer);
            AddLine(tr, btr, P(-0.0212+pos.X, 0.0557+pos.Y), P(-0.0212+pos.X, 0.0447+pos.Y), layer);
            AddLine(tr, btr, P(0.0099+pos.X, 0.0557+pos.Y), P(0.0099+pos.X, 0.0424+pos.Y), layer);
            AddLine(tr, btr, P(-0.0057+pos.X, 0.0557+pos.Y), P(-0.0212+pos.X, 0.0557+pos.Y), layer);
            AddLine(tr, btr, P(0.1264+pos.X, 0.0399+pos.Y), P(-0.1377+pos.X, 0.0399+pos.Y), layer);
            AddLine(tr, btr, P(0.1861+pos.X, -0.0382+pos.Y), P(-0.1865+pos.X, -0.0382+pos.Y), layer);
            AddLine(tr, btr, P(-0.1865+pos.X, -0.0382+pos.Y), P(-0.1865+pos.X, 0.1794+pos.Y), layer);
            AddLine(tr, btr, P(-0.1865+pos.X, 0.1794+pos.Y), P(0.1865+pos.X, 0.1794+pos.Y), layer);
            AddLine(tr, btr, P(0.1865+pos.X, 0.1794+pos.Y), P(0.1861+pos.X, -0.0382+pos.Y), layer);
        }

        // -- Sensore infrarossi allarme --
        // Estratto fedelmente dal DXF F-05 LEGENDA IMPIANTO ELETTRICO
        private static void DrawIRSensor(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer)
        {
            AddLine(tr, btr, P(-0.1191+pos.X, -0.0443+pos.Y), P(-0.0827+pos.X, -0.0665+pos.Y), layer);
            AddLine(tr, btr, P(0.0585+pos.X, -0.0912+pos.Y), P(0.0585+pos.X, 0.1257+pos.Y), layer);
            AddLine(tr, btr, P(-0.0267+pos.X, -0.0912+pos.Y), P(0.0585+pos.X, -0.0912+pos.Y), layer);
            AddLine(tr, btr, P(-0.1191+pos.X, -0.0443+pos.Y), P(-0.079+pos.X, -0.0297+pos.Y), layer);
            AddLine(tr, btr, P(-0.0267+pos.X, 0.1257+pos.Y), P(-0.0267+pos.X, -0.0912+pos.Y), layer);
            AddLine(tr, btr, P(-0.1191+pos.X, -0.1257+pos.Y), P(-0.0853+pos.X, -0.1224+pos.Y), layer);
            AddLine(tr, btr, P(-0.0853+pos.X, -0.1224+pos.Y), P(-0.06+pos.X, -0.1138+pos.Y), layer);
            AddLine(tr, btr, P(-0.06+pos.X, -0.1138+pos.Y), P(-0.0406+pos.X, -0.0987+pos.Y), layer);
            AddLine(tr, btr, P(-0.0406+pos.X, -0.0987+pos.Y), P(-0.0356+pos.X, -0.085+pos.Y), layer);
            AddLine(tr, btr, P(-0.0356+pos.X, -0.085+pos.Y), P(-0.0406+pos.X, -0.0713+pos.Y), layer);
            AddLine(tr, btr, P(-0.0406+pos.X, -0.0713+pos.Y), P(-0.06+pos.X, -0.0562+pos.Y), layer);
            AddLine(tr, btr, P(-0.06+pos.X, -0.0562+pos.Y), P(-0.0853+pos.X, -0.0476+pos.Y), layer);
            AddLine(tr, btr, P(-0.0853+pos.X, -0.0476+pos.Y), P(-0.1191+pos.X, -0.0443+pos.Y), layer);
            AddArc(tr, btr, P(0.0585+pos.X, 0.0173+pos.Y), 0.0606, 270.00*DEG, 90.00*DEG, layer);
            AddText(tr, btr, P(-0.0011+pos.X, -0.022+pos.Y), "R", 0.0344, layer);
            AddText(tr, btr, P(0.0093+pos.X, 0.0648+pos.Y), "I", 0.0344, layer);
            AddText(tr, btr, P(0.071+pos.X, 0.0227+pos.Y), "A", 0.0344, layer);
        }
        // --- Simbolo generico (fallback) -------------------------------
        private static void DrawGeneric(Transaction tr, BlockTableRecord btr,
            Point3d pos, string layer, string label)
        {
            AddCircle(tr, btr, pos, 0.075, layer);
            string lbl = (label ?? "").Length > 4 ? label.Substring(0, 4) : (label ?? "?");
            AddText(tr, btr, pos, lbl, 0.050, layer);
        }

        // ================================================================
        //  HELPERS di disegno (riusati da tutti i metodi Draw*)
        // ================================================================

        public static void AddCircle(Transaction tr, BlockTableRecord btr,
            Point3d c, double r, string layer)
        {
            if (r <= 0) return;
            var e = new Circle(c, Vector3d.ZAxis, r) { Layer = layer };
            btr.AppendEntity(e); tr.AddNewlyCreatedDBObject(e, true);
        }

        public static void AddLine(Transaction tr, BlockTableRecord btr,
            Point3d p1, Point3d p2, string layer)
        {
            if (p1.DistanceTo(p2) < 1e-9) return;
            var e = new Line(p1, p2) { Layer = layer };
            btr.AppendEntity(e); tr.AddNewlyCreatedDBObject(e, true);
        }

        public static void AddArc(Transaction tr, BlockTableRecord btr,
            Point3d c, double r, double startAngle, double endAngle, string layer)
        {
            if (r <= 0) return;
            var e = new Arc(c, r, startAngle, endAngle) { Layer = layer };
            btr.AppendEntity(e); tr.AddNewlyCreatedDBObject(e, true);
        }

        public static void AddText(Transaction tr, BlockTableRecord btr,
            Point3d pos, string text, double h, string layer)
        {
            if (string.IsNullOrEmpty(text) || h <= 0) return;
            var t = new DBText
            {
                TextString = text,
                Position = pos,
                Height = h,
                HorizontalMode = TextHorizontalMode.TextCenter,
                AlignmentPoint = pos,
                Layer = layer
            };
            btr.AppendEntity(t); tr.AddNewlyCreatedDBObject(t, true);
        }

        public static void DrawRect(Transaction tr, BlockTableRecord btr,
            Point3d c, double w, double h, string layer)
        {
            var p = new Polyline();
            p.AddVertexAt(0, new Point2d(c.X - w/2, c.Y - h/2), 0, 0, 0);
            p.AddVertexAt(1, new Point2d(c.X + w/2, c.Y - h/2), 0, 0, 0);
            p.AddVertexAt(2, new Point2d(c.X + w/2, c.Y + h/2), 0, 0, 0);
            p.AddVertexAt(3, new Point2d(c.X - w/2, c.Y + h/2), 0, 0, 0);
            p.Closed = true; p.Layer = layer;
            btr.AppendEntity(p); tr.AddNewlyCreatedDBObject(p, true);
        }
    }
}

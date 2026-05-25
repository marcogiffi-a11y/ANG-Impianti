using System.Collections.Generic;

namespace ImplantiAI
{
    public class RoomData
    {
        public string Name { get; set; } = "";
        public string RoomType { get; set; } = "";
        public double Area { get; set; }
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
    }

    public class CircuitData
    {
        public string Type { get; set; } = "";
        public string CableSection { get; set; } = "2.5";
        public int BreakerSize { get; set; } = 16;
        public string BreakerType { get; set; } = "C";
        public double CableLength { get; set; }
        public int LightPoints { get; set; }
        public int SocketPoints { get; set; }
        public string CircuitNumber { get; set; } = "";
        public List<string> Notes { get; set; } = new();
        public List<SymbolPlacement> Symbols { get; set; } = new();
    }

    public class SymbolPlacement
    {
        public string Type { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public string Label { get; set; } = "";
        public string Layer { get; set; } = "";
    }

    public class ProjectData
    {
        public string Name { get; set; } = "";
        public string FilePath { get; set; } = "";
        public List<RoomData> Rooms { get; set; } = new();
        public List<CircuitData> Circuits { get; set; } = new();
    }

    public class UserRule
    {
        public string Rule { get; set; } = "";
        public string Context { get; set; } = "";
        public int UsageCount { get; set; }
    }

    public class GeometryData
    {
        public List<string> Layers { get; set; } = new();
        public List<TextData> Texts { get; set; } = new();
        public int TotalEntities { get; set; }
        public string BoundingBox { get; set; } = "";
    }

    public class TextData
    {
        public string Content { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public string Layer { get; set; } = "";
    }

    public class ChatMessage
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
    }

    // Risposta strutturata da Claude con comandi di disegno
    public class ClaudeResponse
    {
        public string Text { get; set; } = "";
        public bool HasDrawingCommands => Commands != null && Commands.Count > 0;
        public List<DrawCommand>? Commands { get; set; }
        public string? LearnRule { get; set; } // Regola da imparare
    }

    public class DrawCommand
    {
        public string Action { get; set; } = ""; // symbol, route, label, room
        public string SymbolType { get; set; } = "";
        public string Layer { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
        public string Label { get; set; } = "";
        public string CableSection { get; set; } = "";
        public string RoomName { get; set; } = "";
    }

    public static class Logger
    {
        public static void Log(string msg)
        {
            try
            {
                var path = System.IO.Path.Combine(
                    System.Environment.GetFolderPath(
                        System.Environment.SpecialFolder.ApplicationData),
                    "ANGImpianti", "plugin.log");
                System.IO.Directory.CreateDirectory(
                    System.IO.Path.GetDirectoryName(path)!);
                System.IO.File.AppendAllText(path,
                    $"[{System.DateTime.Now:HH:mm:ss}] {msg}\n");
            }
            catch { }
        }
    }
}

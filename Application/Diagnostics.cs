using Raylib_cs;
using RaylibGameFramework.Logging;

namespace CCM.Application;

public sealed class Diagnostics(DebugSettings settings, RotatingFileLogger logger)
{
    public void Event(string message) => logger.Write("INFO", message);
    public void Detail(string message) { if (settings.Enabled) logger.Write("DEBUG", message); }
    public void Error(string message, Exception error) => logger.Write("ERROR", message, error);
    public void Draw(AppState state)
    {
        if (!settings.Enabled) return;
        string text = $"DEBUG | {state} | Startup: {settings.StartupScene}";
        if (settings.ShowFPS) text += $" | {Raylib.GetFPS()} FPS";
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), 24, new Color(0, 0, 0, 210));
        int x=(Raylib.GetScreenWidth()-Raylib.MeasureText(text,16))/2;
        Raylib.DrawText(text, x, 4, 16, Color.Lime);
    }
}

using CCM.Application;
using Raylib_cs;
using RaylibGameFramework.Input;
namespace CCM.Scenes;

public sealed class LoadingScene(AssetSession assets, Diagnostics diagnostics, InputController input,
    Action completed, Action exit, string catalogueFile = "assets.json") : Scene
{
    private bool rendered;
    private string? error;
    public string? Error => error;
    public override void Enter()
    {
        rendered = false;
        error = null;
        try { assets.Begin(catalogueFile); diagnostics.Event("Asset catalogue queued."); }
        catch (Exception exception) { Fail(exception); }
    }
    private void Fail(Exception exception)
    {
        error = exception.Message;
        diagnostics.Error("Required asset loading failed.", exception);
    }
    public override void Update(float seconds)
    {
        if (error is not null)
        {
            if (input.WasPressed("MenuBack")) exit();
            return;
        }
        if (!rendered) return;
        var manager = assets.Manager!;
        // A completed frame displays 100% before routing. No artificial loading timer.
        if (!manager.HasPendingWork) { diagnostics.Event($"Loaded {manager.LoadedCount} assets."); completed(); return; }
        try { manager.ProcessNext(); }
        catch (Exception exception) { Fail(exception); }
    }
    public override void Render()
    {
        Raylib.ClearBackground(new Color(12, 16, 24, 255));
        int width = Math.Max(100, Math.Min(600, Raylib.GetScreenWidth()-64));
        int x = (Raylib.GetScreenWidth()-width)/2, y = Raylib.GetScreenHeight()/2;
        float progress = assets.Manager?.Progress ?? 0;
        Raylib.DrawText(error is null ? $"Loading {progress:P0}" : "Asset loading failed", x, y-48, 28, Color.RayWhite);
        Raylib.DrawRectangle(x, y, width, 16, Color.DarkGray);
        Raylib.DrawRectangle(x, y, (int)(width*progress), 16, Color.SkyBlue);
        if (error is not null)
        {
            Raylib.DrawText("See CCM logs for details. Escape / controller B to exit.", 24, y+40, 18, Color.Orange);
        }
        rendered = true;
    }
}

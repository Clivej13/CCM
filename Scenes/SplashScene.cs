using CCM.Application;
using Raylib_cs;
using RaylibGameFramework.Input;
namespace CCM.Scenes;

public sealed class SplashScene(AppSettings settings, InputController input, Action completed) : Scene
{
    private double elapsed;
    private bool rendered;
    public override void Enter() { elapsed = 0; rendered = false; }
    public override void Update(float seconds)
    {
        elapsed += seconds;
        if (rendered && (elapsed >= settings.SplashSeconds ||
            settings.SkipSplash && (input.WasPressed("MenuConfirm") || input.WasPressed("SkipSplash"))))
            completed();
    }
    public override void Render()
    {
        Raylib.ClearBackground(new Color(12, 16, 24, 255));
        const int size = 80;
        Raylib.DrawText("CCM", (Raylib.GetScreenWidth()-Raylib.MeasureText("CCM", size))/2,
            (Raylib.GetScreenHeight()-size)/2, size, Color.RayWhite);
        rendered = true;
    }
}

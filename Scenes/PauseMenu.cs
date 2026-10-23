using CCM.Application;
using Raylib_cs;
using RaylibGameFramework.Input;
using RaylibGameFramework.Menus;

namespace CCM.Scenes;

/// <summary>The in-game menu uses the same configured menu actions as the main menu.</summary>
internal sealed class PauseMenu
{
    private readonly MenuManager menus;
    public bool IsOpen { get; private set; }

    public PauseMenu(InputController input, InputConfig bindings)
    {
        var config=MenuConfigLoader.Load(Settings.ConfigPath("menus.json"));
        config.StartMenu="Pause";
        menus=new MenuManager(config,input,bindings);
    }

    public void Open()
    {
        menus.ReturnToStartMenu();
        IsOpen=true;
    }

    public void Close() => IsOpen=false;

    public string? Update() => IsOpen ? menus.Update()?.Function : null;

    public void Render()
    {
        if (!IsOpen) return;
        Raylib.DrawRectangle(0,0,Raylib.GetScreenWidth(),Raylib.GetScreenHeight(),
            new Color(226,232,240,245));
        menus.Draw();
        Raylib.DrawText("Start / Esc / B: resume",
            16,Raylib.GetScreenHeight()-26,18,Color.DarkBlue);
    }
}

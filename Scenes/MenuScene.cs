using System.Text.Json;
using CCM.Application;
using CCM.Game;
using Raylib_cs;
using RaylibGameFramework.Configuration;
using RaylibGameFramework.Input;
using RaylibGameFramework.Menus;
namespace CCM.Scenes;

public sealed class MenuScene : Scene
{
    private readonly MenuConfig config;
    private readonly MenuManager menus;
    private readonly DisplayManager display;
    private readonly JsonSettingsStore<DisplaySettings> store;
    private readonly JsonSettingsStore<GameplaySettings> gameplayStore;
    private readonly GameplaySettings gameplay;
    private bool pendingAutoAttack;
    private readonly Diagnostics diagnostics;
    private readonly Action newGame, exit;
    private DisplaySettings pending = new();
    private List<Resolution> resolutions = [];
    private string status = "";

    public MenuScene(InputController input, InputConfig bindings, DisplayManager display,
        JsonSettingsStore<DisplaySettings> store, Diagnostics diagnostics, Action newGame, Action exit,
        JsonSettingsStore<GameplaySettings> gameplayStore, GameplaySettings gameplay)
    {
        this.display = display; this.store = store; this.diagnostics = diagnostics;
        this.newGame = newGame; this.exit = exit;
        this.gameplayStore = gameplayStore; this.gameplay = gameplay;
        config = MenuConfigLoader.Load(Settings.ConfigPath("menus.json"));
        menus = new MenuManager(config, input, bindings);
    }
    private MenuItemDefinition Control(string function) =>
        config.Menus["Settings"].Items.Single(i => i.Function == function);
    public override void Enter() { menus.ReturnToStartMenu(); Refresh(); }
    private void Refresh()
    {
        pending = display.Current;
        pendingAutoAttack = gameplay.AutoAttackSelectedEnemy;
        status = "";
        RefreshOptions();
    }
    private void RefreshOptions()
    {
        resolutions = display.Available(pending.Fullscreen);
        var selected = new Resolution(pending.Width, pending.Height);
        if (!resolutions.Contains(selected)) selected = resolutions.Last();
        pending.Width = selected.Width; pending.Height = selected.Height;
        Control("SetFullscreen").Value = JsonSerializer.SerializeToElement(pending.Fullscreen);
        Control("SetAutoAttack").Value = JsonSerializer.SerializeToElement(pendingAutoAttack);
        var resolution = Control("SetResolution");
        resolution.Options = resolutions.Select(r => r.ToString()).ToList();
        resolution.Value = JsonSerializer.SerializeToElement(selected.ToString());
    }
    public override void Update(float seconds)
    {
        if (Raylib.IsWindowResized() && menus.CurrentMenuName == "Settings")
            Refresh();
        string before = menus.CurrentMenuName;
        var action = menus.Update();
        if (before != menus.CurrentMenuName)
        {
            diagnostics.Detail($"Menu {before} -> {menus.CurrentMenuName}");
            Refresh(); // entering or leaving discards unapplied edits
        }
        if (action is null) return;
        diagnostics.Detail($"Menu action: {action.Function}");
        switch (action.Function)
        {
            case "NewGame": newGame(); break;
            case "Exit": exit(); break;
            case "SetFullscreen":
                pending.Fullscreen = (bool)action.Value!;
                RefreshOptions(); break;
            case "SetAutoAttack":
                pendingAutoAttack = (bool)action.Value!; break;
            case "SetResolution":
                var size = resolutions.Single(r => r.ToString() == (string)action.Value!);
                pending.Width = size.Width; pending.Height = size.Height; break;
            case "Apply":
                try
                {
                    display.Apply(pending);
                    pending = display.Current;
                    store.Save(pending);
                    gameplayStore.Save(new GameplaySettings { AutoAttackSelectedEnemy = pendingAutoAttack });
                    gameplay.AutoAttackSelectedEnemy = pendingAutoAttack;
                    RefreshOptions();
                    status = "Settings saved.";
                    diagnostics.Event($"Display applied: {pending.Width}x{pending.Height}, fullscreen={pending.Fullscreen}");
                }
                catch (Exception error)
                {
                    diagnostics.Error("Could not apply or save display settings.", error);
                    pending = display.Current; RefreshOptions();
                    status = "Could not apply/save settings. See CCM logs.";
                }
                break;
        }
    }
    public override void Render()
    {
        Raylib.ClearBackground(new Color(226, 232, 240, 255));
        menus.Draw();
        if (status.Length > 0)
            Raylib.DrawText(status, 16, Raylib.GetScreenHeight()-24, 18, Color.DarkBlue);
    }
}

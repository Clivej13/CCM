using CCM.Application;
using CCM.Game;
using RaylibGameFramework.Assets;
using RaylibGameFramework.Configuration;
using RaylibGameFramework.Input;
using RaylibGameFramework.Menus;
using Xunit;

namespace CCM.Tests;
public sealed class FoundationTests
{
    [Theory]
    [InlineData(false, "Menu", AppState.MainMenu)]
    [InlineData(false, "TestLevel", AppState.MainMenu)]
    [InlineData(true, "Menu", AppState.MainMenu)]
    [InlineData(true, "TestLevel", AppState.LevelUp)]
    public void EveryModeMustPassBothLoadsSplashAndTitle(bool enabled, string scene, AppState expected)
    {
        var state = new Lifecycle();
        var history = new List<AppState> { state.State };
        state.Changed += (_, next) => history.Add(next);
        Assert.Throws<InvalidOperationException>(() => state.Transition(expected));
        state.Transition(AppState.Splash);
        Assert.Throws<InvalidOperationException>(() => state.Transition(expected));
        state.Transition(AppState.Title);
        Assert.Throws<InvalidOperationException>(() => state.Transition(expected));
        state.Transition(AppState.Loading);
        state.Transition(Lifecycle.Destination(new() { Enabled = enabled, StartupScene = scene }));
        Assert.Equal(new[] { AppState.InitialLoading, AppState.Splash, AppState.Title, AppState.Loading, expected }, history);
    }
    [Fact]
    public void NewGameReturnAndShutdownAreLegalAndShutdownIsTerminal()
    {
        var state = new Lifecycle();
        state.Transition(AppState.Splash); state.Transition(AppState.Title);
        state.Transition(AppState.Loading); state.Transition(AppState.MainMenu);
        state.Transition(AppState.LevelUp); state.Transition(AppState.Game);
        state.Transition(AppState.MainMenu);
        state.Transition(AppState.Shutdown);
        Assert.Throws<InvalidOperationException>(() => state.Transition(AppState.MainMenu));
    }
    [Fact]
    public void SetupCanBeCancelledBeforeGameplay()
    {
        var state=new Lifecycle();
        state.Transition(AppState.Splash);
        state.Transition(AppState.Title);
        state.Transition(AppState.Loading);
        state.Transition(AppState.MainMenu);
        Assert.Throws<InvalidOperationException>(()=>state.Transition(AppState.Game));
        state.Transition(AppState.LevelUp);
        state.Transition(AppState.MainMenu);
        state.Transition(AppState.LevelUp);
        state.Transition(AppState.Game);
        Assert.Equal(AppState.Game,state.State);
    }

    [Fact]
    public void GameplayCanRevisitLevelUpAndReturnToTheSameState()
    {
        var state=new Lifecycle();
        state.Transition(AppState.Splash);
        state.Transition(AppState.Title);
        state.Transition(AppState.Loading);
        state.Transition(AppState.LevelUp);
        state.Transition(AppState.Game);
        state.Transition(AppState.LevelUp);
        state.Transition(AppState.Game);
        Assert.Equal(AppState.Game,state.State);
    }

    [Theory]
    [InlineData(AppState.InitialLoading)]
    [InlineData(AppState.Title)]
    [InlineData(AppState.Splash)]
    [InlineData(AppState.Loading)]
    [InlineData(AppState.MainMenu)]
    [InlineData(AppState.LevelUp)]
    [InlineData(AppState.Game)]
    public void CanCloseFromEveryState(AppState target)
    {
        var state = new Lifecycle();
        foreach (var next in new[] { AppState.Splash, AppState.Title, AppState.Loading,
                     target==AppState.Game ? AppState.LevelUp : target, target })
        {
            if (state.State == target) break;
            state.Transition(next);
        }
        state.Transition(AppState.Shutdown);
        Assert.Equal(AppState.Shutdown, state.State);
    }
    [Theory]
    [InlineData("regular.json", false, AppState.MainMenu)]
    [InlineData("regular-debug.json", true, AppState.MainMenu)]
    [InlineData("test-level-debug.json", true, AppState.LevelUp)]
    public void BundledProfilesResolveCorrectly(string file, bool enabled, AppState destination)
    {
        var config = Settings.Required<DebugConfig>(Settings.ConfigPath(file));
        config.Debug.Validate();
        Assert.Equal(enabled, config.Debug.Enabled);
        Assert.Equal(destination, Lifecycle.Destination(config.Debug));
    }
    [Fact]
    public void RejectsInvalidConfiguration()
    {
        Assert.Throws<InvalidDataException>(() => new DebugSettings { StartupScene = "Invalid" }.Validate());
        Assert.Throws<InvalidDataException>(() => new AppSettings { SplashSeconds = double.NaN }.Validate());
        Assert.Throws<InvalidDataException>(() => new DisplaySettings { Width = -1 }.Validate());
        Assert.Throws<FileNotFoundException>(() => Settings.Required<AppSettings>(Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".json")));
    }
    [Fact]
    public void DisplaySettingsRoundTripAndMalformedSaveIsPreserved()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CCM-tests-"+Guid.NewGuid());
        string path = Path.Combine(directory, "display.json");
        try
        {
            var store = new JsonSettingsStore<DisplaySettings>(path);
            Assert.Null(store.Load());
            var expected = new DisplaySettings { Width = 1920, Height = 1080, Fullscreen = true };
            store.Save(expected);
            Assert.Equal(expected, store.Load());
            File.WriteAllText(path, "{ broken");
            Assert.Throws<System.Text.Json.JsonException>(() => store.Load());
            Assert.Equal("{ broken", File.ReadAllText(path));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Fact]
    public void GameplayPreferenceRoundTrips()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CCM-tests-"+Guid.NewGuid());
        try
        {
            var store = new JsonSettingsStore<GameplaySettings>(Path.Combine(directory,"gameplay.json"));
            Assert.Null(store.Load());
            store.Save(new GameplaySettings { AutoAttackSelectedEnemy = true });
            Assert.True(store.Load()!.AutoAttackSelectedEnemy);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory,true); }
    }

    [Fact]
    public void BundledConfigurationAndCatalogueAreValidWithoutGraphics()
    {
        Settings.Required<AppSettings>(Settings.ConfigPath("application.json")).Validate();
        var menus = MenuConfigLoader.Load(Settings.ConfigPath("menus.json"));
        Assert.Contains(menus.Menus["Settings"].Items,
            item => item.Function == "SetAutoAttack" && item.Type == "Toggle");
        Assert.Equal(new[] { "NewGame", "OpenMenu", "Exit" },
            menus.Menus["Main"].Items.Select(i => i.Function));
        Assert.Contains(menus.Menus["Pause"].Items,
            item=>item.Function=="OpenLevelUp" && item.Type=="Button");
        var input = InputConfigLoader.Load(Settings.ConfigPath("input.json"));
        foreach (string action in new[] { "MenuUp", "MenuDown", "MenuLeft", "MenuRight", "MenuConfirm", "MenuBack" })
        {
            Assert.Contains(input.Bindings, b => b.Action == action && b.Device == "Keyboard");
            Assert.Contains(input.Bindings, b => b.Action == action && b.Device == "GamepadButton");
        }
        var config = AssetConfigLoader.Load(Settings.ConfigPath("assets.json"));
        Assert.Equal(24,config.Assets.Count);
        foreach (var definition in config.Assets)
        {
            Assert.Equal("Model",definition.Type);
            Assert.True(File.Exists(Path.GetFullPath(definition.Path,AppContext.BaseDirectory)),
                $"Missing packaged model asset: {definition.Path}");
        }
        var assets = new AssetManager(config);
        assets.SetRequiredAssets(config.Assets.Select(a => a.Key).ToArray());
        Assert.True(assets.HasPendingWork);
        Assert.Equal(0, assets.LoadedCount);
        Assert.Equal(0f, assets.Progress);
        Assert.Throws<KeyNotFoundException>(() => assets.RequireAsset("missing"));
        // ProcessNext is intentionally reserved for the native smoke run.
    }
    [Fact]
    public void PresentationCatalogueIsSeparateFromDestinationAssets()
    {
        var presentation = AssetConfigLoader.Load(Settings.ConfigPath("presentation-assets.json"));
        var destination = AssetConfigLoader.Load(Settings.ConfigPath("assets.json"));
        Assert.Contains(presentation.Assets, a => a.Key == "title-mark" && a.Type == "Model");
        Assert.Empty(presentation.Assets.Select(a => a.Key).Intersect(destination.Assets.Select(a => a.Key)));
    }
    [Theory]
    [InlineData(InputDeviceFamily.KeyboardMouse, -1)]
    [InlineData(InputDeviceFamily.Gamepad, 2)]
    public void TitleRequiresFreshPressAndReleaseAndKeepsSelectedDevice(InputDeviceFamily family, int gamepad)
    {
        var selection = new DeviceSelection();
        var device = new SelectedDevice(family, gamepad);
        Assert.False(selection.Update(new(device, true))); // held splash input
        Assert.Null(selection.Selected);
        Assert.False(selection.Update(new(null, false))); // arm
        Assert.False(selection.Update(new(null, false))); // idle never confirms
        Assert.False(selection.Update(new(device, true))); // new press
        Assert.False(selection.Update(new(device, true))); // held
        Assert.Null(selection.Selected);
        Assert.True(selection.Update(new(null, false))); // release confirms
        Assert.Equal(device, selection.Selected);
        Assert.False(selection.Update(new(new SelectedDevice(InputDeviceFamily.KeyboardMouse), true)));
        Assert.Equal(device, selection.Selected);
    }
    [Fact]
    public void MissingAssetProducesUsefulDiagnosticBeforeNativeCalls()
    {
        var error = Assert.Throws<InvalidDataException>(() => new AssetManager(new AssetConfig
        {
            Assets = [new() { Key = "missing", Type = "Model", Path = "Assets/does-not-exist.obj" }]
        }));
        Assert.Contains("missing", error.Message);
        Assert.Contains("does-not-exist.obj", error.Message);
    }
}

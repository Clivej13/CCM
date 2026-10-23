using CCM.Scenes;
using CCM.Game;
using Raylib_cs;
using RaylibGameFramework.Configuration;
using RaylibGameFramework.Input;

namespace CCM.Application;

public sealed class GameApplication(AppSettings settings, DebugSettings debug, Diagnostics diagnostics,
    JsonSettingsStore<DisplaySettings> store, DisplaySettings initial,
    JsonSettingsStore<GameplaySettings>? gameplayStore = null, GameplaySettings? gameplaySettings = null)
{
    private readonly Lifecycle lifecycle = new();
    private readonly DisplayManager display = new();
    private readonly AssetSession assets = new();
    private readonly AssetSession presentation = new();
    private readonly Dictionary<AppState, Scene> scenes = [];
    private Scene? active;
    public void Run(bool smoke = false)
    {
        bool window = false;
        try
        {
            Raylib.SetTraceLogLevel(debug.Enabled ? TraceLogLevel.Info : TraceLogLevel.Warning);
            Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint);
            Raylib.InitWindow(initial.Width, initial.Height, settings.Title);
            window = Raylib.IsWindowReady();
            if (!window) throw new InvalidOperationException("Raylib window initialization failed.");
            Raylib.SetWindowMinSize(640, 480);
            Raylib.SetExitKey(KeyboardKey.Null);
            Raylib.SetTargetFPS(settings.TargetFps);
            display.Apply(initial);
            var bindings = InputConfigLoader.Load(Settings.ConfigPath("input.json"));
            var input = new InputController(bindings);
            var selection = new DeviceSelection();
            var dungeon = Settings.Required<CCM.Game.DungeonSettings>(Settings.ConfigPath("dungeon.json"));
            dungeon.Validate();
            var itemCatalogue=Settings.Required<ItemCatalogue>(Settings.ConfigPath("items.json"));
            itemCatalogue.Validate();
            var playerDefinition=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json"));
            playerDefinition.Validate();
            var enemyCatalogue=Settings.Required<EnemyCatalogue>(Settings.ConfigPath("enemies.json"));
            enemyCatalogue.Validate(itemCatalogue);
            enemyCatalogue.Get(dungeon.EnemyId);
            var containerCatalogue=Settings.Required<ContainerCatalogue>(Settings.ConfigPath("containers.json"));
            containerCatalogue.Validate(itemCatalogue);
            var starterChest=containerCatalogue.Get("starter-chest");
            var sessionInput = new SessionInput(bindings, selection, dungeon);
            var gameplay = gameplaySettings ?? new GameplaySettings();
            var gameplaySave = gameplayStore ?? new JsonSettingsStore<GameplaySettings>(
                Path.Combine(Settings.DataDirectory, "gameplay.json"));
            int titleSamples = 0;
            TitleInput ReadTitleInput()
            {
                if (!smoke) return TitleInputReader.Read();
                // Exercise the same selection gate with idle, press and release samples.
                return ++titleSamples == 2
                    ? new TitleInput(new SelectedDevice(InputDeviceFamily.KeyboardMouse), true)
                    : new TitleInput(null, false);
            }
            void Transition(AppState next) => lifecycle.Transition(next);
            scenes[AppState.InitialLoading] = new LoadingScene(presentation, diagnostics, input,
                () => Transition(AppState.Splash), () => Transition(AppState.Shutdown), "presentation-assets.json");
            scenes[AppState.Splash] = new SplashScene(settings, input, () => Transition(AppState.Title));
            scenes[AppState.Title] = new TitleScene(presentation, selection, ReadTitleInput, () =>
            {
                diagnostics.Event($"Selected controls: {selection.Selected}");
                Transition(AppState.Loading);
            });
            scenes[AppState.Loading] = new LoadingScene(assets, diagnostics, input,
                () => Transition(Lifecycle.Destination(debug)), () => Transition(AppState.Shutdown),
                initialKeys:["foundation-triangle"]);
            scenes[AppState.MainMenu] = new MenuScene(input, bindings, display, store, diagnostics,
                () => Transition(AppState.Game), () => Transition(AppState.Shutdown), gameplaySave, gameplay);
            lifecycle.Changed += (previous, next) =>
            {
                active?.Exit();
                diagnostics.Event($"State {previous} -> {next}");
                // Build the dungeon and its catalogue enemy only when gameplay is entered.
                if (next==AppState.Game && !scenes.ContainsKey(AppState.Game))
                    scenes[AppState.Game]=new GameScene(sessionInput,dungeon,debug.Enabled,
                        () => Transition(AppState.MainMenu),input,bindings,gameplay,
                        itemCatalogue,starterChest.Contents,playerDefinition,enemyCatalogue);
                active = scenes.GetValueOrDefault(next);
                active?.Enter();
            };
            active = scenes[AppState.InitialLoading];
            active.Enter();
            diagnostics.Event($"Application started; debug={debug.Enabled}, destination={Lifecycle.Destination(debug)}");
            int destinationFrames = 0, frames = 0;
            while (lifecycle.State != AppState.Shutdown)
            {
                if (Raylib.WindowShouldClose()) { Transition(AppState.Shutdown); break; }
                input.Update();
                sessionInput.Update();
                active?.Update(Raylib.GetFrameTime());
                if (smoke && active is LoadingScene { Error: { } loadingError })
                    throw new InvalidOperationException(loadingError);
                if (lifecycle.State == AppState.Shutdown) break;
                Raylib.BeginDrawing();
                try
                {
                    active?.Render();
                    diagnostics.Draw(lifecycle.State);
                    if (debug.Enabled && selection.Selected is { } selected)
                        Raylib.DrawText($"Controls: {selected}", 8, 28, 16, Color.Lime);
                }
                finally { Raylib.EndDrawing(); }
                if (smoke)
                {
                    if (++frames > 1200) throw new InvalidOperationException("Smoke run did not reach its destination.");
                    if (lifecycle.State is AppState.MainMenu or AppState.Game)
                    {
                        destinationFrames++;
                        if (destinationFrames == 1 && lifecycle.State != Lifecycle.Destination(debug))
                            throw new InvalidOperationException("Incorrect startup destination.");
                        if (destinationFrames is 10 or 20)
                            Transition(lifecycle.State == AppState.Game ? AppState.MainMenu : AppState.Game);
                        if (destinationFrames == 30)
                        {
                            if (assets.Manager?.LoadedCount != assets.Manager?.RequiredCount)
                                throw new InvalidOperationException("Required assets are not resident.");
                            Transition(AppState.Shutdown);
                        }
                    }
                }
            }
        }
        finally
        {
            try
            {
                if (lifecycle.State != AppState.Shutdown) lifecycle.Transition(AppState.Shutdown);
                try { assets.Dispose(); }
                finally { presentation.Dispose(); }
            }
            finally { if (window) Raylib.CloseWindow(); }
        }
        diagnostics.Event("Clean shutdown.");
    }
}

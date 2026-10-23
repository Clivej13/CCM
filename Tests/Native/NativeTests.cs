using System.Numerics;
using CCM.Application;
using CCM.Game;
using Raylib_cs;
using RaylibGameFramework.Configuration;
using RaylibGameFramework.Logging;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace CCM.NativeTests;

public sealed class NativeTests
{
    [Theory]
    [InlineData("regular.json", "MainMenu")]
    [InlineData("regular-debug.json", "MainMenu")]
    [InlineData("test-level-debug.json", "Game")]
    public void RealWindowLoadsAssetsRoutesAndCleansUp(string profile, string destination)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CCM-native-"+Guid.NewGuid());
        var logger = new RotatingFileLogger(directory);
        var debug = Settings.Required<DebugConfig>(Settings.ConfigPath(profile)).Debug;
        var app = Settings.Required<AppSettings>(Settings.ConfigPath("application.json"));
        var display = new DisplaySettings { Width = 800, Height = 600 };
        try
        {
            new GameApplication(app, debug, new Diagnostics(debug, logger),
                new JsonSettingsStore<DisplaySettings>(Path.Combine(directory, "display.json")), display).Run(smoke: true);
            Assert.False(Raylib.IsWindowReady());
            string log = File.ReadAllText(logger.FilePath);
            Assert.Contains("State InitialLoading -> Splash", log);
            Assert.Contains("State Splash -> Title", log);
            Assert.Contains("State Title -> Loading", log);
            Assert.Contains("Selected controls: Keyboard / mouse", log);
            string[] expectedOrder = ["State InitialLoading -> Splash", "State Splash -> Title",
                "Selected controls:", "State Title -> Loading", "State Loading -> "+destination];
            int previousIndex = -1;
            foreach (string entry in expectedOrder)
            {
                int index = log.IndexOf(entry, StringComparison.Ordinal);
                Assert.True(index > previousIndex, $"Out-of-order startup: {entry}");
                previousIndex = index;
            }
            Assert.Contains("Loaded 1 assets.", log);
            Assert.Contains("State Loading -> "+destination, log);
            Assert.Contains("State MainMenu -> Game", log);
            Assert.Contains("State Game -> MainMenu", log);
            Assert.Contains("Clean shutdown.", log);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void TopDownScreenRayReachesTheGroundUnderTheCursor()
    {
        Raylib.InitWindow(800,600,"CCM top-down camera validation");
        try
        {
            var simulation = new DungeonSimulation(new());
            simulation.AdjustZoom(-6);
            var camera = new Camera3D
            {
                Position=simulation.CameraPosition,
                Target=simulation.CameraTarget,
                Up=-Vector3.UnitZ,
                FovY=60,
                Projection=CameraProjection.Perspective
            };
            var ray = Raylib.GetScreenToWorldRay(new(400,300),camera);
            Assert.True(ray.Direction.Y < -0.9f);
            float distance = -ray.Position.Y/ray.Direction.Y;
            var ground = ray.Position+ray.Direction*distance;
            Assert.InRange(Vector3.Distance(ground,simulation.FreeCameraCentre),0,0.05f);
            for (int i=0;i<12;i++) simulation.UpdateTopDown(new(0,1),0.1f);
            camera.Position=simulation.CameraPosition;
            camera.Target=simulation.CameraTarget;
            var door=simulation.World.Interactables[0];
            var screen=Raylib.GetWorldToScreen(door.Bounds.Centre,camera);
            var objectRay=Raylib.GetScreenToWorldRay(screen,camera);
            Assert.Same(door,InteractionQuery.FindVisible(simulation.World,
                objectRay.Position,objectRay.Direction)?.Item);
        }
        finally { Raylib.CloseWindow(); }
    }

    [Fact]
    public void FollowCameraPointerRaySelectsOffCentreEnemy()
    {
        Raylib.InitWindow(800,600,"CCM follow pointer validation");
        try
        {
            var simulation = new DungeonSimulation(new DungeonSettings
                { Combat = new() { AutoPauseOnCombatStart=false } });
            simulation.Player.Position = new(0,0,-6.35f);
            simulation.UpdateAim(new(0,1.2f,0),-Vector3.UnitZ);
            Assert.True(simulation.TryInteractVisible());
            simulation.UpdateCombat(0.1f);
            simulation.UpdateCombat(0.1f);
            simulation.Player.Position = new(0,0,-25);
            simulation.Enemy.Position = new(2,0,-30);
            var camera = new Camera3D
            {
                Position=simulation.CameraPosition,
                Target=simulation.CameraTarget,
                Up=Vector3.UnitY,
                FovY=60,
                Projection=CameraProjection.Perspective
            };
            var screen = Raylib.GetWorldToScreen(simulation.Enemy.Position+new Vector3(0,0.9f,0),camera);
            Assert.True(screen.X > 0 && screen.X < 800);
            Assert.True(screen.Y > 0 && screen.Y < 600);
            Assert.True(Math.Abs(screen.X-400)>20);
            var ray = Raylib.GetScreenToWorldRay(screen,camera);
            Assert.True(simulation.SelectEnemy(ray.Position,ray.Direction));
        }
        finally { Raylib.CloseWindow(); }
    }

    [Fact]
    public void DisplayChangesAndSavedSettingsRoundTripWithRealWindow()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CCM-display-"+Guid.NewGuid());
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
        Raylib.InitWindow(800, 600, "CCM display validation");
        try
        {
            Assert.True(Raylib.IsWindowReady());
            var display = new DisplayManager();
            var windowed = new DisplaySettings { Width = 640, Height = 480 };
            display.Apply(windowed);
            Assert.Equal(windowed, display.Current);
            var size = display.Available(true).Last();
            display.Apply(new() { Width = size.Width, Height = size.Height, Fullscreen = true });
            Assert.True(Raylib.IsWindowFullscreen());
            // Drivers can choose a different actual mode; the application reads it back.
            var actualMode = new Resolution(display.Current.Width, display.Current.Height);
            Assert.Contains(actualMode, display.Available(true));
            display.Apply(windowed);
            Assert.False(Raylib.IsWindowFullscreen());
            Assert.Equal(windowed, display.Current);
            var store = new JsonSettingsStore<DisplaySettings>(Path.Combine(directory, "display.json"));
            store.Save(display.Current);
            Assert.Equal(windowed, store.Load());
        }
        finally
        {
            if (Raylib.IsWindowFullscreen()) Raylib.ToggleFullscreen();
            Raylib.CloseWindow();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}

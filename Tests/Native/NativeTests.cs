using CCM.Application;
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
            Assert.Equal(size.Width, display.Current.Width);
            Assert.Equal(size.Height, display.Current.Height);
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

using RaylibGameFramework.Configuration;

namespace CCM.Application;

public sealed class AppSettings
{
    public string Title { get; set; } = "CCM";
    public int TargetFps { get; set; } = 60;
    public double SplashSeconds { get; set; } = 2;
    public bool SkipSplash { get; set; } = true;
    public DisplaySettings Window { get; set; } = new();
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Title) || TargetFps is < 1 or > 1000 ||
            !double.IsFinite(SplashSeconds) || SplashSeconds is < 0 or > 60 || Window is null)
            throw new InvalidDataException("Invalid application settings.");
        Window.Validate();
    }
}
public sealed class DebugSettings
{
    public bool Enabled { get; set; }
    public string StartupScene { get; set; } = "Menu";
    public bool ShowFPS { get; set; } = true;
    public void Validate()
    {
        if (StartupScene is not ("Menu" or "TestLevel"))
            throw new InvalidDataException("Debug.StartupScene must be Menu or TestLevel.");
    }
}
public sealed class DebugConfig { public DebugSettings Debug { get; set; } = new(); }
public sealed record DisplaySettings
{
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
    public bool Fullscreen { get; set; }
    public void Validate()
    {
        if (Width is < 640 or > 16384 || Height is < 480 or > 16384)
            throw new InvalidDataException("Display size must be at least 640x480 and at most 16384x16384.");
    }
}
public static class Settings
{
    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CCM");
    public static string ConfigPath(string name) => Path.Combine(AppContext.BaseDirectory, "Config", name);
    public static T Required<T>(string path) where T : class =>
        new JsonSettingsStore<T>(path).Load() ?? throw new FileNotFoundException("Required configuration is missing.", path);
}

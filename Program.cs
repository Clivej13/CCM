using CCM.Application;
using CCM.Game;
using RaylibGameFramework.Configuration;
using RaylibGameFramework.Logging;

var logger = new RotatingFileLogger(Path.Combine(Settings.DataDirectory, "Logs"));
try
{
    try { logger = new RotatingFileLogger(Path.Combine(Settings.DataDirectory, "Logs"),
        Settings.Required<FileLogOptions>(Settings.ConfigPath("logging.json"))); }
    catch (Exception error) { logger.Write("ERROR", "Invalid logging configuration; using defaults.", error); }
    using var crashes = new CrashLogging(logger);
    var app = Settings.Required<AppSettings>(Settings.ConfigPath("application.json"));
    app.Validate();
    string debugFile = "debug.json";
    bool smoke = false;
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "--smoke") smoke = true;
        else if (args[i] == "--debug-config" && i+1 < args.Length) debugFile = args[++i];
        else throw new ArgumentException($"Unknown or incomplete option: {args[i]}");
    }
    var debug = Settings.Required<DebugConfig>(Settings.ConfigPath(debugFile)).Debug
        ?? throw new InvalidDataException("Debug settings must be an object.");
    debug.Validate();
    var diagnostics = new Diagnostics(debug, logger);
    var store = new JsonSettingsStore<DisplaySettings>(Path.Combine(Settings.DataDirectory, "display.json"));
    var initial = app.Window;
    try { initial = store.Load() ?? initial; initial.Validate(); }
    catch (Exception error)
    {
        diagnostics.Error("Invalid saved display settings; using application defaults. The file was preserved.", error);
        initial = app.Window;
    }
    var gameplayStore = new JsonSettingsStore<GameplaySettings>(Path.Combine(Settings.DataDirectory, "gameplay.json"));
    GameplaySettings gameplay;
    try { gameplay = gameplayStore.Load() ?? new GameplaySettings(); }
    catch (Exception error)
    {
        diagnostics.Error("Invalid saved gameplay settings; using defaults. The file was preserved.", error);
        gameplay = new GameplaySettings();
    }
    new GameApplication(app, debug, diagnostics, store, initial, gameplayStore, gameplay).Run(smoke);
    return 0;
}
catch (Exception error)
{
    logger.Write("FATAL", "CCM could not continue.", error);
    Console.Error.WriteLine($"CCM: {error.Message}\nLog: {logger.FilePath}");
    return 1;
}

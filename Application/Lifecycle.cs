namespace CCM.Application;

public enum AppState { InitialLoading, Splash, Title, Loading, MainMenu, LevelUp, Game, Shutdown }

public sealed class Lifecycle
{
    public AppState State { get; private set; } = AppState.InitialLoading;
    public static AppState Destination(DebugSettings debug) =>
        debug.Enabled && debug.StartupScene == "TestLevel" ? AppState.LevelUp : AppState.MainMenu;
    public event Action<AppState, AppState>? Changed;
    public void Transition(AppState next)
    {
        if (next == State) return;
        bool allowed = next == AppState.Shutdown || (State, next) switch
        {
            (AppState.InitialLoading, AppState.Splash) => true,
            (AppState.Splash, AppState.Title) => true,
            (AppState.Title, AppState.Loading) => true,
            (AppState.Loading, AppState.MainMenu or AppState.LevelUp) => true,
            (AppState.MainMenu, AppState.LevelUp) => true,
            (AppState.LevelUp, AppState.Game or AppState.MainMenu) => true,
            (AppState.Game, AppState.MainMenu) => true,
            _ => false
        };
        if (State == AppState.Shutdown || !allowed)
            throw new InvalidOperationException($"Invalid transition: {State} -> {next}");
        var previous = State;
        State = next;
        Changed?.Invoke(previous, next);
    }
}

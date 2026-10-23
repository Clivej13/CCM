using CCM.Application;
using CCM.Game;
using Raylib_cs;
namespace CCM.Scenes;

public sealed class GameScene : Scene
{
    private readonly SessionInput input;
    private readonly DungeonSettings settings;
    private readonly Action returnToMenu;
    private readonly DungeonSimulation simulation;
    private readonly GameplayTargeting targeting;
    private readonly DungeonRenderer renderer;
    private readonly GameplayHud hud;
    private readonly IGameplayControls mouseFollow,mouseTopDown,gamepadFollow,gamepadTopDown;
    private IGameplayControls activeControls;
    private bool ignoreLook,wasTopDown;
    private string? feedback;
    private float feedbackSeconds;

    public GameScene(SessionInput input,DungeonSettings settings,bool debug,
        Action returnToMenu,GameplaySettings? gameplay=null)
    {
        this.input=input;
        this.settings=settings;
        this.returnToMenu=returnToMenu;
        simulation=new(settings,gameplay);
        targeting=new(simulation,settings);
        renderer=new(simulation,debug);
        hud=new(input,simulation,targeting,debug);
        mouseFollow=new MouseFollowControls(input,simulation,targeting);
        mouseTopDown=new MouseTopDownControls(input,simulation,targeting);
        gamepadFollow=new GamepadFollowControls(input,simulation);
        gamepadTopDown=new GamepadTopDownControls(input,simulation,targeting);
        activeControls=SelectControls();
    }

    private IGameplayControls SelectControls() => (input.UsesMouse,simulation.TopDown) switch
    {
        (true,false) => mouseFollow,
        (true,true) => mouseTopDown,
        (false,false) => gamepadFollow,
        _ => gamepadTopDown
    };

    public override void Enter()
    {
        simulation.Reset();
        targeting.Reset();
        feedback=null;
        feedbackSeconds=0;
        ignoreLook=true;
        wasTopDown=false;
        activeControls=SelectControls();
        activeControls.Enter();
        activeControls.BeforeFrame();
    }

    public override void Update(float seconds)
    {
        if (activeControls.BeforeFrame()) ignoreLook=true;
        if (!Raylib.IsWindowFocused()) { activeControls.CancelPendingAction(); return; }
        feedbackSeconds=Math.Max(0,feedbackSeconds-seconds);
        if (input.ReturnPressed()) { returnToMenu(); return; }
        if (input.PausePressed())
        {
            simulation.TogglePause();
            ignoreLook=true;
        }
        if (simulation.Paused) { activeControls.CancelPendingAction(); return; }

        simulation.AdjustZoom(input.Zoom*settings.MouseZoomStep);
        if (!input.UsesMouse && input.CycleViewPressed()) simulation.CycleView();
        if (simulation.TopDown!=wasTopDown)
        {
            wasTopDown=simulation.TopDown;
            activeControls.Exit();
            activeControls=SelectControls();
            activeControls.Enter();
            targeting.Reset();
            ignoreLook=true;
        }
        if (activeControls.BeforeFrame()) ignoreLook=true;
        simulation.ClearInteractionMessage();
        activeControls.Move(seconds,ignoreLook);
        targeting.Update(input,seconds);
        bool combatBeforeFrame=simulation.CombatStarted;
        activeControls.Act(ignoreLook);
        simulation.UpdateCombat(seconds,controllerScheme:!input.UsesMouse);
        if (!combatBeforeFrame && simulation.CombatStarted)
            targeting.OnCombatStarted(input.UsesMouse);
        if (simulation.InteractionMessage is { } interaction)
            { feedback=interaction; feedbackSeconds=1.5f; }
        if (simulation.CombatMessage is { } combat)
            { feedback=combat; feedbackSeconds=1.5f; }
        ignoreLook=false;
    }

    public override void Exit() => activeControls.Exit();

    public override void Render()
    {
        renderer.Render();
        hud.Render(feedback,feedbackSeconds);
    }
}

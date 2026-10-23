using System.Numerics;
using CCM.Application;
using CCM.Game;
using Raylib_cs;

namespace CCM.Scenes;

/// <summary>One layout maps owned input to the shared dungeon simulation.</summary>
public interface IGameplayControls
{
    void Enter() { }
    void Exit() { }
    bool BeforeFrame() => false;
    void CancelPendingAction() { }
    void Move(float seconds,bool ignoreLook);
    void Act(bool ignoreLook);
}

public sealed class MouseFollowControls(SessionInput input,DungeonSimulation simulation,
    GameplayTargeting targeting) : IGameplayControls
{
    private bool captured,dragged,clickPending;
    private Vector2 start;
    private float dragDistance;

    public void Enter()
    {
        captured = dragged = clickPending = false;
        dragDistance = 0;
    }

    public bool BeforeFrame()
    {
        bool capture = Raylib.IsWindowFocused() && input.AttackHeld;
        if (capture==captured) return false;
        if (capture)
        {
            start = Raylib.GetMousePosition();
            dragged = false;
            dragDistance = 0;
            Raylib.DisableCursor();
        }
        else
        {
            Raylib.EnableCursor();
            Raylib.SetMousePosition((int)start.X,(int)start.Y);
            clickPending = Raylib.IsWindowFocused() && !input.AttackHeld && !dragged;
        }
        captured = capture;
        return true;
    }

    public void Move(float seconds,bool ignoreLook)
    {
        if (captured && !ignoreLook)
        {
            dragDistance += input.Look.Length();
            if (dragDistance>6) dragged = true;
        }
        var look = ignoreLook || !captured ? Vector2.Zero : input.Look;
        var move = input.Move;
        float turn = captured ? 0 : move.X;
        move.X = input.Strafe+(captured ? move.X : 0);
        simulation.Update(move,look,true,seconds,turn,faceCamera:true);
    }

    public void CancelPendingAction() => clickPending=false;

    public void Act(bool ignoreLook)
    {
        if (ignoreLook && !clickPending) return;
        bool primary = !ignoreLook && input.SelectPressed();
        if (primary) targeting.SelectEnemy();
        if (!ignoreLook && !primary && input.InteractPressed()) simulation.TryInteractVisible();
        if (clickPending)
        {
            if (!simulation.TryInteractVisible())
            {
                targeting.SelectEnemy();
                simulation.RequestAttack();
            }
        }
        clickPending = false;
    }

    public void Exit()
    {
        if (captured)
        {
            Raylib.EnableCursor();
            Raylib.SetMousePosition((int)start.X,(int)start.Y);
        }
        captured = clickPending = false;
    }
}

public sealed class GamepadFollowControls(SessionInput input,DungeonSimulation simulation)
    : IGameplayControls
{
    public void Move(float seconds,bool ignoreLook) =>
        simulation.Update(input.Move,ignoreLook ? Vector2.Zero : input.Look,false,seconds);
    public void Act(bool ignoreLook)
    {
        if (!ignoreLook && input.InteractPressed()) simulation.ActivateContext();
    }
}

public sealed class MouseTopDownControls(SessionInput input,DungeonSimulation simulation,
    GameplayTargeting targeting) : IGameplayControls
{
    public void Move(float seconds,bool ignoreLook) =>
        simulation.UpdateTopDown(input.Move,seconds);

    public void Act(bool ignoreLook)
    {
        if (ignoreLook) return;
        bool primary = input.SelectPressed();
        if (primary && !simulation.TryInteractVisible())
        {
            if (targeting.SelectEnemy()) simulation.RequestAttack();
            else targeting.MoveToGround();
        }
        if (!primary && input.InteractPressed()) simulation.TryInteractVisible();
        if (input.AttackPressed()) targeting.ApproachOrAttack();
    }
}

public sealed class GamepadTopDownControls(SessionInput input,DungeonSimulation simulation,
    GameplayTargeting targeting) : IGameplayControls
{
    public void Move(float seconds,bool ignoreLook) =>
        simulation.UpdateTopDown(input.Move,seconds);

    public void Act(bool ignoreLook)
    {
        if (ignoreLook || !input.InteractPressed()) return;
        if (targeting.ControllerContextMode)
        {
            simulation.ActivateContext();
            return;
        }
        if (targeting.SelectEnemy()) simulation.RequestAttack();
        else if (simulation.VisibleTarget is { } visible)
            simulation.RequestInteraction(visible.Item);
        else targeting.MoveToGround();
    }
}

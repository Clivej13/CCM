using System.Numerics;
using CCM.Application;
using Raylib_cs;

namespace CCM.Game;

/// <summary>Owns pointer aim, the gamepad cursor and contextual action selection.</summary>
public sealed class GameplayTargeting(DungeonSimulation simulation, DungeonSettings settings)
{
    public Vector2 ControllerCursor { get; private set; }
    public bool ControllerContextMode { get; private set; }
    public Ray Aim { get; private set; }

    public void Reset()
    {
        ControllerContextMode = false;
        ControllerCursor = new(Raylib.GetScreenWidth()/2f,Raylib.GetScreenHeight()/2f);
        Aim = default;
    }

    public void Update(SessionInput input,float seconds)
    {
        if (!input.UsesMouse && simulation.TopDown)
        {
            ControllerCursor += input.Look*settings.ControllerCursorSpeed*seconds;
            ControllerCursor = Vector2.Clamp(ControllerCursor,Vector2.Zero,
                new(Raylib.GetScreenWidth()-1,Raylib.GetScreenHeight()-1));
            if (input.Look.LengthSquared()>0.01f) ControllerContextMode = false;
        }

        if (input.UsesMouse || simulation.TopDown && !ControllerContextMode)
        {
            Vector2 screenPoint = input.UsesMouse ? Raylib.GetMousePosition() : ControllerCursor;
            Aim = Raylib.GetScreenToWorldRay(screenPoint,simulation.Camera.ToRaylibCamera());
            simulation.UpdateAim(Aim.Position,Aim.Direction);
        }
        else
        {
            Aim = default;
            simulation.UpdateContextAim();
        }

        if (input.UsesMouse) return;
        bool left = input.CycleActionLeftPressed();
        bool right = input.CycleActionRightPressed();
        if (left || right)
            ControllerContextMode = simulation.CycleContext(left ? -1 : 1);
    }

    public void OnCombatStarted(bool usesMouse)
    {
        if (!usesMouse) ControllerContextMode = simulation.TopDown;
    }

    public void MoveToGround()
    {
        if (Aim.Direction.Y>=-0.0001f) return;
        float distance = -Aim.Position.Y/Aim.Direction.Y;
        if (distance>0) simulation.SetMoveDestination(Aim.Position+Aim.Direction*distance);
    }

    public bool SelectEnemy()
        => simulation.SelectEnemy(Aim.Position,Aim.Direction);

    public void ApproachOrAttack()
    {
        if (SelectEnemy()) simulation.RequestAttack();
        else if (simulation.VisibleTarget is { } visible)
            simulation.RequestInteraction(visible.Item);
    }
}

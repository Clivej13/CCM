using System.Numerics;
using CCM.Game;
using Xunit;

namespace CCM.Tests;

public sealed class CameraAndNavigationTests
{
    [Fact]
    public void ZoomSwitchesToFreeCameraAndPanningDoesNotMovePlayer()
    {
        var simulation = new DungeonSimulation(new());
        var spawn = simulation.Player.Position;
        simulation.AdjustZoom(-6);
        Assert.True(simulation.TopDown);
        simulation.UpdateTopDown(new(1,0),0.1f);
        simulation.UpdateTopDown(new(0,1),0.1f);
        Assert.Equal(spawn,simulation.Player.Position);
        Assert.InRange(simulation.FreeCameraCentre.X,0.99f,1.01f);
        Assert.InRange(simulation.FreeCameraCentre.Z,2.99f,3.01f);
        simulation.AdjustZoom(2);
        Assert.False(simulation.TopDown);
        Assert.Equal(9,simulation.ZoomDistance);
    }

    [Fact]
    public void ControllerViewButtonCyclesCloseWideAndFreeTopDown()
    {
        var simulation = new DungeonSimulation(new());
        Assert.Equal(5f,simulation.ZoomDistance);
        simulation.CycleView();
        Assert.Equal(8f,simulation.ZoomDistance);
        Assert.False(simulation.TopDown);
        simulation.CycleView();
        Assert.Equal(14f,simulation.ZoomDistance);
        Assert.True(simulation.TopDown);
        simulation.CycleView();
        Assert.Equal(5f,simulation.ZoomDistance);
        Assert.False(simulation.TopDown);
    }

    [Fact]
    public void ClickMovementRoutesThroughOpenDoorButNotClosedDoor()
    {
        var simulation = new DungeonSimulation(new());
        simulation.AdjustZoom(-6);
        Assert.False(simulation.SetMoveDestination(new(0,0,-30)));
        Assert.True(simulation.World.Interactables[0].Activate(simulation.Player.Position));
        Assert.True(simulation.SetMoveDestination(new(0,0,-30)));
        var camera = simulation.FreeCameraCentre;
        for (int i=0;i<400 && simulation.MoveDestination is not null;i++)
            simulation.UpdateTopDown(Vector2.Zero,0.1f);
        Assert.Null(simulation.MoveDestination);
        Assert.InRange(simulation.Player.Position.Z,-30.1f,-29.9f);
        Assert.Equal(camera,simulation.FreeCameraCentre);
    }

    [Fact]
    public void TopDownObjectCommandWalksIntoReachAndActivatesDoor()
    {
        var simulation = new DungeonSimulation(new());
        simulation.AdjustZoom(-6);
        var door = simulation.World.Interactables[0];
        Assert.True(simulation.RequestInteraction(door));
        Assert.NotNull(simulation.MoveDestination);
        Assert.False(door.IsOpen);
        for (int i=0;i<300 && !door.IsOpen;i++)
            simulation.UpdateTopDown(Vector2.Zero,0.1f);
        Assert.True(door.IsOpen);
        Assert.Null(simulation.MoveDestination);
        Assert.False(simulation.CombatStarted); // opening alone does not enter combat
        Assert.Equal("Door opened",simulation.InteractionMessage);
    }

    [Fact]
    public void TopDownObjectCommandAlsoActivatesTheBox()
    {
        var simulation = new DungeonSimulation(new());
        simulation.AdjustZoom(-6);
        var box = simulation.World.Interactables[1];
        Assert.True(simulation.RequestInteraction(box));
        for (int i=0;i<300 && !box.IsOpen;i++)
            simulation.UpdateTopDown(Vector2.Zero,0.1f);
        Assert.True(box.IsOpen, $"Player {simulation.Player.Position}; destination {simulation.MoveDestination}; message {simulation.InteractionMessage}");
        Assert.False(simulation.World.Interactables[0].IsOpen);
        Assert.Equal("Chest opened",simulation.InteractionMessage);
    }

    [Fact]
    public void TopDownObjectCommandCanBeReplacedAndCannotCrossClosedDoor()
    {
        var simulation = new DungeonSimulation(new());
        simulation.AdjustZoom(-6);
        var box = simulation.World.Interactables[1];
        Assert.True(simulation.RequestInteraction(box));
        Assert.False(simulation.SetMoveDestination(new(0,0,-30)));
        for (int i=0;i<100;i++) simulation.UpdateTopDown(Vector2.Zero,0.1f);
        Assert.False(box.IsOpen);
        Assert.True(simulation.RequestInteraction(box));
        Assert.True(simulation.SetMoveDestination(new(0,0,6)));
        for (int i=0;i<100;i++) simulation.UpdateTopDown(Vector2.Zero,0.1f);
        Assert.False(box.IsOpen);

        simulation.Player.Position = new(0,0,-20);
        Assert.False(simulation.RequestInteraction(box));
        Assert.Equal("No clear route to chest",simulation.InteractionMessage);
        Assert.False(box.IsOpen);
    }

    [Fact]
    public void EnemySelectionHasNoDistanceLimitButInteractionDoes()
    {
        var simulation = new DungeonSimulation(new DungeonSettings
            { Combat = new() { AutoPauseOnCombatStart=false } });
        simulation.UpdateAim(new(0,1.2f,0),-Vector3.UnitZ);
        Assert.Same(simulation.World.Interactables[0],simulation.VisibleTarget?.Item);
        Assert.Null(simulation.Target);
        Assert.True(simulation.TryInteractVisible());
        Assert.False(simulation.World.Interactables[0].IsOpen);
        Assert.Equal("Move closer to interact",simulation.InteractionMessage);

        simulation.Player.Position = new(0,0,-6.35f);
        simulation.UpdateAim(new(0,1.2f,0),-Vector3.UnitZ);
        Assert.Same(simulation.World.Interactables[0],simulation.Target?.Item);
        Assert.True(simulation.TryInteractVisible());
        simulation.UpdateCombat(0.1f);
        simulation.UpdateCombat(0.1f);
        Assert.True(simulation.CombatStarted);
        Assert.True(simulation.SelectEnemy(new(0,1,-5),-Vector3.UnitZ));
        Assert.True(Vector3.Distance(simulation.Player.Position,simulation.Enemy.Position)>20);
    }

    [Fact]
    public void FollowCameraCanAimAtAndSelectAnEnemyAcrossBothRooms()
    {
        var simulation = ActiveCombat();
        simulation.Update(Vector2.Zero,new(0,-170),true,0.1f);
        Assert.InRange(simulation.Pitch,-0.061f,-0.059f);
        Assert.True(Vector3.Distance(simulation.Player.Position,simulation.Enemy.Position)>20);
        Assert.True(simulation.SelectEnemy());
    }

    [Fact]
    public void TopDownAttackRoutesIntoMeleeReachAndContinuesAttacking()
    {
        var simulation = ActiveCombat();
        simulation.SelectEnemy(new(0,1,-5),-Vector3.UnitZ);
        Assert.True(simulation.EnemySelected);
        simulation.AdjustZoom(-6);
        var start = simulation.Player.Position;
        simulation.RequestAttack();
        simulation.UpdateTopDown(Vector2.Zero,0.1f);
        Assert.NotNull(simulation.MoveDestination);
        for (int i=0;i<180 && simulation.Enemy.Alive && simulation.Player.Alive;i++)
        {
            simulation.UpdateTopDown(Vector2.Zero,0.1f);
            simulation.UpdateCombat(0.1f);
        }
        Assert.True(simulation.Player.Position.Z < start.Z-1);
        Assert.True(simulation.Enemy.Health < simulation.Enemy.MaxHealth);
    }

    private static DungeonSimulation ActiveCombat()
    {
        var simulation = new DungeonSimulation(new DungeonSettings
            { Combat = new() { AutoPauseOnCombatStart=false } });
        simulation.Player.Position = new(0,0,-6.35f);
        simulation.UpdateAim(new(0,1.2f,0),-Vector3.UnitZ);
        Assert.True(simulation.TryInteractVisible());
        simulation.UpdateCombat(0.1f);
        simulation.UpdateCombat(0.1f);
        Assert.True(simulation.CombatStarted);
        return simulation;
    }

    [Fact]
    public void InvalidCameraModeTuningIsRejected()
    {
        Assert.Throws<InvalidDataException>(()=>new DungeonSettings { TopDownThreshold=5 }.Validate());
        Assert.Throws<InvalidDataException>(()=>new DungeonSettings { MaxZoomDistance=float.NaN }.Validate());
    }
}

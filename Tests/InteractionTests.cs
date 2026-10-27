using System.Numerics;
using CCM.Game;
using Xunit;

namespace CCM.Tests;

public sealed class InteractionTests
{
    [Fact]
    public void ClosedDoorBlocksCorridorAndOpenDoorConnectsBothRooms()
    {
        var world = new DungeonWorld();
        var door = world.Interactables[0];
        var stopped = world.Move(world.Spawn, new(0,0,-40));
        Assert.InRange(stopped.Z,-8.02f,-7.99f);
        Assert.False(world.CanStand(new(0,0,-8.5f)));

        Assert.True(door.Activate(stopped));
        Assert.True(door.IsOpen);
        var farRoom = world.Move(stopped,new(0,0,-40));
        Assert.InRange(farRoom.Z,-39.65f,-39.5f);
        Assert.True(world.CanStand(farRoom));
    }

    [Fact]
    public void DoorCannotCloseOnPlayerAndSceneResetRestoresBothObjects()
    {
        var world = new DungeonWorld();
        var door = world.Interactables[0];
        var box = world.Interactables[1];
        Assert.True(door.Activate(world.Spawn));
        Assert.False(door.Activate(new(0,0,-8.5f)));
        Assert.True(door.IsOpen);
        Assert.True(box.Activate(world.Spawn));
        Assert.True(box.IsOpen);
        Assert.False(world.CanStand(new(3.1f,0,-7.2f)));
        world.Reset();
        Assert.False(door.IsOpen);
        Assert.False(box.IsOpen);
        Assert.False(world.CanStand(new(0,0,-8.5f)));
    }

    [Fact]
    public void CentreRaySelectsDoorOrBoxAndRequiresReach()
    {
        var world = new DungeonWorld();
        var forward = new Vector3(0,0,-1);
        var door = InteractionQuery.Find(world,new(0,1.2f,-6.4f),new(0,1.2f,-4),forward,2.5f);
        Assert.Same(world.Interactables[0],door?.Item);
        Assert.Null(InteractionQuery.Find(world,new(0,1.2f,-4),new(0,1.2f,-4),forward,2.5f));

        var box = InteractionQuery.Find(world,new(3.1f,1.2f,-5.2f),new(3.1f,1.2f,-3),forward,2.5f);
        Assert.Same(world.Interactables[1],box?.Item);
        Assert.Null(InteractionQuery.Find(world,new(3.1f,1.2f,-5.2f),new(3.1f,1.2f,-3),Vector3.UnitX,2.5f));
    }

    [Fact]
    public void ControllerContextPicksReachableObjectByFacingWithoutScreenCursor()
    {
        var simulation = new DungeonSimulation(new DungeonSettings
            { Combat = new() { AutoPauseOnCombatStart=false } });
        simulation.Player.Position = new(0,0,-6.35f);
        simulation.UpdateContextAim();
        Assert.Same(simulation.World.Interactables[0],simulation.Target?.Item);
        Assert.True(simulation.TryInteractVisible());
        Assert.False(simulation.CombatStarted);
        simulation.UpdateCombat(0.1f);
        simulation.UpdateCombat(0.1f);
        Assert.True(simulation.CombatStarted);
        Assert.True(simulation.World.Interactables[0].IsOpen);

        var overlapWorld = new DungeonWorld();
        var overlapEye = new Vector3(1.8f,1.2f,-6);
        Assert.Same(overlapWorld.Interactables[0],
            InteractionQuery.FindNearby(overlapWorld,overlapEye,-Vector3.UnitZ,2.5f)?.Item);
        Assert.Same(overlapWorld.Interactables[1],
            InteractionQuery.FindNearby(overlapWorld,overlapEye,
                Vector3.Normalize(new Vector3(0.6f,0,-0.5f)),2.5f)?.Item);

        simulation.Player.Position = new(3.1f,0,-5.2f);
        simulation.UpdateContextAim();
        Assert.Same(simulation.World.Interactables[1],simulation.Target?.Item);
        Assert.True(simulation.TryInteractVisible());
        Assert.True(simulation.World.Interactables[1].IsOpen);

        simulation.Update(Vector2.Zero,new(MathF.PI/0.003f,0),true,0.1f);
        simulation.UpdateContextAim();
        Assert.Null(simulation.Target);
        Assert.True(Vector3.Distance(simulation.Player.Position,simulation.Enemy.Position)>20);
        Assert.True(simulation.SelectEnemyContextual());
        Assert.True(simulation.EnemySelected);
    }

    [Fact]
    public void ControllerCanCycleReachableActionsAndKeepChoiceWhileTurning()
    {
        var simulation = new DungeonSimulation(new());
        simulation.Player.Position = new(1.8f,0,-6);
        simulation.UpdateContextAim();
        Assert.Same(simulation.World.Interactables[0],simulation.Target?.Item);
        Assert.True(simulation.CycleContext(1));
        Assert.Same(simulation.World.Interactables[1],simulation.Target?.Item);
        simulation.UpdateContextAim();
        Assert.Same(simulation.World.Interactables[1],simulation.Target?.Item);
        Assert.True(simulation.CycleContext(-1));
        Assert.Same(simulation.World.Interactables[0],simulation.Target?.Item);
        simulation.Player.Position = simulation.World.Spawn;
        simulation.UpdateContextAim();
        Assert.Null(simulation.Target);
    }

    [Fact]
    public void GameplayCameraCanActuallyOpenTheDoor()
    {
        var simulation = new DungeonSimulation(new());
        for (int i=0; i<23; i++)
            simulation.Update(new(0,1),Vector2.Zero,true,0.1f);
        simulation.UpdateInteraction(false);
        Assert.True(simulation.Target is not null, $"Player {simulation.Player.Position} Camera {simulation.CameraPosition} Target {simulation.CameraTarget}");
        Assert.Same(simulation.World.Interactables[0],simulation.Target?.Item);
        simulation.UpdateInteraction(true);
        Assert.True(simulation.World.Interactables[0].IsOpen);
        Assert.Equal("Door opened",simulation.InteractionMessage);
        Assert.False(simulation.CombatStarted);
        Assert.False(simulation.Paused);
        simulation.UpdateCombat(0.1f);
        simulation.UpdateCombat(0.1f);
        Assert.True(simulation.CombatStarted);
        Assert.True(simulation.Paused);
    }

    [Fact]
    public void GameplayCameraCanChooseTheBoxBesideTheDoor()
    {
        var simulation = new DungeonSimulation(new());
        // Go around the low block near spawn, then approach the box from the side.
        for (int i=0; i<12; i++)
            simulation.Update(new(1,0),Vector2.Zero,true,0.1f);
        for (int i=0; i<21; i++)
            simulation.Update(new(0,1),Vector2.Zero,true,0.1f);
        for (int i=0; i<5; i++)
            simulation.Update(new(-1,0),Vector2.Zero,true,0.1f);
        simulation.UpdateInteraction(false);
        Assert.True(simulation.Target is not null, $"Player {simulation.Player.Position} Camera {simulation.CameraPosition} Target {simulation.CameraTarget}");
        Assert.Same(simulation.World.Interactables[1],simulation.Target?.Item);
        simulation.UpdateInteraction(true);
        Assert.True(simulation.World.Interactables[1].IsOpen);
        Assert.False(simulation.World.Interactables[0].IsOpen);
        Assert.Equal("Chest opened",simulation.InteractionMessage);
    }

    [Fact]
    public void WorldGeometryBlocksInteractionEvenWhenCameraSeesTarget()
    {
        var world = new DungeonWorld();
        var target = InteractionQuery.Find(world,new(3.1f,1.2f,-9),new(3.1f,1.2f,-4),new(0,0,-1),5);
        Assert.Null(target);
    }

    [Fact]
    public void FireplaceCanBeReachedExtinguishedAndRelit()
    {
        var world=new DungeonWorld();
        var fireplace=world.Fireplaces[2];
        var eye=new Vector3(5,1.2f,-37.5f);
        Assert.False(world.CanStand(fireplace.Position));
        Assert.Same(fireplace,InteractionQuery.FindNearby(world,eye,-Vector3.UnitZ,2.5f)?.Item);
        Assert.Equal("Extinguish fireplace",fireplace.Action);
        Assert.True(fireplace.Activate(new(5,0,-37.5f)));
        Assert.False(fireplace.IsLit);
        Assert.Equal("Light fireplace",fireplace.Action);
        Assert.True(fireplace.Activate(new(5,0,-37.5f)));
        Assert.True(fireplace.IsLit);
        fireplace.Activate(new(5,0,-37.5f));
        world.Reset();
        Assert.True(fireplace.IsLit);
    }
}

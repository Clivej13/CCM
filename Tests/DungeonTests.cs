using System.Numerics;
using CCM.Application;
using CCM.Game;
using Raylib_cs;
using RaylibGameFramework.Input;
using Xunit;
namespace CCM.Tests;

public sealed class DungeonTests
{
    [Fact]
    public void MovementIsCameraRelativeAndDiagonalSpeedIsLimited()
    {
        var straight = new DungeonSimulation(new());
        var diagonal = new DungeonSimulation(new());
        straight.Update(new(0,1),Vector2.Zero,true,0.1f);
        diagonal.Update(new(1,1),Vector2.Zero,true,0.1f);
        Assert.InRange(Vector3.Distance(straight.Player.Position,straight.World.Spawn),0.449f,0.451f);
        Assert.InRange(Vector3.Distance(diagonal.Player.Position,diagonal.World.Spawn),0.449f,0.451f);
        var turned = new DungeonSimulation(new());
        turned.Update(new(0,1),new(MathF.PI/2/0.003f,0),true,0.1f);
        Assert.InRange(turned.Player.Position.X,0.449f,0.451f);
        Assert.InRange(turned.Player.Position.Z,3.999f,4.001f);
        Assert.InRange(turned.Player.Forward.X,0.999f,1.001f);
    }
    [Fact]
    public void KeyboardTurnRotatesCameraAndCharacterWithoutMoving()
    {
        var simulation = new DungeonSimulation(new());
        var start = simulation.Player.Position;
        simulation.Update(Vector2.Zero,Vector2.Zero,true,0.1f,turn:1);
        Assert.Equal(start,simulation.Player.Position);
        Assert.InRange(simulation.Yaw,-0.241f,-0.239f);
        Assert.InRange(simulation.Player.Forward.X,0.23f,0.25f);
        simulation.Update(new(1,0),Vector2.Zero,true,0.1f,faceCamera:true);
        Assert.True(simulation.Player.Position.X>start.X);
        Assert.InRange(simulation.Player.Forward.X,0.23f,0.25f);
    }

    [Fact]
    public void WallsStopLargeDisplacementsAndAllowSliding()
    {
        var world = new DungeonWorld();
        var hit = world.Move(new(7,0,4),new(50,0,0));
        Assert.InRange(hit.X,7.5f,7.65f);
        Assert.True(world.CanStand(hit));
        var slid = world.Move(hit,new(2,0,-2));
        Assert.True(slid.Z < hit.Z-1.8f);
        Assert.True(world.CanStand(slid));
        Assert.Equal(0,slid.Y);
    }
    [Fact]
    public void CorridorDoorIsPassableButEndWallAndObstaclesBlock()
    {
        var world = new DungeonWorld();
        Assert.False(world.CanStand(new(0,0,-8.5f)));
        Assert.True(world.Interactables[0].Activate(world.Spawn));
        var end = world.Move(world.Spawn,new(0,0,-60));
        Assert.InRange(end.Z,-39.65f,-39.5f);
        Assert.True(world.CanStand(end));
        Assert.False(world.CanStand(new(-4,0,-3)));
        Assert.False(world.CanStand(new(3,0,1)));
        Assert.True(world.CanStand(new(0,0,-8.5f)));
    }
    [Fact]
    public void CameraRetractsBeforeWallsAndLeavesClearViewsAlone()
    {
        var world = new DungeonWorld();
        var target = new Vector3(0,1.2f,7);
        var camera = world.CameraPosition(target,new(0,2,12));
        Assert.InRange(camera.Z,7,7.8f);
        Assert.Equal(new Vector3(0,3,4),world.CameraPosition(new(0,1.2f,0),new(0,3,4)));
    }
    [Fact]
    public void MouseUsesDeltaWhileStickRotationUsesElapsedTime()
    {
        var first = new DungeonSimulation(new());
        var second = new DungeonSimulation(new());
        first.Update(Vector2.Zero,new(10,0),true,0.01f);
        second.Update(Vector2.Zero,new(10,0),true,0.08f);
        Assert.Equal(first.Yaw,second.Yaw);
        first.Reset(); second.Reset();
        first.Update(Vector2.Zero,new(1,0),false,0.1f);
        for (int i=0;i<10;i++) second.Update(Vector2.Zero,new(1,0),false,0.01f);
        Assert.InRange(Math.Abs(first.Yaw-second.Yaw),0,0.00001f);
        first.Update(Vector2.Zero,new(0,10000),true,0.1f);
        Assert.Equal(1.25f,first.Pitch);
        first.Update(Vector2.Zero,new(0,-10000),true,0.1f);
        Assert.Equal(-0.18f,first.Pitch);
    }
    [Fact]
    public void ResetRestoresSpawnAndInvalidFramesDoNotCorruptState()
    {
        var game = new DungeonSimulation(new());
        game.Update(new(1,0),Vector2.Zero,true,0.1f);
        game.Reset();
        Assert.Equal(game.World.Spawn,game.Player.Position);
        game.Update(new(float.NaN,0),Vector2.Zero,true,0.1f);
        game.Update(Vector2.One,Vector2.Zero,true,float.NaN);
        Assert.Equal(game.World.Spawn,game.Player.Position);
    }
    [Fact]
    public void RadialDeadzoneRemovesDriftAndPreservesAnalogRange()
    {
        Assert.Equal(Vector2.Zero,InputMath.ApplyRadialDeadzone(new(0.05f,0.05f),0.18f));
        var slow = InputMath.ApplyRadialDeadzone(new(0.3f,0),0.18f);
        Assert.InRange(slow.X,0.14f,0.15f);
        Assert.Equal(Vector2.UnitX,InputMath.ApplyRadialDeadzone(Vector2.UnitX,0.18f));
        Assert.InRange(InputMath.ApplyRadialDeadzone(Vector2.One,0.18f).Length(),0.999f,1.001f);
    }
    [Fact]
    public void DungeonConfigValidatesTuning()
    {
        Settings.Required<DungeonSettings>(Settings.ConfigPath("dungeon.json")).Validate();
        Assert.Throws<InvalidDataException>(()=>new DungeonSettings { MoveDeadzone=1 }.Validate());
        Assert.Throws<InvalidDataException>(()=>new DungeonSettings { CameraDistance=float.NaN }.Validate());
        Assert.Throws<InvalidDataException>(()=>new DungeonSettings { KeyboardTurnSpeed=0 }.Validate());
        Assert.Throws<InvalidDataException>(()=>new DungeonSettings { InteractionRange=0 }.Validate());
    }
}

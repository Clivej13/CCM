using System.Numerics;
using CCM.Application;
using CCM.Game;
using Raylib_cs;
using RaylibGameFramework.Input;
using Xunit;
[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace CCM.InputIntegration.Tests;

public sealed class SessionInputTests
{
    private static InputConfig Bindings() => InputConfigLoader.Load("Config/input.json");
    private static DeviceSelection Select(InputDeviceFamily family, int slot = -1)
    {
        var selection = new DeviceSelection();
        selection.Update(new(null,false));
        selection.Update(new(new SelectedDevice(family,slot),true));
        selection.Update(new(null,false));
        return selection;
    }
    [Fact]
    public void SelectedPadOwnsMovementLookAndBackWhileMenuRemainsUnrestricted()
    {
        Raylib.Reset();
        var bindings = Bindings();
        var session = new SessionInput(bindings,Select(InputDeviceFamily.Gamepad,2),new());
        var menu = new InputController(bindings);
        Raylib.Connected.UnionWith([0,2]);
        Raylib.Axes[(0,GamepadAxis.LeftX)] = 1;
        Raylib.Buttons.Add((0,GamepadButton.RightFaceRight));
        Raylib.Keys.UnionWith([KeyboardKey.W,KeyboardKey.Escape]);
        Raylib.Delta = new(100,100);
        session.Update(); menu.Update();
        Assert.Equal(Vector2.Zero,session.Move);
        Assert.Equal(Vector2.Zero,session.Look);
        Assert.False(session.ReturnPressed());
        Assert.True(menu.WasPressed("MenuBack"));
        Raylib.Axes[(2,GamepadAxis.LeftX)] = .3f;
        Raylib.Axes[(2,GamepadAxis.RightY)] = -.4f;
        session.Update();
        Assert.InRange(session.Move.X,.146f,.147f);
        Assert.InRange(session.Look.Y,-.319f,-.318f);
        Assert.False(session.UsesMouse);
        Raylib.Buttons.Add((2,GamepadButton.RightFaceRight));
        session.Update();
        Assert.True(session.ReturnPressed());
        session.Update();
        Assert.False(session.ReturnPressed());
    }
    [Fact]
    public void KeyboardOwnershipNormalizesMovementButPreservesUnclampedMouseDelta()
    {
        Raylib.Reset();
        var session = new SessionInput(Bindings(),Select(InputDeviceFamily.KeyboardMouse),new());
        Raylib.Connected.Add(0);
        Raylib.Axes[(0,GamepadAxis.LeftX)] = -1;
        Raylib.Buttons.Add((0,GamepadButton.RightFaceRight));
        Raylib.Keys.UnionWith([KeyboardKey.W,KeyboardKey.D]);
        Raylib.Delta = new(-20,7);
        session.Update();
        Assert.InRange(session.Move.Length(),.999f,1.001f);
        Assert.True(session.Move.X>0 && session.Move.Y>0);
        Assert.Equal(new Vector2(-20,7),session.Look);
        Assert.False(session.ReturnPressed());
        Assert.True(session.UsesMouse);
    }
    [Fact]
    public void DisconnectFallbackPreservesAggregateEdgesAndReconnectsOnlyOwnedSlot()
    {
        Raylib.Reset();
        var session = new SessionInput(Bindings(),Select(InputDeviceFamily.Gamepad,2),new());
        Raylib.Connected.UnionWith([0,2]);
        Raylib.Axes[(2,GamepadAxis.LeftX)] = 1;
        Raylib.Buttons.Add((2,GamepadButton.RightFaceRight));
        session.Update(); Assert.True(session.ReturnPressed());
        Raylib.Keys.Add(KeyboardKey.Escape);
        Raylib.Connected.Remove(2);
        session.Update();
        Assert.True(session.ControllerDisconnected);
        Assert.Equal(Vector2.Zero,session.Move);
        Assert.False(session.ReturnPressed()); // held back -> held fallback is one action
        Raylib.Keys.Clear(); session.Update();
        Raylib.Keys.Add(KeyboardKey.Escape); session.Update();
        Assert.True(session.ReturnPressed());
        Raylib.Connected.Add(2); session.Update();
        Assert.False(session.ReturnPressed()); // held fallback -> held controller
        Assert.Equal(Vector2.UnitX,session.Move);
        Raylib.Buttons.Clear(); session.Update();
        Assert.False(session.ReturnPressed()); // keyboard Escape ignored while connected
        Raylib.Buttons.Add((2,GamepadButton.RightFaceRight)); session.Update();
        Assert.True(session.ReturnPressed());
    }
    [Fact]
    public void HeldTitleInputCannotSelectOrLeakIntoGameplay()
    {
        Raylib.Reset();
        var selection = new DeviceSelection();
        var session = new SessionInput(Bindings(),selection,new());
        Raylib.Connected.Add(2);
        Raylib.Buttons.Add((2,GamepadButton.RightFaceRight));
        var device = new SelectedDevice(InputDeviceFamily.Gamepad,2);
        selection.Update(new(device,true)); session.Update();
        Assert.False(session.ReturnPressed());
        Assert.Null(selection.Selected);
        Raylib.Buttons.Clear(); selection.Update(new(null,false)); session.Update();
        Raylib.Buttons.Add((2,GamepadButton.RightFaceRight));
        selection.Update(new(device,true)); session.Update();
        Assert.False(session.ReturnPressed());
        Raylib.Buttons.Clear(); Assert.True(selection.Update(new(null,false))); session.Update();
        Assert.False(session.ReturnPressed());
        Raylib.Buttons.Add((2,GamepadButton.RightFaceRight)); session.Update();
        Assert.True(session.ReturnPressed());
    }
    [Fact]
    public void SignedBindingsDeadzoneAndDiagonalMagnitudeSurviveMigration()
    {
        Raylib.Reset();
        var session = new SessionInput(Bindings(),Select(InputDeviceFamily.Gamepad,1),new());
        Raylib.Connected.Add(1);
        Raylib.Axes[(1,GamepadAxis.LeftX)] = -.05f;
        Raylib.Axes[(1,GamepadAxis.LeftY)] = -.05f;
        session.Update(); Assert.Equal(Vector2.Zero,session.Move);
        Raylib.Axes[(1,GamepadAxis.LeftX)] = -1;
        Raylib.Axes[(1,GamepadAxis.LeftY)] = -1;
        session.Update();
        Assert.InRange(session.Move.Length(),.999f,1.001f);
        Assert.True(session.Move.X<0 && session.Move.Y>0);
        Raylib.Axes[(1,GamepadAxis.LeftX)] = float.NaN;
        Raylib.Axes[(1,GamepadAxis.LeftY)] = 0;
        session.Update(); Assert.Equal(Vector2.Zero,session.Move);
    }
}

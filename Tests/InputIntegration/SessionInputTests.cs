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
    public void KeyboardOwnershipPreservesTurnAndStrafeAxesAndMouseDelta()
    {
        Raylib.Reset();
        var session = new SessionInput(Bindings(),Select(InputDeviceFamily.KeyboardMouse),new());
        Raylib.Connected.Add(0);
        Raylib.Axes[(0,GamepadAxis.LeftX)] = -1;
        Raylib.Buttons.Add((0,GamepadButton.RightFaceRight));
        Raylib.Keys.UnionWith([KeyboardKey.W,KeyboardKey.D]);
        Raylib.Delta = new(-20,7);
        session.Update();
        Assert.Equal(Vector2.One,session.Move);
        Assert.Equal(0,session.Strafe);
        Raylib.Keys.Add(KeyboardKey.Q);
        session.Update();
        Assert.Equal(-1,session.Strafe);
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
    public void InteractionUsesOnlySelectedDeviceAndFiresOncePerPress()
    {
        Raylib.Reset();
        var pad = new SessionInput(Bindings(),Select(InputDeviceFamily.Gamepad,2),new());
        Raylib.Connected.UnionWith([0,2]);
        Raylib.Buttons.Add((0,GamepadButton.RightFaceDown));
        Raylib.Keys.Add(KeyboardKey.E);
        pad.Update();
        Assert.False(pad.InteractPressed());
        Raylib.Buttons.Add((2,GamepadButton.RightFaceDown));
        pad.Update();
        Assert.True(pad.InteractPressed());
        pad.Update();
        Assert.False(pad.InteractPressed());
        Raylib.Buttons.Remove((2,GamepadButton.RightFaceDown));
        pad.Update();
        Raylib.Buttons.Add((2,GamepadButton.RightFaceDown));
        pad.Update();
        Assert.True(pad.InteractPressed());

        Raylib.Reset();
        var keyboard = new SessionInput(Bindings(),Select(InputDeviceFamily.KeyboardMouse),new());
        Raylib.Connected.Add(0);
        Raylib.Buttons.Add((0,GamepadButton.RightFaceDown));
        keyboard.Update();
        Assert.False(keyboard.InteractPressed());
        Raylib.Keys.Add(KeyboardKey.E);
        keyboard.Update();
        Assert.False(keyboard.InteractPressed());
        Assert.Equal(1,keyboard.Strafe);
        Raylib.Keys.Add(KeyboardKey.F);
        keyboard.Update();
        Assert.True(keyboard.InteractPressed());
        keyboard.Update();
        Assert.False(keyboard.InteractPressed());
    }

    [Fact]
    public void CombatAndPauseActionsBelongToTheSelectedDevice()
    {
        Raylib.Reset();
        var pad = new SessionInput(Bindings(),Select(InputDeviceFamily.Gamepad,2),new());
        Raylib.Connected.UnionWith([0,2]);
        Raylib.Keys.Add(KeyboardKey.Space);
        Raylib.Mouse.UnionWith([MouseButton.Left,MouseButton.Right]);
        Raylib.Buttons.UnionWith([
            (0,GamepadButton.MiddleRight),
            (0,GamepadButton.MiddleLeft),
            (0,GamepadButton.LeftTrigger1),
            (0,GamepadButton.RightTrigger1)
        ]);
        pad.Update();
        Assert.False(pad.PausePressed());
        Assert.False(pad.CombatPausePressed());
        Assert.False(pad.SelectPressed());
        Assert.False(pad.AttackPressed());
        Assert.False(pad.AttackHeld);
        Raylib.Buttons.UnionWith([
            (2,GamepadButton.MiddleRight),
            (2,GamepadButton.MiddleLeft),
            (2,GamepadButton.LeftTrigger1),
            (2,GamepadButton.RightTrigger1)
        ]);
        pad.Update();
        Assert.True(pad.PausePressed());
        Assert.True(pad.CombatPausePressed());
        Assert.True(pad.CycleActionLeftPressed());
        Assert.True(pad.CycleActionRightPressed());
        Assert.False(pad.SelectPressed());
        Assert.False(pad.AttackPressed());
        Assert.False(pad.AttackHeld);
        pad.Update();
        Assert.False(pad.PausePressed());
        Assert.False(pad.CombatPausePressed());
        Assert.False(pad.CycleActionLeftPressed());
        Assert.False(pad.CycleActionRightPressed());

        Raylib.Reset();
        var keyboard = new SessionInput(Bindings(),Select(InputDeviceFamily.KeyboardMouse),new());
        Raylib.Keys.Add(KeyboardKey.Space);
        Raylib.Mouse.UnionWith([MouseButton.Left,MouseButton.Right]);
        keyboard.Update();
        Assert.False(keyboard.PausePressed());
        Assert.True(keyboard.CombatPausePressed());
        Assert.True(keyboard.SelectPressed());
        Assert.True(keyboard.AttackPressed());
        Raylib.Keys.Add(KeyboardKey.Escape);
        keyboard.Update();
        Assert.True(keyboard.PausePressed());
    }

    [Fact]
    public void StartEscapeAndBackSpaceRemainSeparatePauseActions()
    {
        Raylib.Reset();
        var pad=new SessionInput(Bindings(),Select(InputDeviceFamily.Gamepad,2),new());
        Raylib.Connected.Add(2);
        Raylib.Buttons.Add((2,GamepadButton.RightFaceRight));
        pad.Update();
        Assert.True(pad.ReturnPressed());
        Assert.False(pad.PausePressed());
        Assert.False(pad.CombatPausePressed());

        Raylib.Buttons.Clear();
        pad.Update();
        Raylib.Buttons.Add((2,GamepadButton.MiddleRight));
        pad.Update();
        Assert.True(pad.PausePressed());
        Assert.False(pad.CombatPausePressed());

        Raylib.Buttons.Clear();
        pad.Update();
        Raylib.Buttons.Add((2,GamepadButton.MiddleLeft));
        pad.Update();
        Assert.False(pad.PausePressed());
        Assert.True(pad.CombatPausePressed());

        Raylib.Buttons.Clear();
        pad.Update();
        Raylib.Keys.Add(KeyboardKey.Escape);
        pad.Update();
        Assert.True(pad.PausePressed());
        Assert.False(pad.CombatPausePressed());
    }

    [Fact]
    public void MouseWheelZoomDoesNotUseControllerTriggers()
    {
        Raylib.Reset();
        var pad = new SessionInput(Bindings(),Select(InputDeviceFamily.Gamepad,2),new());
        Raylib.Connected.UnionWith([0,2]);
        Raylib.Wheel = new(0,-2);
        Raylib.Axes[(0,GamepadAxis.RightTrigger)] = 1;
        pad.Update();
        Assert.Equal(0,pad.Zoom);
        Raylib.Axes[(2,GamepadAxis.RightTrigger)] = 1;
        pad.Update();
        Assert.Equal(0,pad.Zoom);
        Raylib.Axes[(2,GamepadAxis.RightTrigger)] = -1;
        Raylib.Axes[(2,GamepadAxis.LeftTrigger)] = 1;
        pad.Update();
        Assert.Equal(0,pad.Zoom);

        Raylib.Reset();
        var keyboard = new SessionInput(Bindings(),Select(InputDeviceFamily.KeyboardMouse),new());
        Raylib.Wheel = new(0,-2);
        keyboard.Update();
        Assert.Equal(-2,keyboard.Zoom);
    }

    [Fact]
    public void SelectedControllerCyclesActionsAndCameraOnPressEdges()
    {
        Raylib.Reset();
        var pad = new SessionInput(Bindings(),Select(InputDeviceFamily.Gamepad,2),new());
        Raylib.Connected.UnionWith([0,2]);
        Raylib.Buttons.UnionWith([
            (0,GamepadButton.LeftTrigger1),
            (0,GamepadButton.RightTrigger1),
            (0,GamepadButton.RightThumb)
        ]);
        pad.Update();
        Assert.False(pad.CycleActionLeftPressed());
        Assert.False(pad.CycleActionRightPressed());
        Assert.False(pad.CycleViewPressed());
        Raylib.Buttons.UnionWith([
            (2,GamepadButton.LeftTrigger1),
            (2,GamepadButton.RightThumb)
        ]);
        pad.Update();
        Assert.True(pad.CycleActionLeftPressed());
        Assert.False(pad.CycleActionRightPressed());
        Assert.True(pad.CycleViewPressed());
        pad.Update();
        Assert.False(pad.CycleActionLeftPressed());
        Assert.False(pad.CycleViewPressed());
        Raylib.Buttons.Remove((2,GamepadButton.LeftTrigger1));
        Raylib.Buttons.Add((2,GamepadButton.RightTrigger1));
        pad.Update();
        Assert.True(pad.CycleActionRightPressed());
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

    [Fact]
    public void LeftStickNavigatesMenusAndSelectedDeviceInventoryOnPressEdges()
    {
        Raylib.Reset();
        var bindings=Bindings();
        var menu=new InputController(bindings);
        var inventory=new SessionInput(bindings,Select(InputDeviceFamily.Gamepad,2),new());
        Raylib.Connected.UnionWith([0,2]);

        Raylib.Axes[(0,GamepadAxis.LeftY)]=-1;
        menu.Update(); inventory.Update();
        Assert.True(menu.WasPressed("MenuUp"));
        Assert.False(inventory.UiUpPressed());
        menu.Update(); inventory.Update();
        Assert.False(menu.WasPressed("MenuUp"));

        Raylib.Axes[(0,GamepadAxis.LeftY)]=0;
        Raylib.Axes[(2,GamepadAxis.LeftY)]=-.4f;
        menu.Update(); inventory.Update();
        Assert.False(inventory.UiUpPressed());
        Raylib.Axes[(2,GamepadAxis.LeftY)]=-.8f;
        menu.Update(); inventory.Update();
        Assert.True(menu.WasPressed("MenuUp"));
        Assert.True(inventory.UiUpPressed());
        menu.Update(); inventory.Update();
        Assert.False(inventory.UiUpPressed());

        Raylib.Axes[(2,GamepadAxis.LeftY)]=.8f;
        menu.Update(); inventory.Update();
        Assert.True(menu.WasPressed("MenuDown"));
        Assert.True(inventory.UiDownPressed());

        Raylib.Axes[(2,GamepadAxis.LeftX)]=-.8f;
        menu.Update();
        Assert.True(menu.WasPressed("MenuLeft"));
        Raylib.Axes[(2,GamepadAxis.LeftX)]=.8f;
        menu.Update();
        Assert.True(menu.WasPressed("MenuRight"));
    }

    [Fact]
    public void InventoryFiltersAndPaneSwitchUseTheSelectedController()
    {
        Raylib.Reset();
        var pad=new SessionInput(Bindings(),Select(InputDeviceFamily.Gamepad,2),new());
        Raylib.Connected.UnionWith([0,2]);
        Raylib.Axes[(0,GamepadAxis.LeftX)]=-1;
        Raylib.Buttons.Add((0,GamepadButton.RightFaceLeft));
        pad.Update();
        Assert.False(pad.UiLeftPressed());
        Assert.False(pad.UiPanePressed());

        Raylib.Axes[(2,GamepadAxis.LeftX)]=-.8f;
        Raylib.Buttons.Add((2,GamepadButton.RightFaceLeft));
        pad.Update();
        Assert.True(pad.UiLeftPressed());
        Assert.True(pad.UiPanePressed());
        pad.Update();
        Assert.False(pad.UiLeftPressed());
        Assert.False(pad.UiPanePressed());
        Raylib.Axes[(2,GamepadAxis.LeftX)]=.8f;
        pad.Update();
        Assert.True(pad.UiRightPressed());
    }

    [Fact]
    public void KeyboardFiltersAndPanelTabsHaveSeparatePressEdges()
    {
        Raylib.Reset();
        var keyboard=new SessionInput(Bindings(),Select(InputDeviceFamily.KeyboardMouse),new());
        Raylib.Keys.UnionWith([KeyboardKey.C,KeyboardKey.K,KeyboardKey.D,KeyboardKey.Q]);
        keyboard.Update();
        Assert.True(keyboard.CharacterPressed());
        Assert.True(keyboard.AbilityPressed());
        Assert.True(keyboard.UiRightPressed());
        Assert.False(keyboard.UiLeftPressed());
        Assert.True(keyboard.PanelLeftPressed());
        Assert.False(keyboard.PanelRightPressed());
        keyboard.Update();
        Assert.False(keyboard.CharacterPressed());
        Assert.False(keyboard.AbilityPressed());
        Assert.False(keyboard.UiRightPressed());
        Assert.False(keyboard.PanelLeftPressed());
        Raylib.Keys.Clear();
        keyboard.Update();
        Raylib.Keys.UnionWith([KeyboardKey.A,KeyboardKey.E]);
        keyboard.Update();
        Assert.True(keyboard.UiLeftPressed());
        Assert.True(keyboard.PanelRightPressed());
        Assert.False(keyboard.UiRightPressed());
        Assert.False(keyboard.PanelLeftPressed());

        Raylib.Reset();
        var pad=new SessionInput(Bindings(),Select(InputDeviceFamily.Gamepad,2),new());
        Raylib.Connected.Add(2);
        Raylib.Keys.UnionWith([KeyboardKey.C,KeyboardKey.K,KeyboardKey.A,KeyboardKey.D,
            KeyboardKey.Q,KeyboardKey.E]);
        pad.Update();
        Assert.False(pad.CharacterPressed());
        Assert.False(pad.AbilityPressed());
        Assert.False(pad.PanelLeftPressed());
        Assert.False(pad.PanelRightPressed());
        Assert.False(pad.UiLeftPressed());
        Assert.False(pad.UiRightPressed());
    }

    [Fact]
    public void SelectedControllerTriggersSwitchPanelsOncePerPress()
    {
        Raylib.Reset();
        var pad=new SessionInput(Bindings(),Select(InputDeviceFamily.Gamepad,2),new());
        Raylib.Connected.UnionWith([0,2]);
        Raylib.Axes[(0,GamepadAxis.LeftTrigger)]=1;
        Raylib.Axes[(0,GamepadAxis.RightTrigger)]=1;
        pad.Update();
        Assert.False(pad.PanelLeftPressed());
        Assert.False(pad.PanelRightPressed());

        Raylib.Axes[(2,GamepadAxis.LeftTrigger)]=.4f;
        pad.Update();
        Assert.False(pad.PanelLeftPressed());
        Raylib.Axes[(2,GamepadAxis.LeftTrigger)]=.8f;
        pad.Update();
        Assert.True(pad.PanelLeftPressed());
        Assert.False(pad.PanelRightPressed());
        pad.Update();
        Assert.False(pad.PanelLeftPressed());

        Raylib.Axes[(2,GamepadAxis.LeftTrigger)]=-1;
        Raylib.Axes[(2,GamepadAxis.RightTrigger)]=.8f;
        pad.Update();
        Assert.False(pad.PanelLeftPressed());
        Assert.True(pad.PanelRightPressed());
        pad.Update();
        Assert.False(pad.PanelRightPressed());
        Raylib.Connected.Remove(2);
        pad.Update();
        Assert.False(pad.PanelLeftPressed());
        Assert.False(pad.PanelRightPressed());
    }

    [Fact]
    public void InventoryNavigationUsesOnlyTheSelectedDeviceAndPressEdges()
    {
        Raylib.Reset();
        var pad=new SessionInput(Bindings(),Select(InputDeviceFamily.Gamepad,2),new());
        Raylib.Connected.UnionWith([0,2]);
        Raylib.Keys.UnionWith([KeyboardKey.I,KeyboardKey.Enter,KeyboardKey.Down]);
        Raylib.Buttons.Add((0,GamepadButton.RightFaceUp));
        pad.Update();
        Assert.False(pad.InventoryPressed());
        Assert.False(pad.UiConfirmPressed());
        Assert.False(pad.UiDownPressed());

        Raylib.Buttons.UnionWith([
            (2,GamepadButton.RightFaceUp),
            (2,GamepadButton.RightFaceDown),
            (2,GamepadButton.LeftFaceDown)
        ]);
        pad.Update();
        Assert.True(pad.InventoryPressed());
        Assert.True(pad.UiConfirmPressed());
        Assert.True(pad.UiDownPressed());
        pad.Update();
        Assert.False(pad.InventoryPressed());
        Assert.False(pad.UiConfirmPressed());

        Raylib.Reset();
        var keyboard=new SessionInput(Bindings(),Select(InputDeviceFamily.KeyboardMouse),new());
        Raylib.Connected.Add(0);
        Raylib.Buttons.Add((0,GamepadButton.RightFaceUp));
        keyboard.Update();
        Assert.False(keyboard.InventoryPressed());
        Raylib.Keys.UnionWith([KeyboardKey.I,KeyboardKey.Enter,KeyboardKey.Up]);
        keyboard.Update();
        Assert.True(keyboard.InventoryPressed());
        Assert.True(keyboard.UiConfirmPressed());
        Assert.True(keyboard.UiUpPressed());
    }
}

using System.Numerics;
using CCM.Game;
using RaylibGameFramework.Input;
namespace CCM.Application;

/// <summary>Selected gameplay device. Unrestricted menu input remains separate.</summary>
public sealed class SessionInput
{
    private readonly InputConfig bindings;
    private readonly DeviceSelection selection;
    private readonly DungeonSettings settings;
    private readonly IGamepadSource gamepads;
    private readonly InputController keyboard;
    private bool backDown, backPressed;
    public Vector2 Move { get; private set; }
    public Vector2 Look { get; private set; }
    public bool UsesMouse => selection.Selected?.Family == InputDeviceFamily.KeyboardMouse;
    public bool ControllerDisconnected => selection.Selected is { Family: InputDeviceFamily.Gamepad } selected &&
        !gamepads.Available(selected.Gamepad);

    public SessionInput(InputConfig bindings, DeviceSelection selection, DungeonSettings settings, IGamepadSource? gamepads = null)
    {
        this.bindings = bindings; this.selection = selection; this.settings = settings;
        this.gamepads = gamepads ?? new RaylibGamepadSource();
        keyboard = new(new InputConfig { Bindings = bindings.Bindings.Where(b =>
            b.Device.Equals("Keyboard",StringComparison.OrdinalIgnoreCase) ||
            b.Device.Equals("Mouse",StringComparison.OrdinalIgnoreCase) ||
            b.Device.Equals("MouseAxis",StringComparison.OrdinalIgnoreCase)).ToList() });
    }
    public void Update()
    {
        keyboard.Update();
        Move = Look = Vector2.Zero;
        bool down = false;
        if (selection.Selected is { } selected)
        {
            float Value(string action) => UsesMouse ? Math.Abs(keyboard.GetValue(action))
                : GamepadActions.Value(bindings, gamepads, selected.Gamepad, action);
            Move = new(Value("MoveRight")-Value("MoveLeft"), Value("MoveForward")-Value("MoveBackward"));
            Look = new(Value("LookRight")-Value("LookLeft"), Value("LookDown")-Value("LookUp"));
            if (UsesMouse)
            {
                if (Move.LengthSquared() > 1) Move = Vector2.Normalize(Move);
            }
            else
            {
                Move = GamepadActions.Deadzone(Move, settings.MoveDeadzone);
                Look = GamepadActions.Deadzone(Look, settings.LookDeadzone);
            }
            down = ControllerDisconnected ? keyboard.IsDown("MenuBack") : Value("MenuBack") > 0;
        }
        backPressed = down && !backDown;
        backDown = down;
    }
    public bool ReturnPressed() => backPressed;
}

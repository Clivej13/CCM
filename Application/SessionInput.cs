using System.Numerics;
using CCM.Game;
using Raylib_cs;
using RaylibGameFramework.Input;
namespace CCM.Application;

/// <summary>Maps owned framework input to gameplay; menu input remains unrestricted.</summary>
public sealed class SessionInput
{
    private readonly InputConfig bindings;
    private readonly DeviceSelection selection;
    private readonly DungeonSettings settings;
    private readonly InputController keyboard;
    private InputController? gameplay;
    private SelectedDevice? owner;
    private bool backDown, backPressed;
    public Vector2 Move { get; private set; }
    public Vector2 Look { get; private set; }
    public bool UsesMouse => selection.Selected?.Family == InputDeviceFamily.KeyboardMouse;
    public bool ControllerDisconnected => selection.Selected is { Family: InputDeviceFamily.Gamepad } selected &&
        !Raylib.IsGamepadAvailable(selected.Gamepad);

    public SessionInput(InputConfig bindings, DeviceSelection selection, DungeonSettings settings)
    {
        this.bindings = bindings; this.selection = selection; this.settings = settings;
        keyboard = new(bindings, new InputControllerOptions { DeviceFamily = InputDeviceFamily.KeyboardMouse });
    }
    public void Update()
    {
        // Always sample the fallback, even while a connected controller owns gameplay.
        keyboard.Update();
        Move = Look = Vector2.Zero;
        bool down = false;
        if (selection.Selected is { } selected)
        {
            if (gameplay is null || owner != selected)
            {
                gameplay = new(bindings, new InputControllerOptions
                {
                    DeviceFamily = selected.Family,
                    GamepadIndex = selected.Family == InputDeviceFamily.Gamepad ? selected.Gamepad : null
                });
                owner = selected;
            }
            gameplay.Update();
            float Value(string action) => Math.Abs(gameplay.GetRawValue(action));
            Move = new(Value("MoveRight")-Value("MoveLeft"), Value("MoveForward")-Value("MoveBackward"));
            Look = new(Value("LookRight")-Value("LookLeft"), Value("LookDown")-Value("LookUp"));
            if (UsesMouse)
            {
                if (Move.LengthSquared() > 1) Move = Vector2.Normalize(Move);
            }
            else
            {
                Move = InputMath.ApplyRadialDeadzone(Move, settings.MoveDeadzone);
                Look = InputMath.ApplyRadialDeadzone(Look, settings.LookDeadzone);
            }
            down = ControllerDisconnected ? keyboard.IsDown("MenuBack") : gameplay.IsDown("MenuBack");
        }
        // Preserve one logical edge when fallback and controller are both held.
        backPressed = down && !backDown;
        backDown = down;
    }
    public bool ReturnPressed() => backPressed;
    public bool InteractPressed() => gameplay?.WasPressed("Interact") == true;
    public bool PausePressed() => gameplay?.WasPressed("Pause") == true;
    public bool SelectPressed() => gameplay?.WasPressed("SelectEnemy") == true;
    public bool AttackOrInteractPressed() => gameplay?.WasPressed("AttackOrInteract") == true;
    public string InteractionBinding => gameplay?.GetBinding("Interact",
        UsesMouse ? InputDeviceFamily.KeyboardMouse : InputDeviceFamily.Gamepad) is { } binding
        ? InputController.GetInputDisplayName(binding.Device,binding.Input) : "Unbound";
}

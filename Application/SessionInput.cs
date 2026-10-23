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
    private bool backDown, backPressed, pauseDown, pausePressed;
    public Vector2 Move { get; private set; }
    public float Strafe { get; private set; }
    public Vector2 Look { get; private set; }
    /// <summary>Mouse wheel delta; controller camera changes use CycleView.</summary>
    public float Zoom { get; private set; }
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
        Strafe = Zoom = 0;
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
                Strafe = Value("StrafeRight") - Value("StrafeLeft");
                Zoom = Raylib.GetMouseWheelMoveV().Y;
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
        bool pauseHeld = keyboard.IsDown("Pause") ||
            (!ControllerDisconnected && gameplay?.IsDown("Pause") == true);
        pausePressed = pauseHeld && !pauseDown;
        pauseDown = pauseHeld;
    }
    public bool ReturnPressed() => backPressed;
    public bool InteractPressed() => gameplay?.WasPressed("Interact") == true;
    public bool PausePressed() => pausePressed;
    public bool CombatPausePressed() => gameplay?.WasPressed("CombatPause") == true;
    public bool SelectPressed() => gameplay?.WasPressed("SelectEnemy") == true;
    public bool AttackPressed() => gameplay?.WasPressed("Attack") == true;
    public bool AttackHeld => gameplay?.IsDown("Attack") == true;
    public bool CycleViewPressed() => gameplay?.WasPressed("CycleView") == true;
    public bool CycleActionLeftPressed() => gameplay?.WasPressed("CycleActionLeft") == true;
    public bool CycleActionRightPressed() => gameplay?.WasPressed("CycleActionRight") == true;
    public bool InventoryPressed() => gameplay?.WasPressed("Inventory") == true;
    public bool CharacterPressed() => UsesMouse && gameplay?.WasPressed("Character") == true;
    public bool PanelLeftPressed() => gameplay?.WasPressed("PanelLeft") == true;
    public bool PanelRightPressed() => gameplay?.WasPressed("PanelRight") == true;
    public bool UiUpPressed() => gameplay?.WasPressed("MenuUp") == true;
    public bool UiDownPressed() => gameplay?.WasPressed("MenuDown") == true;
    public bool UiLeftPressed() => gameplay?.WasPressed("MenuLeft") == true ||
        UsesMouse && gameplay?.WasPressed("MoveLeft") == true;
    public bool UiRightPressed() => gameplay?.WasPressed("MenuRight") == true ||
        UsesMouse && gameplay?.WasPressed("MoveRight") == true;
    public bool UiPanePressed() => gameplay?.WasPressed("InventoryPane") == true;
    public bool UiConfirmPressed() => gameplay?.WasPressed("MenuConfirm") == true;
    public string InteractionBinding => gameplay?.GetBinding("Interact",
        UsesMouse ? InputDeviceFamily.KeyboardMouse : InputDeviceFamily.Gamepad) is { } binding
        ? InputController.GetInputDisplayName(binding.Device,binding.Input) : "Unbound";
}

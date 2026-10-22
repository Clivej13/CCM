using Raylib_cs;
using RaylibGameFramework.Input;
namespace CCM.Application;

/// <summary>CCM adapter for selected-device return input; menus retain unrestricted framework input.</summary>
public sealed class SessionInput(InputConfig bindings, DeviceSelection selection)
{
    private readonly InputController keyboard = new(new InputConfig
    {
        Bindings = bindings.Bindings.Where(b => b.Device is "Keyboard" or "Mouse" or "MouseAxis").ToList()
    });
    public void Update() => keyboard.Update();
    public bool ReturnPressed()
    {
        if (selection.Selected is not { } selected) return false;
        if (selected.Family == InputDeviceFamily.KeyboardMouse) return keyboard.WasPressed("MenuBack");
        // Let a disconnected controller's player recover to the menu with keyboard/mouse.
        if (!Raylib.IsGamepadAvailable(selected.Gamepad)) return keyboard.WasPressed("MenuBack");
        return bindings.Bindings.Any(b => b.Action == "MenuBack" && b.Device == "GamepadButton" &&
            Enum.TryParse<GamepadButton>(b.Input, true, out var button) &&
            Raylib.IsGamepadButtonPressed(selected.Gamepad, button));
    }
}

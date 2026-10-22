using Raylib_cs;
using RaylibGameFramework.Input;
namespace CCM.Application;

public static class TitleInputReader
{
    public static TitleInput Read()
    {
        SelectedDevice? pressed = null;
        bool down = false;
        // Keyboard/mouse wins simultaneous presses, then the lowest controller slot.
        foreach (var key in Enum.GetValues<KeyboardKey>())
        {
            if (key == KeyboardKey.Null) continue;
            down |= Raylib.IsKeyDown(key);
            if (Raylib.IsKeyPressed(key)) pressed ??= new(InputDeviceFamily.KeyboardMouse);
        }
        foreach (var button in Enum.GetValues<MouseButton>())
        {
            down |= Raylib.IsMouseButtonDown(button);
            if (Raylib.IsMouseButtonPressed(button)) pressed ??= new(InputDeviceFamily.KeyboardMouse);
        }
        for (int gamepad = 0; gamepad < 4; gamepad++)
        {
            if (!Raylib.IsGamepadAvailable(gamepad)) continue;
            foreach (var button in Enum.GetValues<GamepadButton>())
            {
                if (button == GamepadButton.Unknown) continue;
                down |= Raylib.IsGamepadButtonDown(gamepad, button);
                if (Raylib.IsGamepadButtonPressed(gamepad, button))
                    pressed ??= new(InputDeviceFamily.Gamepad, gamepad);
            }
        }
        // Mouse motion and stick drift never confirm the title.
        return new(pressed, down);
    }
}

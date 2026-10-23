using System.Numerics;
namespace Raylib_cs;
public enum KeyboardKey { Null, W, A, S, D, E, Up, Down, Left, Right, Enter, Escape, Space }
public enum MouseButton { Left, Right }
public enum GamepadButton { Unknown, RightFaceDown, RightFaceRight, LeftFaceUp, LeftFaceDown, LeftFaceLeft, LeftFaceRight, LeftTrigger1, RightTrigger1, MiddleRight }
public enum GamepadAxis { LeftX, LeftY, RightX, RightY, LeftTrigger, RightTrigger }
public static class Raylib
{
    public static Vector2 Delta;
    public static readonly HashSet<KeyboardKey> Keys = [];
    public static readonly HashSet<MouseButton> Mouse = [];
    public static readonly HashSet<int> Connected = [];
    public static readonly HashSet<(int, GamepadButton)> Buttons = [];
    public static readonly Dictionary<(int, GamepadAxis),float> Axes = [];
    public static Vector2 GetMouseDelta() => Delta;
    public static Vector2 GetMousePosition() => Vector2.Zero;
    public static Vector2 GetMouseWheelMoveV() => Vector2.Zero;
    public static bool IsKeyDown(KeyboardKey key) => Keys.Contains(key);
    public static bool IsKeyPressed(KeyboardKey key) => Keys.Contains(key);
    public static bool IsMouseButtonDown(MouseButton button) => Mouse.Contains(button);
    public static bool IsMouseButtonPressed(MouseButton button) => Mouse.Contains(button);
    public static bool IsGamepadAvailable(int slot) => Connected.Contains(slot);
    public static bool IsGamepadButtonDown(int slot,GamepadButton button) => Buttons.Contains((slot,button));
    public static bool IsGamepadButtonPressed(int slot,GamepadButton button) => Buttons.Contains((slot,button));
    public static float GetGamepadAxisMovement(int slot,GamepadAxis axis) =>
        Axes.GetValueOrDefault((slot,axis),axis is GamepadAxis.LeftTrigger or GamepadAxis.RightTrigger ? -1 : 0);
    public static void Reset() { Delta=Vector2.Zero; Keys.Clear(); Mouse.Clear(); Connected.Clear(); Buttons.Clear(); Axes.Clear(); }
}

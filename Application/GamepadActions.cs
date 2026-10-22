using System.Numerics;
using Raylib_cs;
using RaylibGameFramework.Input;
namespace CCM.Application;

public interface IGamepadSource
{
    bool Available(int index);
    float Axis(int index, GamepadAxis axis);
    bool Down(int index, GamepadButton button);
}
public sealed class RaylibGamepadSource : IGamepadSource
{
    public bool Available(int index) => Raylib.IsGamepadAvailable(index);
    public float Axis(int index, GamepadAxis axis) => Raylib.GetGamepadAxisMovement(index, axis);
    public bool Down(int index, GamepadButton button) => Raylib.IsGamepadButtonDown(index, button);
}

/// <summary>Temporary CCM adapter: exact controller slot, raw directional axes and radial deadzones.</summary>
public static class GamepadActions
{
    public static float Value(InputConfig bindings, IGamepadSource source, int slot, string action)
    {
        if (slot < 0 || !source.Available(slot)) return 0;
        float result = 0;
        foreach (var binding in bindings.Bindings.Where(b => b.Action.Equals(action, StringComparison.OrdinalIgnoreCase)))
        {
            float value = 0;
            if (binding.Device.Equals("GamepadButton", StringComparison.OrdinalIgnoreCase) &&
                Enum.TryParse<GamepadButton>(binding.Input,true,out var button))
                value = source.Down(slot,button) ? 1 : 0;
            else if (binding.Device.Equals("GamepadAxis", StringComparison.OrdinalIgnoreCase))
            {
                bool negative = binding.Input.EndsWith("Negative", StringComparison.OrdinalIgnoreCase);
                bool positive = binding.Input.EndsWith("Positive", StringComparison.OrdinalIgnoreCase);
                string name = negative ? binding.Input[..^8] : positive ? binding.Input[..^8] : binding.Input;
                if (Enum.TryParse<GamepadAxis>(name,true,out var axis))
                {
                    float raw = source.Axis(slot,axis);
                    if (float.IsFinite(raw))
                        value = negative ? Math.Max(0,-raw) : Math.Max(0,raw);
                }
            }
            result = Math.Max(result,Math.Clamp(value,0,1));
        }
        return result;
    }
    public static Vector2 Deadzone(Vector2 value, float deadzone)
    {
        if (!float.IsFinite(deadzone) || deadzone is < 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(deadzone));
        float length = value.Length();
        if (!float.IsFinite(length) || length <= deadzone) return Vector2.Zero;
        return value / length * ((Math.Min(length,1)-deadzone)/(1-deadzone));
    }
}

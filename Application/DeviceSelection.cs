using RaylibGameFramework.Input;
namespace CCM.Application;

public readonly record struct SelectedDevice(InputDeviceFamily Family, int Gamepad = -1)
{
    public override string ToString() => Family == InputDeviceFamily.Gamepad ? $"Controller {Gamepad + 1}" : "Keyboard / mouse";
}
public readonly record struct TitleInput(SelectedDevice? Pressed, bool AnyButtonDown);

/// <summary>Waits for release, a fresh deliberate press, then release again.</summary>
public sealed class DeviceSelection
{
    private bool armed;
    private SelectedDevice? pending;
    public SelectedDevice? Selected { get; private set; }
    public bool Update(TitleInput input)
    {
        if (Selected is not null) return false;
        if (!armed) { armed = !input.AnyButtonDown; return false; }
        if (pending is null) { pending = input.Pressed; return false; }
        if (input.AnyButtonDown) return false;
        Selected = pending;
        return true;
    }
}

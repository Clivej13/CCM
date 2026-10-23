namespace CCM.Game;

public sealed class DungeonSettings
{
    public float MoveSpeed { get; set; } = 4.5f;
    public float MouseSensitivity { get; set; } = 0.003f;
    public float ControllerLookSpeed { get; set; } = 2.4f;
    public float MoveDeadzone { get; set; } = 0.18f;
    public float LookDeadzone { get; set; } = 0.12f;
    public float CameraDistance { get; set; } = 5f;
    public float InteractionRange { get; set; } = 2.5f;
    public float CameraPitch { get; set; } = 0.45f;
    public void Validate()
    {
        if (!float.IsFinite(InteractionRange) || InteractionRange is < 0.5f or > 5 ||
            !float.IsFinite(MoveSpeed) || MoveSpeed is <= 0 or > 20 ||
            !float.IsFinite(MouseSensitivity) || MouseSensitivity is <= 0 or > 0.1f ||
            !float.IsFinite(ControllerLookSpeed) || ControllerLookSpeed is <= 0 or > 10 ||
            !float.IsFinite(MoveDeadzone) || MoveDeadzone is < 0 or >= 1 ||
            !float.IsFinite(LookDeadzone) || LookDeadzone is < 0 or >= 1 ||
            !float.IsFinite(CameraDistance) || CameraDistance is < 2 or > 12 ||
            !float.IsFinite(CameraPitch) || CameraPitch is < 0.12f or > 1.25f)
            throw new InvalidDataException("Invalid dungeon movement/camera settings.");
    }
}

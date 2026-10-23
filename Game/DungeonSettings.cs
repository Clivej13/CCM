namespace CCM.Game;

public sealed class GameplaySettings
{
    public bool AutoAttackSelectedEnemy { get; set; }
}

public sealed class CombatSettings
{
    public int PlayerHealth { get; set; } = 100;
    public int EnemyHealth { get; set; } = 60;
    public int Damage { get; set; } = 12;
    public float CastSeconds { get; set; } = 0.6f;
    public float CooldownSeconds { get; set; } = 0.8f;
    public float Range { get; set; } = 1.8f;
    public float EnemySpeed { get; set; } = 2.3f;
    public float VisionRange { get; set; } = 28f;
    public bool AutoPauseOnCombatStart { get; set; } = true;
    public void Validate()
    {
        if (PlayerHealth is <= 0 or > 10000 || EnemyHealth is <= 0 or > 10000 ||
            Damage is <= 0 or > 10000 ||
            !float.IsFinite(CastSeconds) || CastSeconds is <= 0 or > 10 ||
            !float.IsFinite(CooldownSeconds) || CooldownSeconds is < 0 or > 10 ||
            !float.IsFinite(Range) || Range is < 0.5f or > 5 ||
            !float.IsFinite(EnemySpeed) || EnemySpeed is < 0 or > 20 ||
            !float.IsFinite(VisionRange) || VisionRange is <= 0 or > 100)
            throw new InvalidDataException("Invalid dungeon combat settings.");
    }
}

public sealed class DungeonSettings
{
    public float MoveSpeed { get; set; } = 4.5f;
    public float KeyboardTurnSpeed { get; set; } = 2.4f;
    public float MouseSensitivity { get; set; } = 0.003f;
    public float ControllerLookSpeed { get; set; } = 2.4f;
    public float MoveDeadzone { get; set; } = 0.18f;
    public float LookDeadzone { get; set; } = 0.12f;
    public float CameraDistance { get; set; } = 5f;
    public float WideCameraDistance { get; set; } = 8f;
    public float TopDownThreshold { get; set; } = 10f;
    public float TopDownCameraDistance { get; set; } = 14f;
    public float MaxZoomDistance { get; set; } = 30f;
    public float MouseZoomStep { get; set; } = 1.5f;
    public float FreeCameraSpeed { get; set; } = 10f;
    public float ControllerCursorSpeed { get; set; } = 650f;
    public float InteractionRange { get; set; } = 2.5f;
    public float CameraPitch { get; set; } = 0.45f;
    public CombatSettings Combat { get; set; } = new();
    public void Validate()
    {
        if (!float.IsFinite(InteractionRange) || InteractionRange is < 0.5f or > 5 ||
            !float.IsFinite(MoveSpeed) || MoveSpeed is <= 0 or > 20 ||
            !float.IsFinite(KeyboardTurnSpeed) || KeyboardTurnSpeed is <= 0 or > 10 ||
            !float.IsFinite(MouseSensitivity) || MouseSensitivity is <= 0 or > 0.1f ||
            !float.IsFinite(ControllerLookSpeed) || ControllerLookSpeed is <= 0 or > 10 ||
            !float.IsFinite(MoveDeadzone) || MoveDeadzone is < 0 or >= 1 ||
            !float.IsFinite(LookDeadzone) || LookDeadzone is < 0 or >= 1 ||
            !float.IsFinite(CameraDistance) || CameraDistance is < 2 or > 12 ||
            !float.IsFinite(WideCameraDistance) || WideCameraDistance <= CameraDistance ||
            !float.IsFinite(TopDownThreshold) || TopDownThreshold <= WideCameraDistance || TopDownThreshold > 25 ||
            !float.IsFinite(TopDownCameraDistance) || TopDownCameraDistance <= TopDownThreshold ||
            !float.IsFinite(MaxZoomDistance) || MaxZoomDistance < TopDownCameraDistance || MaxZoomDistance > 80 ||
            !float.IsFinite(MouseZoomStep) || MouseZoomStep is <= 0 or > 10 ||
            !float.IsFinite(FreeCameraSpeed) || FreeCameraSpeed is <= 0 or > 30 ||
            !float.IsFinite(ControllerCursorSpeed) || ControllerCursorSpeed is <= 0 or > 2000 ||
            !float.IsFinite(CameraPitch) || CameraPitch is < -0.18f or > 1.25f)
            throw new InvalidDataException("Invalid dungeon movement/camera settings.");
        if (Combat is null) throw new InvalidDataException("Combat settings are required.");
        Combat.Validate();
    }
}

namespace CCM.Game;

public sealed class StartingItemDefinition
{
    public string ItemId { get; set; } = "";
    public int Quantity { get; set; }
}

public sealed class GameplaySettings
{
    public bool AutoAttackSelectedEnemy { get; set; }
}

public sealed class CombatSettings
{
    public bool AutoPauseOnCombatStart { get; set; } = true;
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
    public float FireplaceLightIntensity { get; set; } = 1f;
    public CombatSettings Combat { get; set; } = new();
    public string EnemyId { get; set; } = "dungeon-guard";
    public List<StartingItemDefinition> StartingItems { get; set; } = [];
    public List<string> StartingEquipped { get; set; } = [];
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
            !float.IsFinite(CameraPitch) || CameraPitch is < -0.18f or > 1.25f ||
            !float.IsFinite(FireplaceLightIntensity) ||
            FireplaceLightIntensity is < 0 or > 3)
            throw new InvalidDataException("Invalid dungeon movement/camera settings.");
        if (Combat is null || string.IsNullOrWhiteSpace(EnemyId) ||
            StartingItems is null || StartingEquipped is null ||
            StartingItems.Any(stack=>stack is null || string.IsNullOrWhiteSpace(stack.ItemId) ||
                stack.Quantity<=0) ||
            StartingEquipped.Any(string.IsNullOrWhiteSpace) ||
            StartingEquipped.Distinct(StringComparer.Ordinal).Count()!=StartingEquipped.Count)
            throw new InvalidDataException("Combat settings, EnemyId and starting equipment are required.");
    }
}

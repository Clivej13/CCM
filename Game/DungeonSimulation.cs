using System.Numerics;
using RaylibGameFramework.ThreeD;
namespace CCM.Game;

public sealed class DungeonSimulation
{
    public DungeonWorld World { get; } = new();
    public Transform3D Player { get; private set; } = Transform3D.Identity;
    public InteractionTarget? Target { get; private set; }
    public string? InteractionMessage { get; private set; }
    public void UpdateInteraction(bool activate)
    {
        Target = InteractionQuery.Find(World,Player.Position+new Vector3(0,1.2f,0),
            CameraPosition,CameraTarget-CameraPosition,settings.InteractionRange);
        InteractionMessage = null;
        if (activate && Target is { } target)
        {
            bool changed = target.Item.Activate(Player.Position);
            InteractionMessage = changed
                ? $"{target.Item.Name} {(target.Item.IsOpen ? "opened" : "closed")}"
                : "Step away from the doorway";
            // The door moved: refresh targeting so no stale object stays highlighted.
            Target = InteractionQuery.Find(World,Player.Position+new Vector3(0,1.2f,0),
                CameraPosition,CameraTarget-CameraPosition,settings.InteractionRange);
        }
    }
    public float Yaw { get; private set; }
    public float Pitch { get; private set; }
    private readonly DungeonSettings settings;
    private Vector3 CameraAnchor => Player.Position + new Vector3(0,1.2f,0);
    // Aim the screen centre just ahead of the character so nearby objects stay under the reticle.
    public Vector3 CameraTarget => CameraAnchor + new Vector3(-MathF.Sin(Yaw),0,-MathF.Cos(Yaw)) * 1.5f - new Vector3(0,0.3f,0);
    public Vector3 CameraPosition => World.CameraPosition(CameraAnchor, CameraAnchor +
        new Vector3(MathF.Sin(Yaw)*MathF.Cos(Pitch), MathF.Sin(Pitch), MathF.Cos(Yaw)*MathF.Cos(Pitch)) * settings.CameraDistance);
    public DungeonSimulation(DungeonSettings settings) { settings.Validate(); this.settings = settings; Reset(); }
    public void Reset()
    {
        World.Reset(); Target = null; InteractionMessage = null;
        Player = new Transform3D(World.Spawn, Quaternion.Identity, Vector3.One);
        Yaw = 0; Pitch = settings.CameraPitch;
    }
    public void Update(Vector2 move, Vector2 look, bool mouseLook, float seconds)
    {
        if (!float.IsFinite(seconds) || seconds <= 0) return;
        seconds = Math.Min(seconds, 0.1f);
        if (!float.IsFinite(move.X) || !float.IsFinite(move.Y) ||
            !float.IsFinite(look.X) || !float.IsFinite(look.Y)) return;
        float sensitivity = mouseLook ? settings.MouseSensitivity : settings.ControllerLookSpeed * seconds;
        Yaw = MathF.IEEERemainder(Yaw - look.X * sensitivity, 2 * MathF.PI);
        Pitch = Math.Clamp(Pitch + look.Y * sensitivity, 0.12f, 1.25f);
        if (move.LengthSquared() > 1) move = Vector2.Normalize(move);
        var forward = new Vector3(-MathF.Sin(Yaw),0,-MathF.Cos(Yaw));
        var right = new Vector3(MathF.Cos(Yaw),0,-MathF.Sin(Yaw));
        var direction = right * move.X + forward * move.Y;
        var player = Player;
        player.Position = World.Move(player.Position, direction * (settings.MoveSpeed * seconds));
        if (direction.LengthSquared() > 0.0001f)
            player.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.Atan2(-direction.X,-direction.Z));
        Player = player;
    }
}

using System.Numerics;

namespace CCM.Game;

public sealed class DungeonSimulation
{
    private readonly DungeonSettings settings;
    private bool attackRequested;
    public DungeonWorld World { get; } = new();
    public PlayerEntity Player { get; private set; }
    public EnemyEntity Enemy { get; private set; }
    public InteractionTarget? Target { get; private set; }
    public string? InteractionMessage { get; private set; }
    public string? CombatMessage { get; private set; }
    public bool CombatStarted { get; private set; }
    public bool Paused { get; private set; }
    public bool EnemySelected { get; private set; }
    public bool AutoAttackEnabled => attackRequested;
    public float Yaw { get; private set; }
    public float Pitch { get; private set; }
    private Vector3 CameraAnchor => Player.Position + new Vector3(0,1.2f,0);
    // Aim the screen centre just ahead of the character so nearby objects stay under the reticle.
    public Vector3 CameraTarget => CameraAnchor + new Vector3(-MathF.Sin(Yaw),0,-MathF.Cos(Yaw)) * 1.5f - new Vector3(0,0.3f,0);
    public Vector3 CameraPosition => World.CameraPosition(CameraAnchor, CameraAnchor +
        new Vector3(MathF.Sin(Yaw)*MathF.Cos(Pitch), MathF.Sin(Pitch), MathF.Cos(Yaw)*MathF.Cos(Pitch)) * settings.CameraDistance);

    public DungeonSimulation(DungeonSettings settings)
    {
        settings.Validate();
        this.settings = settings;
        Player = new(World.Spawn,settings.Combat);
        Enemy = new(new(0,0,-30),settings.Combat);
        Reset();
    }
    public void Reset()
    {
        World.Reset();
        Player = new(World.Spawn,settings.Combat);
        Enemy = new(new(0,0,-30),settings.Combat);
        Target = null;
        InteractionMessage = CombatMessage = null;
        CombatStarted = Paused = EnemySelected = attackRequested = false;
        Yaw = 0;
        Pitch = settings.CameraPitch;
    }
    public void TogglePause() => Paused = !Paused;

    public void UpdateInteraction(bool activate)
    {
        if (Paused || !Player.Alive) return;
        Target = InteractionQuery.Find(World,CameraAnchor,CameraPosition,
            CameraTarget-CameraPosition,settings.InteractionRange);
        InteractionMessage = null;
        if (!activate || Target is not { } target) return;
        bool changed = target.Item.Activate(Player.Position);
        InteractionMessage = changed
            ? $"{target.Item.Name} {(target.Item.IsOpen ? "opened" : "closed")}"
            : "Step away from the doorway";
        if (changed && target.Item.Kind == InteractionKind.Door && target.Item.IsOpen && !CombatStarted)
        {
            CombatStarted = true;
            Paused = settings.Combat.AutoPauseOnCombatStart;
            InteractionMessage = Paused ? "Combat started - paused" : "Combat started";
        }
        Target = InteractionQuery.Find(World,CameraAnchor,CameraPosition,
            CameraTarget-CameraPosition,settings.InteractionRange);
    }

    public bool SelectEnemy()
    {
        if (Paused || !CombatStarted || !Player.Alive || !Enemy.Alive) return false;
        var ray = Vector3.Normalize(CameraTarget-CameraPosition) * 100;
        var bounds = new Block(Enemy.Position+new Vector3(0,0.9f,0),new(1,1.8f,1));
        if (!DungeonWorld.SegmentHit(CameraPosition,ray,bounds.Min,bounds.Max,out float hit)) return false;
        var point = CameraPosition + ray*hit;
        if (Vector3.Distance(CameraAnchor,point)>20 || !World.ClearLine(CameraPosition,point)) return false;
        EnemySelected = true;
        return true;
    }
    public void RequestAttack()
    {
        if (!Paused && Player.Alive && EnemySelected && Enemy.Alive) attackRequested = true;
    }
    public void UpdateCombat(float seconds)
    {
        CombatMessage = null;
        if (!CombatStarted || Paused || !float.IsFinite(seconds) || seconds <= 0) return;
        seconds = Math.Min(seconds,0.1f);
        if (!Enemy.Alive || !Player.Alive) return;
        var distance = Vector2.Distance(new(Enemy.Position.X,Enemy.Position.Z),
            new(Player.Position.X,Player.Position.Z));
        if (distance > settings.Combat.Range*0.85f)
        {
            var offset = Player.Position-Enemy.Position;
            offset.Y = 0;
            if (offset.LengthSquared()>0.0001f)
                Enemy.Position = World.Move(Enemy.Position,Vector3.Normalize(offset)*settings.Combat.EnemySpeed*seconds);
        }
        bool clear = World.ClearLine(Player.Position+Vector3.UnitY,Enemy.Position+Vector3.UnitY);
        bool playerCanHit = Player.InRange(Enemy) && clear;
        bool enemyCanHit = Enemy.InRange(Player) && clear;
        if (attackRequested && Player.UpdateAttack(seconds,Enemy,playerCanHit))
            CombatMessage = $"Enemy hit - {Enemy.Health}/{Enemy.MaxHealth} HP";
        else if (!attackRequested) Player.CancelAttack();
        if (Enemy.Alive && Enemy.UpdateAttack(seconds,Player,enemyCanHit))
            CombatMessage = $"Player hit - {Player.Health}/{Player.MaxHealth} HP";
        if (!Enemy.Alive)
        {
            attackRequested = false;
            CombatMessage = "Enemy defeated";
        }
        else if (!Player.Alive)
        {
            attackRequested = false;
            CombatMessage = "Player defeated - Escape to menu";
        }
    }

    public void Update(Vector2 move, Vector2 look, bool mouseLook, float seconds)
    {
        if (Paused || !Player.Alive || !float.IsFinite(seconds) || seconds <= 0) return;
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
        Player.Position = World.Move(Player.Position,direction * (settings.MoveSpeed * seconds));
        if (direction.LengthSquared() > 0.0001f)
            Player.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.Atan2(-direction.X,-direction.Z));
    }
}

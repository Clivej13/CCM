using System.Numerics;
using RaylibGameFramework.ThreeD;

namespace CCM.Game;

public enum AttackPhase { Ready, Casting, Cooldown }

/// <summary>Shared health and melee timing for player and enemies.</summary>
public abstract class CombatEntity
{
    private float phaseSeconds;
    private readonly CombatSettings stats;
    protected CombatEntity(Vector3 position, int health, CombatSettings stats)
    {
        this.stats = stats;
        MaxHealth = health;
        Health = health;
        Transform = new Transform3D(position, Quaternion.Identity, Vector3.One);
    }
    public Transform3D Transform { get; private set; }
    public Vector3 Position
    {
        get => Transform.Position;
        set { var transform = Transform; transform.Position = value; Transform = transform; }
    }
    public Quaternion Rotation
    {
        get => Transform.Rotation;
        set { var transform = Transform; transform.Rotation = value; Transform = transform; }
    }
    public Vector3 Forward => Transform.Forward;
    public Vector3 TransformPoint(Vector3 point) => Transform.TransformPoint(point);
    public int MaxHealth { get; }
    public int Health { get; private set; }
    public bool Alive => Health > 0;
    public AttackPhase Phase { get; private set; }
    public float AttackProgress => Phase == AttackPhase.Casting
        ? 1 - phaseSeconds / stats.CastSeconds : 0;
    public void ReceiveDamage(int damage) => Health = Math.Max(0, Health - Math.Max(0, damage));
    public void CancelAttack() { Phase = AttackPhase.Ready; phaseSeconds = 0; }

    /// <summary>Returns true only when the cast completes and damage lands. Range is checked at start and impact.</summary>
    public bool UpdateAttack(float seconds, CombatEntity? target, bool canHit)
    {
        if (!Alive || !float.IsFinite(seconds) || seconds <= 0) return false;
        if (Phase == AttackPhase.Ready)
        {
            if (target is null || !target.Alive || !canHit || !InRange(target)) return false;
            Phase = AttackPhase.Casting;
            phaseSeconds = stats.CastSeconds;
        }
        phaseSeconds = Math.Max(0, phaseSeconds - seconds);
        if (phaseSeconds > 0) return false;
        if (Phase == AttackPhase.Casting)
        {
            bool landed = target is { Alive: true } && canHit && InRange(target);
            if (landed) target!.ReceiveDamage(stats.Damage);
            Phase = AttackPhase.Cooldown;
            phaseSeconds = stats.CooldownSeconds;
            return landed;
        }
        Phase = AttackPhase.Ready;
        return false;
    }
    public bool InRange(CombatEntity target) =>
        Vector2.Distance(new(Position.X, Position.Z), new(target.Position.X, target.Position.Z)) <= stats.Range;
}

public sealed class PlayerEntity : CombatEntity
{
    public Inventory Inventory { get; } = new();
    public Equipment Equipment { get; }
    public PlayerEntity(Vector3 position, CombatSettings stats, ItemCatalogue? items=null)
        : base(position,stats.PlayerHealth,stats)
    {
        Equipment=new Equipment(Inventory,items ?? new ItemCatalogue());
    }
}

public sealed class EnemyEntity(Vector3 position, CombatSettings stats)
    : CombatEntity(position, stats.EnemyHealth, stats);

using System.Numerics;
using RaylibGameFramework.ThreeD;

namespace CCM.Game;

public enum AttackPhase { Ready, Casting, Cooldown }

/// <summary>Players and enemies share attributes, mitigation, health and attack timing.</summary>
public abstract class CombatEntity
{
    private float phaseSeconds;
    private CombatProfile activeAttack;
    protected CombatEntity(Vector3 position,CharacterAttributes attributes)
    {
        Attributes=attributes ?? throw new ArgumentNullException(nameof(attributes));
        attributes.Validate();
        MaxHealth=CombatFormula.Calculate(attributes).MaxHealth;
        Health=MaxHealth;
        MaxStamina=60+attributes.Endurance*10;
        Stamina=MaxStamina;
        Transform=new Transform3D(position,Quaternion.Identity,Vector3.One);
    }
    public CharacterAttributes Attributes { get; private set; }
    public abstract CombatProfile Profile { get; }
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
    public int MaxHealth { get; private set; }
    public int Health { get; private set; }
    public int MaxStamina { get; private set; }
    public float Stamina { get; private set; }
    public StatusCollection Statuses { get; } = new();
    public bool Alive => Health>0;
    protected void ApplyAttributes(CharacterAttributes next)
    {
        next.Validate();
        int missingHealth=MaxHealth-Health;
        float missingStamina=MaxStamina-Stamina;
        Attributes=next.Copy();
        MaxHealth=CombatFormula.Calculate(Attributes).MaxHealth;
        MaxStamina=60+Attributes.Endurance*10;
        Health=Health==0 ? 0 : Math.Clamp(MaxHealth-missingHealth,1,MaxHealth);
        Stamina=Math.Clamp(MaxStamina-missingStamina,0,MaxStamina);
        CancelAttack();
    }
    public bool TrySpendStamina(int cost)
    {
        if (cost<0 || !Alive || Stamina<cost) return false;
        Stamina-=cost;
        return true;
    }
    public void RegenerateStamina(float seconds)
    {
        if (Alive && float.IsFinite(seconds) && seconds>0)
            Stamina=Math.Min(MaxStamina,Stamina+8f*seconds);
    }
    public AttackPhase Phase { get; private set; }
    public float AttackProgress => Phase==AttackPhase.Casting
        ? 1-phaseSeconds/activeAttack.CastSeconds : 0;
    public void ReceiveDamage(int rawDamage) =>
        Health=Math.Max(0,Health-CombatFormula.DamageAfterDefence(rawDamage,Profile));
    public void CancelAttack() { Phase=AttackPhase.Ready; phaseSeconds=0; }

    /// <summary>Returns true only when the cast completes and damage lands. Range is checked at start and impact.</summary>
    public bool UpdateAttack(float seconds,CombatEntity? target,bool canHit)
    {
        if (!Alive || !float.IsFinite(seconds) || seconds<=0) return false;
        if (Statuses.BlocksActions) { CancelAttack(); return false; }
        if (Phase==AttackPhase.Ready)
        {
            activeAttack=Profile;
            if (target is null || !target.Alive || !canHit || !InRange(target,activeAttack.Range))
                return false;
            Phase=AttackPhase.Casting;
            phaseSeconds=activeAttack.CastSeconds;
        }
        phaseSeconds=Math.Max(0,phaseSeconds-seconds);
        if (phaseSeconds>0) return false;
        if (Phase==AttackPhase.Casting)
        {
            bool landed=target is { Alive:true } && canHit && InRange(target,activeAttack.Range);
            if (landed) target!.ReceiveDamage(activeAttack.Damage);
            Phase=AttackPhase.Cooldown;
            phaseSeconds=activeAttack.CooldownSeconds;
            return landed;
        }
        Phase=AttackPhase.Ready;
        return false;
    }
    public bool InRange(CombatEntity target) => InRange(target,Profile.Range);
    private bool InRange(CombatEntity target,float range) =>
        Vector2.Distance(new(Position.X,Position.Z),new(target.Position.X,target.Position.Z))<=range;
}

public sealed class PlayerEntity : CombatEntity
{
    private readonly ItemCatalogue items;
    public Inventory Inventory { get; } = new();
    public Equipment Equipment { get; }
    public AbilityBook Abilities { get; }
    public IReadOnlyList<string> SkillIds { get; private set; }
    public PlayerEntity(Vector3 position,CharacterAttributes attributes,ItemCatalogue? items=null,
        IEnumerable<string>? skillIds=null,IEnumerable<string>? abilityIds=null)
        : base(position,attributes)
    {
        this.items=items ?? new ItemCatalogue();
        Equipment=new Equipment(Inventory,this.items);
        SkillIds=skillIds?.ToArray() ?? [];
        Abilities=new AbilityBook(abilityIds);
    }
    public override CombatProfile Profile => CombatFormula.Calculate(Attributes,
        Equipped(EquipmentSlot.Weapon),Equipped(EquipmentSlot.Body),Equipped(EquipmentSlot.Quiver));
    public ItemVisual? EquippedWeaponVisual => Equipped(EquipmentSlot.Weapon)?.VisualKind;
    public void ApplyBuild(PlayerDefinition previous,PlayerDefinition next)
    {
        next.Validate();
        ApplyAttributes(next.Attributes);
        SkillIds=next.SkillIds.ToArray();
        Abilities.UpdateStartingAbilities(previous.AbilityIds,next.AbilityIds);
    }
    private ItemDefinition? Equipped(EquipmentSlot slot) =>
        Equipment.In(slot) is { } id ? items.Get(id) : null;
}

public sealed class EnemyEntity : CombatEntity
{
    private readonly ItemCatalogue items;
    public EnemyDefinition Definition { get; }
    public EnemyEntity(Vector3 position,EnemyDefinition definition,ItemCatalogue? items=null)
        : base(position,definition.Attributes)
    {
        Definition=new EnemyDefinition
        {
            Id=definition.Id, Name=definition.Name, Attributes=definition.Attributes,
            MoveSpeed=definition.MoveSpeed, VisionRange=definition.VisionRange,
            WeaponId=definition.WeaponId, ArmourId=definition.ArmourId,
            OnHitStatusId=definition.OnHitStatusId
        };
        this.items=items ?? new ItemCatalogue();
    }
    public override CombatProfile Profile => CombatFormula.Calculate(Attributes,
        Definition.WeaponId is { } weapon ? items.Get(weapon) : null,
        Definition.ArmourId is { } armour ? items.Get(armour) : null);
}

namespace CCM.Game;

/// <summary>Shared innate attributes for players and enemies.</summary>
public sealed class CharacterAttributes
{
    public int Strength { get; init; } = 10;
    public int Endurance { get; init; } = 10;
    public int Agility { get; init; } = 10;
    public int Intelligence { get; init; } = 10;
    public int Willpower { get; init; } = 10;
    public void Validate()
    {
        if (Strength is < 1 or > 100 || Endurance is < 1 or > 100 ||
            Agility is < 1 or > 100 || Intelligence is < 1 or > 100 ||
            Willpower is < 1 or > 100)
            throw new InvalidDataException("Character attributes must be between 1 and 100.");
    }
}

public sealed class PlayerDefinition
{
    public CharacterAttributes Attributes { get; set; } = new();
    public void Validate()
    {
        if (Attributes is null) throw new InvalidDataException("Player attributes are required.");
        Attributes.Validate();
    }
}

public sealed class EnemyDefinition
{
    public string Id { get; set; } = "dungeon-guard";
    public string Name { get; set; } = "Dungeon guard";
    public CharacterAttributes Attributes { get; set; } = new() { Endurance=3 };
    public float MoveSpeed { get; set; } = 2.3f;
    public float VisionRange { get; set; } = 28f;
    public string? WeaponId { get; set; }
    public string? ArmourId { get; set; }

    public void Validate(ItemCatalogue items)
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name) ||
            Attributes is null || !float.IsFinite(MoveSpeed) || MoveSpeed is < 0 or > 20 ||
            !float.IsFinite(VisionRange) || VisionRange is <= 0 or > 100)
            throw new InvalidDataException("Invalid enemy definition.");
        Attributes.Validate();
        if (WeaponId is not null && items.Get(WeaponId).EquipmentSlot!=EquipmentSlot.Weapon)
            throw new InvalidDataException($"Enemy {Id} weapon must use the Weapon slot.");
        if (ArmourId is not null && items.Get(ArmourId).EquipmentSlot!=EquipmentSlot.Body)
            throw new InvalidDataException($"Enemy {Id} armour must use the Body slot.");
    }
}

public sealed class EnemyCatalogue
{
    public List<EnemyDefinition> Enemies { get; set; } = [];
    public void Validate(ItemCatalogue items)
    {
        if (Enemies is null || Enemies.Count==0)
            throw new InvalidDataException("Enemy catalogue is empty.");
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach (var enemy in Enemies)
        {
            if (enemy is null) throw new InvalidDataException("Enemy definition is null.");
            enemy.Validate(items);
            if (!ids.Add(enemy.Id))
                throw new InvalidDataException($"Duplicate enemy ID: {enemy.Id}");
        }
    }
    public EnemyDefinition Get(string id) =>
        Enemies.FirstOrDefault(enemy=>enemy.Id==id)
        ?? throw new KeyNotFoundException($"Unknown enemy: {id}");
}

/// <summary>Live values calculated from innate attributes and equipped catalogue items.</summary>
public readonly record struct CombatProfile(
    int MaxHealth,int Damage,float Range,float AttacksPerSecond,
    float CastSeconds,float CooldownSeconds,int Armour,int ResistancePercent);

public static class CombatFormula
{
    public const float UnarmedRange=1.8f;

    public static CombatProfile Calculate(CharacterAttributes attributes,
        ItemDefinition? weapon=null,ItemDefinition? armour=null,ItemDefinition? quiver=null)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        attributes.Validate();
        int health=40+attributes.Endurance*6;
        int damage=2+attributes.Strength+(weapon?.AttackPower ?? 0);
        float speed=(weapon?.AttackSpeed ?? 0.7f)*(0.75f+attributes.Agility*0.025f);
        speed=Math.Clamp(speed,0.2f,4f);
        float interval=1f/speed;
        float cast=interval*0.42f;
        float cooldown=interval-cast;
        float range=weapon?.AttackRange ?? UnarmedRange;
        int defence=attributes.Endurance/5+
            (armour?.Protection ?? 0)+(quiver?.Protection ?? 0);
        int resistance=Math.Min(60,attributes.Willpower+attributes.Intelligence/2);
        return new(health,damage,range,speed,cast,cooldown,defence,resistance);
    }

    public static int DamageAfterDefence(int raw,CombatProfile defender)
    {
        if (raw<=0) return 0;
        int afterArmour=Math.Max(1,raw-defender.Armour);
        return Math.Max(1,(int)MathF.Ceiling(afterArmour*(1-defender.ResistancePercent/100f)));
    }
}

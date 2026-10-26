namespace CCM.Game;

/// <summary>Resolved on a successful ability hit. A conditional status is consumed for its bonus.</summary>
public sealed class AbilityCombat
{
    public int StaminaCost { get; set; }
    public float CastSeconds { get; set; }
    public float RecoverySeconds { get; set; } = 4f;
    public float DamageMultiplier { get; set; } = 1f;
    public string? ApplyStatusId { get; set; }
    public string? ConsumeStatusId { get; set; }
    public string? ConditionalApplyStatusId { get; set; }
    public float ConditionalDamageMultiplier { get; set; } = 1f;

    public void Validate(StatusCatalogue? statuses)
    {
        if (StaminaCost is < 1 or > 100 || !float.IsFinite(CastSeconds) ||
            CastSeconds is < 0 or > 3 || !float.IsFinite(RecoverySeconds) ||
            RecoverySeconds is < 0 or > 30 || !float.IsFinite(DamageMultiplier) ||
            DamageMultiplier is <= 0 or > 5 || !float.IsFinite(ConditionalDamageMultiplier) ||
            ConditionalDamageMultiplier is < 1 or > 5 ||
            (ConsumeStatusId is null &&
             (ConditionalApplyStatusId is not null || ConditionalDamageMultiplier!=1)))
            throw new InvalidDataException("Invalid ability combat values.");
        foreach (string? id in new[] { ApplyStatusId,ConsumeStatusId,ConditionalApplyStatusId })
            if (id is not null)
            {
                if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException("Empty ability status ID.");
                statuses?.Get(id);
            }
    }
}

/// <summary>Catalogue data describes learned actions and optional combat effects.</summary>
public sealed class AbilityDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Path { get; set; } = "";
    public List<SkillRequirement> Attributes { get; set; } = [];
    public List<string> RequiredSkills { get; set; } = [];
    public string? RequiredEquipmentSlot { get; set; }
    public string? RequiredWeaponVisual { get; set; }
    public AbilityCombat? Combat { get; set; }

    public void Validate(SkillCatalogue skills,StatusCatalogue? statuses=null)
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name) ||
            string.IsNullOrWhiteSpace(Description) || string.IsNullOrWhiteSpace(Path) ||
            Attributes is null || RequiredSkills is null ||
            Attributes.Count>SkillDefinition.MaxRequirements ||
            RequiredSkills.Any(string.IsNullOrWhiteSpace) ||
            RequiredSkills.Distinct(StringComparer.Ordinal).Count()!=RequiredSkills.Count)
            throw new InvalidDataException($"Invalid ability definition: {Id}");
        var kinds=new HashSet<AttributeKind>();
        foreach (var requirement in Attributes)
        {
            if (requirement is null) throw new InvalidDataException($"Null ability requirement: {Id}");
            requirement.Validate();
            if (!kinds.Add(requirement.Kind))
                throw new InvalidDataException($"Duplicate ability attribute: {Id}");
        }
        foreach (var skillId in RequiredSkills) skills.Get(skillId);
        if (RequiredEquipmentSlot is not null &&
            (!Enum.TryParse<EquipmentSlot>(RequiredEquipmentSlot,true,out var slot) ||
             !Enum.IsDefined(slot) || slot==EquipmentSlot.None))
            throw new InvalidDataException($"Invalid ability equipment requirement: {Id}");
        if (RequiredWeaponVisual is not null &&
            (!Enum.TryParse<ItemVisual>(RequiredWeaponVisual,true,out var visual) ||
             visual is not (ItemVisual.Sword or ItemVisual.Bow or ItemVisual.Staff)))
            throw new InvalidDataException($"Invalid ability weapon requirement: {Id}");
        if (Combat is null)
            throw new InvalidDataException($"Ability has no implemented combat effect: {Id}");
        Combat.Validate(statuses);
    }

    public bool MeetsRequirements(CharacterAttributes attributes,IReadOnlyCollection<string> skills) =>
        Attributes.All(requirement=>attributes.Get(requirement.Kind)>=requirement.Minimum) &&
        RequiredSkills.All(skills.Contains);

    public bool HasRequiredEquipment(Equipment equipment) =>
        RequiredEquipmentSlot is null ||
        equipment.In(Enum.Parse<EquipmentSlot>(RequiredEquipmentSlot,true)) is not null;
}

public sealed class AbilityCatalogue
{
    public int StartingAbilityPoints { get; set; } = 2;
    public List<AbilityDefinition> Abilities { get; set; } = [];

    public void Validate(SkillCatalogue skills,StatusCatalogue? statuses=null)
    {
        if (StartingAbilityPoints is < 1 or > 20 || Abilities is null ||
            Abilities.Count<StartingAbilityPoints)
            throw new InvalidDataException("Ability catalogue needs enough entries for its starting points.");
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach (var ability in Abilities)
        {
            if (ability is null) throw new InvalidDataException("Null ability definition.");
            ability.Validate(skills,statuses);
            if (!ids.Add(ability.Id))
                throw new InvalidDataException($"Duplicate ability ID: {ability.Id}");
        }
    }

    public AbilityDefinition Get(string id) =>
        Abilities.FirstOrDefault(ability=>ability.Id==id)
        ?? throw new KeyNotFoundException($"Unknown ability: {id}");

    public IReadOnlyList<AbilityDefinition> OrderedFor(CharacterAttributes attributes,
        IReadOnlyCollection<string> skills) =>
        Abilities.OrderByDescending(ability=>ability.MeetsRequirements(attributes,skills)).ToArray();
}

/// <summary>Learned actions survive changing equipment or a later attribute respec.</summary>
public sealed class AbilityBook
{
    private readonly HashSet<string> learned=new(StringComparer.Ordinal);
    public AbilityLoadout Loadout { get; } = new();
    public AbilityBook(IEnumerable<string>? initial=null)
    {
        if (initial is not null)
            foreach (var id in initial) learned.Add(id);
    }
    public IReadOnlyCollection<string> LearnedIds => learned;

    public bool Knows(string id) => learned.Contains(id);

    public void UpdateStartingAbilities(IEnumerable<string> previous,IEnumerable<string> current)
    {
        var next=new HashSet<string>(current,StringComparer.Ordinal);
        foreach (var id in previous)
            if (!next.Contains(id))
            {
                learned.Remove(id);
                Loadout.ClearAbility(id);
            }
        learned.UnionWith(next);
    }

    public bool TryLearnFromTome(string tomeId,Inventory bag,ItemCatalogue items,
        AbilityCatalogue abilities,CharacterAttributes attributes,IReadOnlyCollection<string> skills)
    {
        var tome=items.Get(tomeId);
        if (tome.ItemType!=ItemType.Tome || tome.AbilityId is null ||
            bag.Count(tomeId)==0) return false;
        var ability=abilities.Get(tome.AbilityId);
        if (Knows(ability.Id) || !ability.MeetsRequirements(attributes,skills)) return false;
        if (!bag.Remove(tomeId,1)) return false;
        learned.Add(ability.Id);
        return true;
    }

    public bool CanUse(AbilityDefinition ability,PlayerEntity player) =>
        Knows(ability.Id) &&
        ability.MeetsRequirements(player.Attributes,player.SkillIds) &&
        ability.HasRequiredEquipment(player.Equipment) &&
        (ability.RequiredWeaponVisual is null ||
         player.EquippedWeaponVisual==Enum.Parse<ItemVisual>(ability.RequiredWeaponVisual,true)) &&
        (ability.RequiredWeaponVisual!="Bow" || player.HasAmmo);
}

/// <summary>Player-owned assignments shared by the keyboard bar and controller wheel.</summary>
public sealed class AbilityLoadout
{
    public const int SlotCount=12;
    public const int WheelSlotCount=8;
    private static readonly string[] KeyLabels=
        ["1","2","3","4","5","6","7","8","9","0","-","="];
    private readonly string?[] slots=new string?[SlotCount];
    public string? In(int index) => slots[Valid(index)];
    public static string KeyLabel(int index) => KeyLabels[Valid(index)];

    public bool Assign(int index,string abilityId,AbilityBook book)
    {
        Valid(index);
        if (!book.Knows(abilityId)) return false;
        slots[index]=abilityId;
        return true;
    }

    public void Clear(int index) => slots[Valid(index)]=null;

    public void ClearAbility(string abilityId)
    {
        for (int i=0;i<slots.Length;i++)
            if (slots[i]==abilityId) slots[i]=null;
    }

    private static int Valid(int index) =>
        index>=0 && index<SlotCount ? index : throw new ArgumentOutOfRangeException(nameof(index));
}

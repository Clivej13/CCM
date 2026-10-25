namespace CCM.Game;

public enum AttributeKind { Strength, Endurance, Agility, Intelligence, Willpower, Speed, Charisma, Luck }

public sealed class SkillRequirement
{
    public string Attribute { get; set; } = "";
    public int Minimum { get; set; }
    public AttributeKind Kind => Enum.Parse<AttributeKind>(Attribute, true);

    public void Validate()
    {
        if (!Enum.TryParse<AttributeKind>(Attribute, true, out _) ||
            !CharacterAttributes.IsValid(Minimum))
            throw new InvalidDataException($"Invalid skill requirement: {Attribute} {Minimum}.");
    }
}

public sealed class SkillDefinition
{
    public const int MaxRequirements = 3;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<SkillRequirement> Requirements { get; set; } = [];

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name) ||
            string.IsNullOrWhiteSpace(Description) || Requirements is null ||
            Requirements.Count is < 1 or > MaxRequirements)
            throw new InvalidDataException($"Skill ID, name, description and 1-{MaxRequirements} requirements are required.");
        var kinds = new HashSet<AttributeKind>();
        foreach (var requirement in Requirements)
        {
            if (requirement is null) throw new InvalidDataException($"Null requirement for {Id}.");
            requirement.Validate();
            if (!kinds.Add(requirement.Kind))
                throw new InvalidDataException($"Duplicate requirement for {Id}: {requirement.Attribute}.");
        }
    }

    public bool IsAvailable(CharacterAttributes attributes) =>
        Requirements.All(requirement => attributes.Get(requirement.Kind) >= requirement.Minimum);
}

public sealed class SkillCatalogue
{
    public int SkillPoints { get; set; } = 2;
    public List<SkillDefinition> Skills { get; set; } = [];

    public void Validate()
    {
        if (SkillPoints is < 1 or > 20 || Skills is null || Skills.Count < SkillPoints)
            throw new InvalidDataException("Skill catalogue needs enough skills for its point limit.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var skill in Skills)
        {
            if (skill is null) throw new InvalidDataException("Null skill definition.");
            skill.Validate();
            if (!ids.Add(skill.Id)) throw new InvalidDataException($"Duplicate skill ID: {skill.Id}.");
        }
    }

    public SkillDefinition Get(string id) =>
        Skills.FirstOrDefault(skill => skill.Id == id)
        ?? throw new KeyNotFoundException($"Unknown skill: {id}");

    public IReadOnlyList<SkillDefinition> OrderedFor(CharacterAttributes attributes) =>
        Skills.OrderByDescending(skill => skill.IsAvailable(attributes)).ToArray();
}

/// <summary>A temporary setup draft. Attributes are free; skills and abilities use separate points.</summary>
public sealed class CharacterBuild
{
    private readonly SkillCatalogue catalogue;
    private readonly AbilityCatalogue? abilities;
    private readonly HashSet<string> selected = new(StringComparer.Ordinal);
    private readonly HashSet<string> selectedAbilities = new(StringComparer.Ordinal);
    public CharacterAttributes Attributes { get; private set; }
    public IReadOnlyCollection<string> SelectedSkills => selected;
    public IReadOnlyCollection<string> SelectedAbilities => selectedAbilities;
    public int RemainingSkillPoints => catalogue.SkillPoints - selected.Count;
    public int RemainingAbilityPoints => (abilities?.StartingAbilityPoints ?? 0) - selectedAbilities.Count;
    public bool CanComplete => RemainingSkillPoints == 0 && RemainingAbilityPoints == 0;

    public CharacterBuild(CharacterAttributes initial, SkillCatalogue catalogue,
        AbilityCatalogue? abilities=null)
    {
        this.catalogue = catalogue;
        this.abilities = abilities;
        catalogue.Validate();
        abilities?.Validate(catalogue);
        initial.Validate();
        Attributes = initial.Copy();
    }

    public bool SetAttribute(AttributeKind kind, int value)
    {
        if (!CharacterAttributes.IsValid(value)) return false;
        Attributes = Attributes.With(kind, value);
        selected.RemoveWhere(id => !catalogue.Get(id).IsAvailable(Attributes));
        RefundUnavailableAbilities();
        return true;
    }

    public bool ToggleSkill(string id)
    {
        var skill = catalogue.Get(id);
        if (selected.Remove(id))
        {
            RefundUnavailableAbilities();
            return true;
        }
        if (RemainingSkillPoints == 0 || !skill.IsAvailable(Attributes)) return false;
        return selected.Add(id);
    }

    public bool ToggleAbility(string id)
    {
        if (abilities is null) return false;
        var ability=abilities.Get(id);
        if (selectedAbilities.Remove(id)) return true;
        if (RemainingAbilityPoints==0 || !ability.MeetsRequirements(Attributes,selected))
            return false;
        return selectedAbilities.Add(id);
    }

    private void RefundUnavailableAbilities()
    {
        if (abilities is not null)
            selectedAbilities.RemoveWhere(id=>
                !abilities.Get(id).MeetsRequirements(Attributes,selected));
    }

    public PlayerDefinition Complete()
    {
        if (!CanComplete) throw new InvalidOperationException("Spend all skill and ability points before entering the dungeon.");
        return new PlayerDefinition
        {
            Attributes = Attributes.Copy(),
            SkillIds = catalogue.Skills.Where(skill => selected.Contains(skill.Id))
                .Select(skill => skill.Id).ToList(),
            AbilityIds = abilities?.Abilities.Where(ability=>selectedAbilities.Contains(ability.Id))
                .Select(ability=>ability.Id).ToList() ?? []
        };
    }
}

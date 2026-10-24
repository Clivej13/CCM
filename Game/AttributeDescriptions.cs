namespace CCM.Game;

public sealed class AttributeDescription
{
    public string Attribute { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Details { get; set; } = "";
    public AttributeKind Kind => Enum.Parse<AttributeKind>(Attribute,true);
}

// TODO(character-system): Review Config/attributes.json and Config/skills.json whenever
// attribute formulas or skill effects change. See docs/character-system-copy.md.
public sealed class AttributeDescriptionCatalogue
{
    public List<AttributeDescription> Attributes { get; set; } = [];

    public void Validate()
    {
        if (Attributes is null || Attributes.Count!=Enum.GetValues<AttributeKind>().Length)
            throw new InvalidDataException("Attribute descriptions must cover every attribute.");
        var kinds=new HashSet<AttributeKind>();
        foreach (var entry in Attributes)
        {
            if (entry is null || !Enum.TryParse<AttributeKind>(entry.Attribute,true,out var kind) ||
                !Enum.IsDefined(kind) || string.IsNullOrWhiteSpace(entry.Summary) ||
                string.IsNullOrWhiteSpace(entry.Details) || !kinds.Add(kind))
                throw new InvalidDataException("Attribute descriptions contain an invalid or duplicate entry.");
        }
    }

    public AttributeDescription Get(AttributeKind kind) =>
        Attributes.First(entry=>entry.Kind==kind);
}

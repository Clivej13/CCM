namespace CCM.Game;

public enum StatusTone { Beneficial, Warning, Dangerous }

public sealed class StatusDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public float DurationSeconds { get; set; }
    public string Tone { get; set; } = "Warning";
    public bool BlocksActions { get; set; }
    public StatusTone DisplayTone => Enum.Parse<StatusTone>(Tone,true);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name) ||
            string.IsNullOrWhiteSpace(Description) || !float.IsFinite(DurationSeconds) ||
            DurationSeconds is <= 0 or > 60 ||
            !Enum.TryParse<StatusTone>(Tone,true,out var tone) || !Enum.IsDefined(tone))
            throw new InvalidDataException($"Invalid status definition: {Id}");
    }
}

public sealed class StatusCatalogue
{
    public List<StatusDefinition> Statuses { get; set; } = [];
    public void Validate()
    {
        if (Statuses is null || Statuses.Count==0)
            throw new InvalidDataException("Status catalogue is empty.");
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach (var status in Statuses)
        {
            if (status is null) throw new InvalidDataException("Null status definition.");
            status.Validate();
            if (!ids.Add(status.Id))
                throw new InvalidDataException($"Duplicate status ID: {status.Id}");
        }
    }
    public StatusDefinition Get(string id) =>
        Statuses.FirstOrDefault(status=>status.Id==id)
        ?? throw new KeyNotFoundException($"Unknown status: {id}");
}

public sealed class ActiveStatus(StatusDefinition definition,float remainingSeconds)
{
    public StatusDefinition Definition { get; } = definition;
    public float RemainingSeconds { get; internal set; } = remainingSeconds;
}

/// <summary>Timed effects belong to an individual combatant and refresh on reapplication.</summary>
public sealed class StatusCollection
{
    private readonly Dictionary<string,ActiveStatus> active=new(StringComparer.Ordinal);
    public IReadOnlyCollection<ActiveStatus> Active => active.Values;
    public bool BlocksActions => active.Values.Any(status=>status.Definition.BlocksActions);
    public bool Has(string id) => active.ContainsKey(id);
    public void Apply(StatusDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        active[definition.Id]=new(definition,definition.DurationSeconds);
    }
    public bool Consume(string id) => active.Remove(id);
    public void Tick(float seconds)
    {
        if (!float.IsFinite(seconds) || seconds<=0) return;
        foreach (var status in active.Values)
            status.RemainingSeconds-=seconds;
        foreach (var id in active.Where(pair=>pair.Value.RemainingSeconds<=0)
                     .Select(pair=>pair.Key).ToArray())
            active.Remove(id);
    }
}

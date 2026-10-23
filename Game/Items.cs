namespace CCM.Game;

public enum EquipmentSlot { None, Weapon, Body, Quiver }
public enum ItemVisual { Sword, Bow, Staff, Armour, Quiver, Arrow }

public sealed class ItemDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Slot { get; set; } = "None";
    public string Visual { get; set; } = "";
    public bool Stackable { get; set; }
    public EquipmentSlot EquipmentSlot =>
        Enum.TryParse<EquipmentSlot>(Slot,true,out var slot) ? slot : EquipmentSlot.None;
    public ItemVisual VisualKind => Enum.Parse<ItemVisual>(Visual,true);
}

public sealed class ItemCatalogue
{
    public List<ItemDefinition> Items { get; set; } = [];
    public void Validate()
    {
        if (Items is null || Items.Count==0) throw new InvalidDataException("Item catalogue is empty.");
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in Items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Id) ||
                string.IsNullOrWhiteSpace(item.Name) ||
                !Enum.TryParse<EquipmentSlot>(item.Slot,true,out var slot) ||
                !Enum.IsDefined(slot) ||
                !Enum.TryParse<ItemVisual>(item.Visual,true,out var visual) ||
                !Enum.IsDefined(visual) || !ids.Add(item.Id))
                throw new InvalidDataException("Invalid or duplicate item definition.");
            if (item.Stackable && slot!=EquipmentSlot.None)
                throw new InvalidDataException($"Equippable item {item.Id} cannot be stackable.");
        }
    }
    public ItemDefinition Get(string id) =>
        Items.FirstOrDefault(item=>item.Id==id)
        ?? throw new KeyNotFoundException($"Unknown item: {id}");
}

public sealed record ItemStack(string ItemId,int Quantity);

public sealed class ContainerDefinition
{
    public string Id { get; set; } = "";
    public List<ItemStack> Contents { get; set; } = [];
}

public sealed class ContainerCatalogue
{
    public List<ContainerDefinition> Containers { get; set; } = [];
    public void Validate(ItemCatalogue items)
    {
        if (Containers is null || Containers.Count==0)
            throw new InvalidDataException("Container catalogue is empty.");
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach (var container in Containers)
        {
            if (container is null || string.IsNullOrWhiteSpace(container.Id) ||
                container.Contents is null || !ids.Add(container.Id))
                throw new InvalidDataException("Invalid or duplicate container definition.");
            var contents=new HashSet<string>(StringComparer.Ordinal);
            foreach (var stack in container.Contents)
            {
                if (stack is null || string.IsNullOrWhiteSpace(stack.ItemId) ||
                    stack.Quantity<=0 || !contents.Add(stack.ItemId))
                    throw new InvalidDataException($"Invalid contents in {container.Id}.");
                var definition=items.Get(stack.ItemId);
                if (!definition.Stackable && stack.Quantity!=1)
                    throw new InvalidDataException($"Non-stackable item {stack.ItemId} must have quantity one.");
            }
        }
    }
    public ContainerDefinition Get(string id) =>
        Containers.FirstOrDefault(container=>container.Id==id)
        ?? throw new KeyNotFoundException($"Unknown container: {id}");
}

/// <summary>Counts item IDs; definitions and visuals always come from ItemCatalogue.</summary>
public sealed class Inventory
{
    private readonly List<ItemStack> stacks=[];
    public IReadOnlyList<ItemStack> Stacks => stacks;
    public Inventory(IEnumerable<ItemStack>? initial=null) => Reset(initial);
    public void Reset(IEnumerable<ItemStack>? initial=null)
    {
        stacks.Clear();
        if (initial is not null)
            foreach (var stack in initial) Add(stack.ItemId,stack.Quantity);
    }
    public int Count(string id) => stacks.FirstOrDefault(s=>s.ItemId==id)?.Quantity ?? 0;
    public int CountVisual(ItemCatalogue catalogue,ItemVisual visual) =>
        catalogue.Items.Where(item=>item.VisualKind==visual).Sum(item=>Count(item.Id));
    public void Add(string id,int quantity)
    {
        if (string.IsNullOrWhiteSpace(id) || quantity<=0) throw new ArgumentOutOfRangeException(nameof(quantity));
        int index=stacks.FindIndex(s=>s.ItemId==id);
        if (index<0) stacks.Add(new(id,quantity));
        else stacks[index]=stacks[index] with { Quantity=checked(stacks[index].Quantity+quantity) };
    }
    public bool Remove(string id,int quantity)
    {
        if (quantity<=0) return false;
        int index=stacks.FindIndex(s=>s.ItemId==id);
        if (index<0 || stacks[index].Quantity<quantity) return false;
        int remaining=stacks[index].Quantity-quantity;
        if (remaining==0) stacks.RemoveAt(index);
        else stacks[index]=stacks[index] with { Quantity=remaining };
        return true;
    }
    public bool TransferTo(Inventory destination,string id,int quantity)
    {
        if (ReferenceEquals(this,destination) || quantity<=0 || Count(id)<quantity ||
            destination.Count(id)>int.MaxValue-quantity) return false;
        destination.Add(id,quantity);
        Remove(id,quantity);
        return true;
    }
}

public sealed class Equipment(Inventory inventory,ItemCatalogue catalogue)
{
    private readonly Dictionary<EquipmentSlot,string> equipped=[];
    public string? In(EquipmentSlot slot) => equipped.GetValueOrDefault(slot);
    public bool Equip(string id)
    {
        if (inventory.Count(id)==0) return false;
        var slot=catalogue.Get(id).EquipmentSlot;
        if (slot==EquipmentSlot.None) return false;
        equipped[slot]=id;
        return true;
    }
    public void Clear() => equipped.Clear();
}

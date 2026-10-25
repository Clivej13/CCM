namespace CCM.Game;

public enum EquipmentSlot { None, Weapon, Body, Quiver }
public enum ItemVisual { Sword, Bow, Staff, Armour, Quiver, Arrow, Tome }
public enum ItemType { Weapon, Apparel, Ammunition, Tome }
public enum ItemFilter { All, Weapons, Apparel, Tomes }

public sealed class ItemDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Slot { get; set; } = "None";
    public string Visual { get; set; } = "";
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public float Weight { get; set; }
    public int AttackPower { get; set; }
    public float AttackRange { get; set; }
    public float AttackSpeed { get; set; }
    public int Protection { get; set; }
    public int Value { get; set; }
    public bool Stackable { get; set; }
    public string? AbilityId { get; set; }
    public EquipmentSlot EquipmentSlot =>
        Enum.TryParse<EquipmentSlot>(Slot,true,out var slot) ? slot : EquipmentSlot.None;
    public ItemVisual VisualKind => Enum.Parse<ItemVisual>(Visual,true);
    public ItemType ItemType => Enum.Parse<ItemType>(Type,true);
}

public sealed class ItemCatalogue
{
    public List<ItemDefinition> Items { get; set; } = [];
    public void Validate() => Validate(null);
    public void Validate(AbilityCatalogue? abilities)
    {
        if (Items is null || Items.Count==0) throw new InvalidDataException("Item catalogue is empty.");
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in Items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Id) ||
                string.IsNullOrWhiteSpace(item.Name) ||
                string.IsNullOrWhiteSpace(item.Description) ||
                !Enum.TryParse<EquipmentSlot>(item.Slot,true,out var slot) ||
                !Enum.IsDefined(slot) ||
                !Enum.TryParse<ItemVisual>(item.Visual,true,out var visual) ||
                !Enum.IsDefined(visual) ||
                !Enum.TryParse<ItemType>(item.Type,true,out var type) ||
                !Enum.IsDefined(type) ||
                !float.IsFinite(item.Weight) || item.Weight<0 ||
                item.AttackPower<0 || !float.IsFinite(item.AttackRange) ||
                !float.IsFinite(item.AttackSpeed) || item.Protection<0 || item.Value<0 ||
                !ids.Add(item.Id))
                throw new InvalidDataException("Invalid or duplicate item definition.");
            if (item.Stackable && slot!=EquipmentSlot.None)
                throw new InvalidDataException($"Equippable item {item.Id} cannot be stackable.");
            if (type==ItemType.Weapon && slot!=EquipmentSlot.Weapon ||
                type==ItemType.Apparel && slot is not (EquipmentSlot.Body or EquipmentSlot.Quiver) ||
                (type is ItemType.Ammunition or ItemType.Tome) && slot!=EquipmentSlot.None)
                throw new InvalidDataException($"Item {item.Id} has an incompatible type and slot.");
            if (type==ItemType.Weapon && (item.AttackRange is < 0.5f or > 5f ||
                    item.AttackSpeed is < 0.2f or > 4f) ||
                type!=ItemType.Weapon && (item.AttackRange!=0 || item.AttackSpeed!=0))
                throw new InvalidDataException($"Item {item.Id} has invalid weapon range or speed.");
            if (type==ItemType.Tome)
            {
                if (visual!=ItemVisual.Tome || item.Stackable ||
                    string.IsNullOrWhiteSpace(item.AbilityId) ||
                    item.AttackPower!=0 || item.Protection!=0)
                    throw new InvalidDataException($"Invalid ability tome: {item.Id}");
                abilities?.Get(item.AbilityId);
            }
            else if (item.AbilityId is not null)
                throw new InvalidDataException($"Only a tome can reference an ability: {item.Id}");
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

/// <summary>The bag owns unequipped item stacks; equipment has separate ownership.</summary>
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
    public IReadOnlyList<ItemStack> Filter(ItemCatalogue catalogue,ItemFilter filter) => filter switch
    {
        ItemFilter.All => stacks,
        ItemFilter.Weapons => stacks.Where(stack=>catalogue.Get(stack.ItemId).ItemType==ItemType.Weapon).ToArray(),
        ItemFilter.Apparel => stacks.Where(stack=>catalogue.Get(stack.ItemId).ItemType==ItemType.Apparel).ToArray(),
        ItemFilter.Tomes => stacks.Where(stack=>catalogue.Get(stack.ItemId).ItemType==ItemType.Tome).ToArray(),
        _ => throw new ArgumentOutOfRangeException(nameof(filter))
    };
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
        string? previous=In(slot);
        if (previous is not null && inventory.Count(previous)==int.MaxValue) return false;
        if (!inventory.Remove(id,1)) return false;
        if (previous is not null) inventory.Add(previous,1);
        equipped[slot]=id;
        return true;
    }
    public bool Unequip(EquipmentSlot slot)
    {
        if (slot==EquipmentSlot.None || In(slot) is not { } id ||
            inventory.Count(id)==int.MaxValue) return false;
        inventory.Add(id,1);
        equipped.Remove(slot);
        return true;
    }
    public void Clear()
    {
        foreach (var slot in equipped.Keys.ToArray())
            if (!Unequip(slot))
                throw new InvalidOperationException($"Could not return equipped item in {slot} to the bag.");
    }
}

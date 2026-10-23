using CCM.Application;
using CCM.Game;
using Xunit;

namespace CCM.Tests;

public sealed class InventoryTests
{
    [Fact]
    public void BundledCataloguePopulatesChestAndEquipsItemsById()
    {
        var items=Settings.Required<ItemCatalogue>(Settings.ConfigPath("items.json"));
        items.Validate();
        var containers=Settings.Required<ContainerCatalogue>(Settings.ConfigPath("containers.json"));
        containers.Validate(items);
        var contents=containers.Get("starter-chest").Contents;
        var simulation=new DungeonSimulation(new(),items:items,chestContents:contents);
        var chest=Assert.IsType<ChestInteractable>(simulation.World.Interactables[1]);
        Assert.Equal(6,chest.Contents.Stacks.Count);
        Assert.Equal(20,chest.Contents.Count("arrow"));

        Assert.True(chest.Contents.TransferTo(simulation.Player.Inventory,"iron-sword",1));
        Assert.True(simulation.Player.Equipment.Equip("iron-sword"));
        Assert.Equal("iron-sword",simulation.Player.Equipment.In(EquipmentSlot.Weapon));
        Assert.Equal(0,simulation.Player.Inventory.Count("iron-sword"));
        Assert.True(chest.Contents.TransferTo(simulation.Player.Inventory,"short-bow",1));
        Assert.True(simulation.Player.Equipment.Equip("short-bow"));
        Assert.Equal("short-bow",simulation.Player.Equipment.In(EquipmentSlot.Weapon));
        Assert.Equal(0,simulation.Player.Inventory.Count("short-bow"));
        Assert.Equal(1,simulation.Player.Inventory.Count("iron-sword")); // replaced gear returns to the bag

        foreach (string id in new[] { "leather-armour","leather-quiver","oak-staff" })
            Assert.True(chest.Contents.TransferTo(simulation.Player.Inventory,id,1));
        Assert.True(simulation.Player.Equipment.Equip("leather-armour"));
        Assert.True(simulation.Player.Equipment.Equip("leather-quiver"));
        Assert.True(simulation.Player.Equipment.Equip("oak-staff"));
        Assert.Equal("leather-armour",simulation.Player.Equipment.In(EquipmentSlot.Body));
        Assert.Equal("leather-quiver",simulation.Player.Equipment.In(EquipmentSlot.Quiver));
        Assert.Equal("oak-staff",simulation.Player.Equipment.In(EquipmentSlot.Weapon));
        Assert.Equal(0,simulation.Player.Inventory.Count("oak-staff"));
        Assert.Equal(0,simulation.Player.Inventory.Count("leather-armour"));
        Assert.Equal(0,simulation.Player.Inventory.Count("leather-quiver"));
        Assert.True(simulation.Player.Equipment.Unequip(EquipmentSlot.Quiver));
        Assert.Null(simulation.Player.Equipment.In(EquipmentSlot.Quiver));
        Assert.Equal(1,simulation.Player.Inventory.Count("leather-quiver"));
        Assert.True(simulation.Player.Equipment.Equip("leather-quiver"));
        Assert.True(chest.Contents.TransferTo(simulation.Player.Inventory,"arrow",20));
        Assert.Equal(20,simulation.Player.Inventory.Count("arrow"));
        Assert.False(simulation.Player.Equipment.Equip("arrow"));
        Assert.Empty(chest.Contents.Stacks);

        simulation.Reset();
        chest=Assert.IsType<ChestInteractable>(simulation.World.Interactables[1]);
        Assert.Equal(6,chest.Contents.Stacks.Count);
        Assert.Empty(simulation.Player.Inventory.Stacks);
        Assert.Null(simulation.Player.Equipment.In(EquipmentSlot.Weapon));
    }

    [Fact]
    public void MissingOrDuplicateCatalogueReferencesFailBeforeGameplay()
    {
        var items=Settings.Required<ItemCatalogue>(Settings.ConfigPath("items.json"));
        items.Validate();
        var containers=new ContainerCatalogue
        {
            Containers=[new() { Id="starter-chest",Contents=[new("missing-item",1)] }]
        };
        Assert.Throws<KeyNotFoundException>(()=>containers.Validate(items));
        containers.Containers[0].Contents=[new("arrow",0)];
        Assert.Throws<InvalidDataException>(()=>containers.Validate(items));
        containers.Containers[0].Contents=[new("iron-sword",2)];
        Assert.Throws<InvalidDataException>(()=>containers.Validate(items));

        items.Items.Add(new ItemDefinition
            { Id="iron-sword",Name="Duplicate",Slot="Weapon",Visual="Sword" });
        Assert.Throws<InvalidDataException>(items.Validate);
    }

    [Fact]
    public void CatalogueStatsFiltersAndEquipmentOwnershipAreConsistent()
    {
        var items=Settings.Required<ItemCatalogue>(Settings.ConfigPath("items.json"));
        items.Validate();
        var bag=new Inventory(items.Items.Select(item=>new ItemStack(item.Id,1)));
        Assert.Equal(6,bag.Filter(items,ItemFilter.All).Count);
        Assert.Equal(3,bag.Filter(items,ItemFilter.Weapons).Count);
        Assert.Equal(2,bag.Filter(items,ItemFilter.Apparel).Count);
        var sword=items.Get("iron-sword");
        Assert.Equal(ItemType.Weapon,sword.ItemType);
        Assert.Equal(EquipmentSlot.Weapon,sword.EquipmentSlot);
        Assert.True(sword.Weight>0);
        Assert.True(sword.AttackPower>0);
        Assert.True(sword.Value>0);
        Assert.False(string.IsNullOrWhiteSpace(sword.Description));

        var equipment=new Equipment(bag,items);
        Assert.True(equipment.Equip("iron-sword"));
        Assert.Equal(2,bag.Filter(items,ItemFilter.Weapons).Count);
        Assert.Equal(0,bag.Count("iron-sword"));
        Assert.False(equipment.Equip("iron-sword"));
        Assert.True(equipment.Unequip(EquipmentSlot.Weapon));
        Assert.Equal(3,bag.Filter(items,ItemFilter.Weapons).Count);
        Assert.False(equipment.Unequip(EquipmentSlot.Weapon));
        Assert.False(equipment.Unequip(EquipmentSlot.None));
        Assert.True(equipment.Equip("iron-sword"));
        equipment.Clear();
        Assert.Null(equipment.In(EquipmentSlot.Weapon));
        Assert.Equal(1,bag.Count("iron-sword"));

        sword.Weight=float.NaN;
        Assert.Throws<InvalidDataException>(items.Validate);
        sword.Weight=3.2f;
        sword.Type="Apparel";
        Assert.Throws<InvalidDataException>(items.Validate);
        sword.Type="Weapon";
        sword.Description="";
        Assert.Throws<InvalidDataException>(items.Validate);
    }

    [Fact]
    public void ChestInteractionRaisesLootEventAndKeepsContentsUntilTaken()
    {
        var items=Settings.Required<ItemCatalogue>(Settings.ConfigPath("items.json"));
        var contents=Settings.Required<ContainerCatalogue>(Settings.ConfigPath("containers.json"))
            .Get("starter-chest").Contents;
        var simulation=new DungeonSimulation(new(),items:items,chestContents:contents);
        simulation.Player.Position=new(3.1f,0,-5.2f);
        simulation.UpdateAim(new(3.1f,1.2f,-3),-System.Numerics.Vector3.UnitZ);
        Assert.IsType<ChestInteractable>(simulation.Target?.Item);
        Assert.True(simulation.TryInteractVisible());
        Assert.NotNull(simulation.ChestOpened);
        Assert.True(simulation.ChestOpened.IsOpen);
        Assert.Equal(6,simulation.ChestOpened.Contents.Stacks.Count);
        simulation.ClearInteractionMessage();
        Assert.Null(simulation.ChestOpened);
        simulation.UpdateAim(new(3.1f,1.2f,-3),-System.Numerics.Vector3.UnitZ);
        Assert.True(simulation.TryInteractVisible()); // searching again reopens the panel
        Assert.NotNull(simulation.ChestOpened);
    }
}

using System.Numerics;
using CCM.Application;
using CCM.Game;
using Xunit;

namespace CCM.Tests;

public sealed class AbilityTests
{
    private static (SkillCatalogue Skills,AbilityCatalogue Abilities,ItemCatalogue Items,
        ContainerCatalogue Containers) Bundled()
    {
        var skills=Settings.Required<SkillCatalogue>(Settings.ConfigPath("skills.json"));
        skills.Validate();
        var abilities=Settings.Required<AbilityCatalogue>(Settings.ConfigPath("abilities.json"));
        abilities.Validate(skills);
        var items=Settings.Required<ItemCatalogue>(Settings.ConfigPath("items.json"));
        items.Validate(abilities);
        var containers=Settings.Required<ContainerCatalogue>(Settings.ConfigPath("containers.json"));
        containers.Validate(items);
        return (skills,abilities,items,containers);
    }

    [Fact]
    public void CharacterSetupSpendsTwoAbilityPointsAndSeedsTheDungeonPlayer()
    {
        var (skills,abilities,items,_)=Bundled();
        var attributes=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json")).Attributes;
        var build=new CharacterBuild(attributes,skills,abilities);
        Assert.Equal(2,build.RemainingAbilityPoints);
        Assert.True(build.ToggleAbility("second-wind"));
        Assert.False(build.ToggleAbility("arcane-pulse"));
        Assert.True(build.ToggleSkill("arcane-spark"));
        Assert.True(build.ToggleSkill("quickstep"));
        Assert.True(build.ToggleAbility("arcane-pulse"));
        Assert.False(build.ToggleAbility("swift-step")); // both ability points are spent
        Assert.True(build.CanComplete);
        var definition=build.Complete();
        Assert.Equal(new[] { "second-wind","arcane-pulse" },definition.AbilityIds);

        var simulation=new DungeonSimulation(new DungeonSettings(),items:items,
            playerDefinition:definition);
        Assert.True(simulation.Player.Abilities.Knows("second-wind"));
        Assert.True(simulation.Player.Abilities.Knows("arcane-pulse"));
        simulation.Reset();
        Assert.True(simulation.Player.Abilities.Knows("second-wind"));
        Assert.True(simulation.Player.Abilities.Knows("arcane-pulse"));
    }

    [Fact]
    public void LoweringAttributesOrRemovingSkillsRefundsStartingAbilityPoints()
    {
        var (skills,abilities,_,_)=Bundled();
        var attributes=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json")).Attributes;
        var build=new CharacterBuild(attributes,skills,abilities);
        Assert.True(build.ToggleAbility("second-wind"));
        Assert.True(build.ToggleAbility("clear-mind"));
        Assert.True(build.SetAttribute(AttributeKind.Endurance,3));
        Assert.DoesNotContain("second-wind",build.SelectedAbilities);
        Assert.Equal(1,build.RemainingAbilityPoints);
        Assert.True(build.ToggleSkill("arcane-spark"));
        Assert.True(build.ToggleAbility("arcane-pulse"));
        Assert.True(build.ToggleSkill("arcane-spark")); // removing the prerequisite
        Assert.DoesNotContain("arcane-pulse",build.SelectedAbilities);
        Assert.Equal(1,build.RemainingAbilityPoints);
        Assert.False(build.CanComplete);
    }

    [Fact]
    public void AvailableSetupAbilitiesSortAheadOfLockedPaths()
    {
        var (skills,abilities,_,_)=Bundled();
        var attributes=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json")).Attributes;
        var build=new CharacterBuild(attributes,skills,abilities);
        Assert.Equal(new[] { "second-wind","clear-mind" },
            abilities.OrderedFor(build.Attributes,build.SelectedSkills)
                .Take(2).Select(ability=>ability.Id));
        Assert.True(build.ToggleSkill("arcane-spark"));
        Assert.Equal(new[] { "second-wind","clear-mind","arcane-pulse" },
            abilities.OrderedFor(build.Attributes,build.SelectedSkills)
                .Take(3).Select(ability=>ability.Id));
    }

    [Fact]
    public void TomeNeedsTrainingAndAttributesThenTeachesOnce()
    {
        var (_,abilities,items,containers)=Bundled();
        var player=new PlayerEntity(Vector3.Zero,
            new CharacterAttributes
            {
                Strength=4,Endurance=4,Agility=4,Intelligence=4,
                Willpower=4,Speed=4,Charisma=4,Luck=4
            },items,["power-strike"]);
        var chest=new Inventory(containers.Get("starter-chest").Contents);
        Assert.True(chest.TransferTo(player.Inventory,"tome-guard-break",1));
        Assert.Single(player.Inventory.Filter(items,ItemFilter.Tomes));
        Assert.False(player.Abilities.TryLearnFromTome("tome-guard-break",player.Inventory,
            items,abilities,player.Attributes,player.SkillIds));
        Assert.Equal(1,player.Inventory.Count("tome-guard-break"));

        var strongAttributes=player.Attributes.With(AttributeKind.Strength,5);
        var untrained=new PlayerEntity(Vector3.Zero,strongAttributes,items);
        Assert.True(player.Inventory.TransferTo(untrained.Inventory,"tome-guard-break",1));
        Assert.False(untrained.Abilities.TryLearnFromTome("tome-guard-break",
            untrained.Inventory,items,abilities,untrained.Attributes,untrained.SkillIds));
        Assert.Equal(1,untrained.Inventory.Count("tome-guard-break"));

        var strongPlayer=new PlayerEntity(Vector3.Zero,strongAttributes,items,["power-strike"]);
        Assert.True(untrained.Inventory.TransferTo(strongPlayer.Inventory,"tome-guard-break",1));
        Assert.True(strongPlayer.Abilities.TryLearnFromTome("tome-guard-break",
            strongPlayer.Inventory,items,abilities,strongPlayer.Attributes,strongPlayer.SkillIds));
        Assert.True(strongPlayer.Abilities.Knows("guard-break"));
        Assert.Equal(0,strongPlayer.Inventory.Count("tome-guard-break"));
        Assert.False(strongPlayer.Abilities.CanUse(abilities.Get("guard-break"),strongPlayer));

        strongPlayer.Inventory.Add("iron-sword",1);
        Assert.True(strongPlayer.Equipment.Equip("iron-sword"));
        Assert.True(strongPlayer.Abilities.CanUse(abilities.Get("guard-break"),strongPlayer));
        Assert.True(strongPlayer.Equipment.Unequip(EquipmentSlot.Weapon));
        Assert.True(strongPlayer.Abilities.Knows("guard-break"));
        Assert.False(strongPlayer.Abilities.CanUse(abilities.Get("guard-break"),strongPlayer));
        strongPlayer.Inventory.Add("tome-guard-break",1);
        Assert.False(strongPlayer.Abilities.TryLearnFromTome("tome-guard-break",
            strongPlayer.Inventory,items,abilities,strongPlayer.Attributes,strongPlayer.SkillIds));
        Assert.Equal(1,strongPlayer.Inventory.Count("tome-guard-break"));
    }

    [Fact]
    public void DifferentTrainingPathsCanLearnDifferentTomes()
    {
        var (_,abilities,items,containers)=Bundled();
        var chest=new Inventory(containers.Get("starter-chest").Contents);
        var attributes=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json")).Attributes;
        var player=new PlayerEntity(Vector3.Zero,attributes,items,["arcane-spark","quickstep"]);
        foreach (string id in new[] { "tome-arcane-pulse","tome-swift-step" })
            Assert.True(chest.TransferTo(player.Inventory,id,1));
        Assert.True(player.Abilities.TryLearnFromTome("tome-arcane-pulse",player.Inventory,
            items,abilities,player.Attributes,player.SkillIds));
        Assert.True(player.Abilities.TryLearnFromTome("tome-swift-step",player.Inventory,
            items,abilities,player.Attributes,player.SkillIds));
        Assert.Equal(2,player.Abilities.LearnedIds.Count);
        Assert.True(player.Abilities.CanUse(abilities.Get("arcane-pulse"),player));
        Assert.True(player.Abilities.CanUse(abilities.Get("swift-step"),player));
        Assert.False(player.Abilities.Knows("guard-break"));
    }

    [Fact]
    public void InvalidAbilityAndTomeReferencesFailValidation()
    {
        var (skills,abilities,items,_)=Bundled();
        abilities.Get("arcane-pulse").RequiredSkills.Add("unknown-skill");
        Assert.Throws<KeyNotFoundException>(()=>abilities.Validate(skills));
        abilities.Get("arcane-pulse").RequiredSkills.Remove("unknown-skill");

        items.Get("tome-arcane-pulse").AbilityId="unknown-ability";
        Assert.Throws<KeyNotFoundException>(()=>items.Validate(abilities));
        items.Get("tome-arcane-pulse").AbilityId="arcane-pulse";
        items.Get("tome-arcane-pulse").Slot="Weapon";
        Assert.Throws<InvalidDataException>(()=>items.Validate(abilities));
    }
}

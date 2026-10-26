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
    public void AbilityCastsMustFinishBeforeTheirRelatedStatusDurations()
    {
        var skills=Settings.Required<SkillCatalogue>(Settings.ConfigPath("skills.json"));
        var statuses=Settings.Required<StatusCatalogue>(Settings.ConfigPath("statuses.json"));
        var abilities=Settings.Required<AbilityCatalogue>(Settings.ConfigPath("abilities.json"));
        abilities.Validate(skills,statuses);
        var aimed=abilities.Get("bow-aimed-shot").Combat!;
        Assert.True(aimed.CastSeconds<statuses.Get("pinned").DurationSeconds);
        Assert.True(aimed.CastSeconds<statuses.Get("marked").DurationSeconds);
        aimed.CastSeconds=statuses.Get("pinned").DurationSeconds;
        Assert.Throws<InvalidDataException>(()=>abilities.Validate(skills,statuses));
    }

    [Fact]
    public void UnspentPointsCanBeTakenIntoTheDungeonAndAllocatedLater()
    {
        var (skills,abilities,_,_)=Bundled();
        var initial=new PlayerDefinition
        {
            Attributes=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json"))
                .Attributes
        };
        var first=new CharacterBuild(initial,skills,abilities);
        Assert.True(first.HasUnspentPoints);
        var entered=first.Complete();
        Assert.Empty(entered.SkillIds);
        Assert.Empty(entered.AbilityIds);

        var later=new CharacterBuild(entered,skills,abilities);
        Assert.True(later.ToggleSkill("sword-novice"));
        Assert.True(later.ToggleAbility("sword-quick-cut"));
        var partial=later.Complete();
        Assert.Equal(1,later.RemainingSkillPoints);
        Assert.Equal(1,later.RemainingAbilityPoints);

        var revisit=new CharacterBuild(partial,skills,abilities,["sword-opening-break"]);
        Assert.False(revisit.ToggleAbility("sword-opening-break")); // tome learning costs no point
        Assert.Equal(1,revisit.RemainingSkillPoints);
        Assert.Equal(1,revisit.RemainingAbilityPoints);
        Assert.Contains("sword-quick-cut",revisit.SelectedAbilities);
        Assert.True(revisit.SetAttribute(AttributeKind.Strength,5));
        Assert.True(revisit.ToggleSkill("sword-adept"));
        Assert.True(revisit.ToggleAbility("sword-driving-cut"));
        Assert.False(revisit.HasUnspentPoints);
    }

    [Fact]
    public void CharacterSetupSpendsTwoAbilityPointsAndSeedsTheDungeonPlayer()
    {
        var (skills,abilities,items,_)=Bundled();
        var attributes=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json")).Attributes;
        var build=new CharacterBuild(attributes,skills,abilities);
        Assert.Equal(2,build.RemainingAbilityPoints);
        Assert.False(build.ToggleAbility("sword-quick-cut"));
        Assert.True(build.ToggleSkill("sword-novice"));
        Assert.True(build.SetAttribute(AttributeKind.Strength,5));
        Assert.True(build.ToggleSkill("sword-adept"));
        Assert.True(build.ToggleAbility("sword-quick-cut"));
        Assert.True(build.ToggleAbility("sword-opening-break"));
        Assert.False(build.ToggleAbility("sword-driving-cut")); // both ability points are spent
        Assert.False(build.HasUnspentPoints);
        var definition=build.Complete();
        Assert.Equal(new[] { "sword-quick-cut","sword-opening-break" },definition.AbilityIds);

        var simulation=new DungeonSimulation(new DungeonSettings(),items:items,
            playerDefinition:definition);
        Assert.True(simulation.Player.Abilities.Knows("sword-quick-cut"));
        Assert.True(simulation.Player.Abilities.Knows("sword-opening-break"));
        simulation.Reset();
        Assert.True(simulation.Player.Abilities.Knows("sword-quick-cut"));
        Assert.True(simulation.Player.Abilities.Knows("sword-opening-break"));
    }

    [Fact]
    public void LoweringAnAttributeRefundsAnUnavailableStartingAbility()
    {
        var (skills,abilities,_,_)=Bundled();
        var attributes=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json")).Attributes;
        var build=new CharacterBuild(attributes,skills,abilities);
        Assert.True(build.ToggleSkill("sword-novice"));
        Assert.True(build.ToggleAbility("sword-quick-cut"));
        Assert.True(build.ToggleAbility("sword-opening-break"));
        Assert.True(build.SetAttribute(AttributeKind.Agility,3));
        Assert.DoesNotContain("sword-quick-cut",build.SelectedAbilities);
        Assert.Contains("sword-opening-break",build.SelectedAbilities);
        Assert.Equal(1,build.RemainingAbilityPoints);
        Assert.True(build.HasUnspentPoints);
    }

    [Fact]
    public void RemovingNoviceRefundsAdeptAndBothDependentAbilities()
    {
        var (skills,abilities,_,_)=Bundled();
        var attributes=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json"))
            .Attributes.With(AttributeKind.Strength,5);
        var build=new CharacterBuild(attributes,skills,abilities);
        Assert.True(build.ToggleSkill("sword-novice"));
        Assert.True(build.ToggleSkill("sword-adept"));
        Assert.True(build.ToggleAbility("sword-opening-break"));
        Assert.True(build.ToggleAbility("sword-finishing-cut"));
        Assert.True(build.ToggleSkill("sword-novice"));
        Assert.Empty(build.SelectedSkills);
        Assert.Empty(build.SelectedAbilities);
        Assert.Equal(2,build.RemainingSkillPoints);
        Assert.Equal(2,build.RemainingAbilityPoints);
    }

    [Fact]
    public void AvailableSetupAbilitiesSortAheadOfLockedAbilities()
    {
        var (skills,abilities,_,_)=Bundled();
        var attributes=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json")).Attributes;
        var build=new CharacterBuild(attributes,skills,abilities);
        Assert.DoesNotContain(abilities.Abilities,
            ability=>ability.MeetsRequirements(build.Attributes,build.SelectedSkills));
        Assert.True(build.ToggleSkill("sword-novice"));
        Assert.Equal(new[] { "sword-quick-cut","sword-opening-break" },
            abilities.OrderedFor(build.Attributes,build.SelectedSkills).Take(2).Select(a=>a.Id));
        Assert.True(build.SetAttribute(AttributeKind.Strength,5));
        Assert.True(build.ToggleSkill("sword-adept"));
        Assert.All(abilities.Abilities.Where(a=>a.Path=="Iron Sword"),
            ability=>Assert.True(ability.MeetsRequirements(build.Attributes,build.SelectedSkills)));
        Assert.All(abilities.Abilities.Where(a=>a.Path=="Archer"),
            ability=>Assert.False(ability.MeetsRequirements(build.Attributes,build.SelectedSkills)));
        Assert.True(build.ToggleSkill("sword-novice"));
        Assert.Empty(build.SelectedSkills);
        Assert.DoesNotContain(abilities.Abilities,
            ability=>ability.MeetsRequirements(build.Attributes,build.SelectedSkills));
    }

    [Fact]
    public void SwordTomeNeedsAttributesThenTeachesOnceAndRequiresSwordToUse()
    {
        var (_,abilities,items,containers)=Bundled();
        var attributes=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json")).Attributes;
        var chest=new Inventory(containers.Get("starter-chest").Contents);
        var player=new PlayerEntity(Vector3.Zero,attributes.With(AttributeKind.Agility,3),items,
            ["sword-novice"]);
        Assert.True(chest.TransferTo(player.Inventory,"tome-sword-quick-cut",1));
        Assert.Single(player.Inventory.Filter(items,ItemFilter.Tomes));
        Assert.False(player.Abilities.TryLearnFromTome("tome-sword-quick-cut",player.Inventory,
            items,abilities,player.Attributes,player.SkillIds));
        Assert.Equal(1,player.Inventory.Count("tome-sword-quick-cut"));

        var untrained=new PlayerEntity(Vector3.Zero,attributes,items);
        Assert.True(player.Inventory.TransferTo(untrained.Inventory,"tome-sword-quick-cut",1));
        Assert.False(untrained.Abilities.TryLearnFromTome("tome-sword-quick-cut",
            untrained.Inventory,items,abilities,untrained.Attributes,untrained.SkillIds));

        var eligible=new PlayerEntity(Vector3.Zero,attributes,items,["sword-novice"]);
        Assert.True(untrained.Inventory.TransferTo(eligible.Inventory,"tome-sword-quick-cut",1));
        Assert.True(eligible.Abilities.TryLearnFromTome("tome-sword-quick-cut",
            eligible.Inventory,items,abilities,eligible.Attributes,eligible.SkillIds));
        Assert.True(eligible.Abilities.Knows("sword-quick-cut"));
        Assert.Equal(0,eligible.Inventory.Count("tome-sword-quick-cut"));
        Assert.False(eligible.Abilities.CanUse(abilities.Get("sword-quick-cut"),eligible));

        eligible.Inventory.Add("iron-sword",1);
        Assert.True(eligible.Equipment.Equip("iron-sword"));
        Assert.True(eligible.Abilities.CanUse(abilities.Get("sword-quick-cut"),eligible));
        Assert.True(eligible.Equipment.Unequip(EquipmentSlot.Weapon));
        Assert.True(eligible.Abilities.Knows("sword-quick-cut"));
        Assert.False(eligible.Abilities.CanUse(abilities.Get("sword-quick-cut"),eligible));
        eligible.Inventory.Add("tome-sword-quick-cut",1);
        Assert.False(eligible.Abilities.TryLearnFromTome("tome-sword-quick-cut",
            eligible.Inventory,items,abilities,eligible.Attributes,eligible.SkillIds));
        Assert.Equal(1,eligible.Inventory.Count("tome-sword-quick-cut"));
    }

    [Fact]
    public void BundledAbilitiesAndTomesHaveImplementedCombatEffects()
    {
        var (_,abilities,items,containers)=Bundled();
        Assert.Equal(6,abilities.Abilities.Count);
        Assert.All(abilities.Abilities,ability=>
        {
            Assert.NotNull(ability.Combat);
            Assert.Single(ability.RequiredSkills);
            Assert.Contains(ability.RequiredSkills[0],
                new[] { "sword-novice","sword-adept","bow-novice" });
        });
        var chest=new Inventory(containers.Get("starter-chest").Contents);
        var tomes=items.Items.Where(item=>item.ItemType==ItemType.Tome).ToArray();
        Assert.Equal(6,tomes.Length);
        Assert.All(tomes,tome=>
        {
            Assert.NotNull(tome.AbilityId);
            Assert.NotNull(abilities.Get(tome.AbilityId!).Combat);
            Assert.Equal(1,chest.Count(tome.Id));
        });
    }

    [Fact]
    public void RevisitedBuildUpdatesTheLivePlayerWithoutResettingDungeonProgress()
    {
        var (_,abilities,items,_)=Bundled();
        var initial=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json"));
        initial.SkillIds=["sword-novice"];
        initial.AbilityIds=["sword-opening-break"];
        var simulation=new DungeonSimulation(new DungeonSettings(),items:items,
            playerDefinition:initial,abilities:abilities);
        var player=simulation.Player;
        player.Position=new Vector3(3,0,-7);
        player.Inventory.Add("iron-sword",1);
        Assert.True(player.Equipment.Equip("iron-sword"));
        player.Inventory.Add("tome-sword-quick-cut",1);
        Assert.True(player.Abilities.TryLearnFromTome("tome-sword-quick-cut",
            player.Inventory,items,abilities,player.Attributes,player.SkillIds));
        Assert.True(player.Abilities.Loadout.Assign(0,"sword-opening-break",player.Abilities));
        int oldMaxHealth=player.MaxHealth;
        player.ReceiveDamage(8);
        int missingHealth=player.MaxHealth-player.Health;
        simulation.Enemy.ReceiveDamage(2);
        int enemyHealth=simulation.Enemy.Health;
        simulation.TogglePause();

        var revised=new PlayerDefinition
        {
            Attributes=initial.Attributes.With(AttributeKind.Strength,5)
                .With(AttributeKind.Endurance,5),
            SkillIds=["sword-novice","sword-adept"],
            AbilityIds=["sword-driving-cut"]
        };
        simulation.ApplyPlayerBuild(initial,revised);
        Assert.Same(player,simulation.Player);
        Assert.Equal(new Vector3(3,0,-7),player.Position);
        Assert.Equal("iron-sword",player.Equipment.In(EquipmentSlot.Weapon));
        Assert.Equal(enemyHealth,simulation.Enemy.Health);
        Assert.True(simulation.Paused);
        Assert.True(player.MaxHealth>oldMaxHealth);
        Assert.Equal(missingHealth,player.MaxHealth-player.Health);
        Assert.Equal(5,player.Attributes.Strength);
        Assert.Contains("sword-adept",player.SkillIds);
        Assert.True(player.Abilities.Knows("sword-driving-cut"));
        Assert.True(player.Abilities.Knows("sword-quick-cut")); // learned from a tome
        Assert.False(player.Abilities.Knows("sword-opening-break"));
        Assert.Null(player.Abilities.Loadout.In(0));
    }

    [Fact]
    public void LoadoutAcceptsOnlyLearnedAbilitiesAndResetsWithPlayer()
    {
        var (_,_,items,_)=Bundled();
        var definition=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json"));
        definition.AbilityIds=["sword-quick-cut"];
        var simulation=new DungeonSimulation(new DungeonSettings(),items:items,
            playerDefinition:definition);
        var player=simulation.Player;
        Assert.False(player.Abilities.Loadout.Assign(0,"sword-opening-break",player.Abilities));
        Assert.True(player.Abilities.Loadout.Assign(0,"sword-quick-cut",player.Abilities));
        Assert.True(player.Abilities.Loadout.Assign(11,"sword-quick-cut",player.Abilities));
        Assert.Equal("sword-quick-cut",player.Abilities.Loadout.In(0));
        Assert.Equal("sword-quick-cut",player.Abilities.Loadout.In(11));
        player.Abilities.Loadout.Clear(0);
        Assert.Null(player.Abilities.Loadout.In(0));
        Assert.Throws<ArgumentOutOfRangeException>(()=>player.Abilities.Loadout.In(12));
        simulation.Reset();
        Assert.Null(simulation.Player.Abilities.Loadout.In(11));
    }

    [Fact]
    public void InvalidAbilityAndTomeReferencesFailValidation()
    {
        var (skills,abilities,items,_)=Bundled();
        var ability=abilities.Get("sword-quick-cut");
        ability.Combat=null;
        Assert.Throws<InvalidDataException>(()=>abilities.Validate(skills));
        ability.Combat=new AbilityCombat { StaminaCost=10 };
        ability.RequiredSkills.Add("unknown-skill");
        Assert.Throws<KeyNotFoundException>(()=>abilities.Validate(skills));
        ability.RequiredSkills.Remove("unknown-skill");

        var tome=items.Get("tome-sword-quick-cut");
        tome.AbilityId="unknown-ability";
        Assert.Throws<KeyNotFoundException>(()=>items.Validate(abilities));
        tome.AbilityId="sword-quick-cut";
        tome.Slot="Weapon";
        Assert.Throws<InvalidDataException>(()=>items.Validate(abilities));
    }
}

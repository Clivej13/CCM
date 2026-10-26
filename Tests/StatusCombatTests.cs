using System.Numerics;
using CCM.Application;
using CCM.Game;
using Xunit;

namespace CCM.Tests;

public sealed class StatusCombatTests
{
    private static (StatusCatalogue Statuses,AbilityCatalogue Abilities,ItemCatalogue Items)
        Bundled()
    {
        var statuses=Settings.Required<StatusCatalogue>(Settings.ConfigPath("statuses.json"));
        statuses.Validate();
        var skills=Settings.Required<SkillCatalogue>(Settings.ConfigPath("skills.json"));
        skills.Validate();
        var abilities=Settings.Required<AbilityCatalogue>(Settings.ConfigPath("abilities.json"));
        abilities.Validate(skills,statuses);
        var items=Settings.Required<ItemCatalogue>(Settings.ConfigPath("items.json"));
        items.Validate(abilities);
        return (statuses,abilities,items);
    }

    [Fact]
    public void TimedStatusesRefreshExpireAndBlockActions()
    {
        var (statuses,_,_)=Bundled();
        var active=new StatusCollection();
        active.Apply(statuses.Get("shaken"));
        active.Tick(3);
        Assert.True(active.Has("shaken"));
        active.Apply(statuses.Get("shaken"));
        Assert.Equal(4,Assert.Single(active.Active).RemainingSeconds);
        active.Apply(statuses.Get("stunned"));
        Assert.True(active.BlocksActions);
        active.Tick(0.8f);
        Assert.False(active.Has("stunned"));
        Assert.True(active.Has("shaken"));
        Assert.False(active.BlocksActions);
        Assert.True(active.Consume("shaken"));
        Assert.False(active.Consume("shaken"));
    }

    [Fact]
    public void InvalidStatusAndAbilityReferencesFailAtStartup()
    {
        var (statuses,abilities,items)=Bundled();
        statuses.Statuses.Add(new StatusDefinition
        {
            Id="shaken",Name="Duplicate",Description="Duplicate",DurationSeconds=1
        });
        Assert.Throws<InvalidDataException>(statuses.Validate);
        statuses.Statuses.RemoveAt(statuses.Statuses.Count-1);
        abilities.Get("sword-quick-cut").Combat!.ApplyStatusId="missing";
        var skills=Settings.Required<SkillCatalogue>(Settings.ConfigPath("skills.json"));
        Assert.Throws<KeyNotFoundException>(()=>abilities.Validate(skills,statuses));
        var enemies=Settings.Required<EnemyCatalogue>(Settings.ConfigPath("enemies.json"));
        enemies.Enemies[0].OnHitStatusId="missing";
        Assert.Throws<KeyNotFoundException>(()=>enemies.Validate(items,statuses));
    }

    [Fact]
    public void SwordStatusesMakeLongAndShortRoutesWithDifferentStaminaCosts()
    {
        var (statuses,abilities,items)=Bundled();
        var longRoute=ReadySwordFight(statuses,abilities,items);
        int startLong=longRoute.Player.MaxStamina;
        Cast(longRoute,"sword-opening-break");
        Assert.True(longRoute.Enemy.Statuses.Has("shaken"));
        Cast(longRoute,"sword-quick-cut");
        Assert.False(longRoute.Enemy.Statuses.Has("shaken"));
        Assert.True(longRoute.Enemy.Statuses.Has("exposed"));
        Cast(longRoute,"sword-driving-cut");
        Assert.True(longRoute.Enemy.Statuses.Has("staggered"));
        int beforeFinish=longRoute.Enemy.Health;
        Cast(longRoute,"sword-finishing-cut");
        Assert.True(longRoute.Enemy.Health<beforeFinish);
        Assert.False(longRoute.Enemy.Statuses.Has("staggered"));
        int longSpent=startLong-(int)longRoute.Player.Stamina;

        var shortRoute=ReadySwordFight(statuses,abilities,items);
        int startShort=shortRoute.Player.MaxStamina;
        Cast(shortRoute,"sword-quick-cut");
        Assert.True(shortRoute.Enemy.Statuses.Has("pressured"));
        int beforeBreak=shortRoute.Enemy.Health;
        Cast(shortRoute,"sword-opening-break");
        Assert.False(shortRoute.Enemy.Statuses.Has("pressured"));
        Assert.True(shortRoute.Enemy.Health<beforeBreak);
        int shortSpent=startShort-(int)shortRoute.Player.Stamina;
        Assert.True(longSpent>shortSpent);
        Assert.True(longRoute.Enemy.Health<shortRoute.Enemy.Health);
    }

    [Fact]
    public void StunStopsMovementAndAttacksWhileAutoAttacksCostNoStamina()
    {
        var (statuses,abilities,items)=Bundled();
        var fight=ReadySwordFight(statuses,abilities,items);
        int full=fight.Player.MaxStamina;
        fight.RequestAttack();
        for (int i=0;i<8;i++) fight.UpdateCombat(0.1f);
        Assert.Equal(full,fight.Player.Stamina);
        fight.Player.Statuses.Apply(statuses.Get("stunned"));
        Vector3 before=fight.Player.Position;
        fight.Update(Vector2.UnitY,Vector2.Zero,true,0.1f);
        Assert.Equal(before,fight.Player.Position);
        int enemyHealth=fight.Enemy.Health;
        fight.UpdateCombat(0.1f);
        Assert.Equal(enemyHealth,fight.Enemy.Health);
        Assert.Equal(AttackPhase.Ready,fight.Player.Phase);
    }

    [Fact]
    public void LowStaminaRejectsSwordAbility()
    {
        var (statuses,abilities,items)=Bundled();
        var fight=ReadySwordFight(statuses,abilities,items);
        Assert.True(fight.Player.TrySpendStamina(fight.Player.MaxStamina));
        Assert.Equal("Not enough stamina",fight.RequestAbility("sword-quick-cut"));
        Assert.Equal(0,fight.Player.Stamina);
        fight.UpdateCombat(0.1f);
        Assert.True(fight.Player.Stamina>0);
    }

    [Fact]
    public void EnemyHitAppliesVisiblePlayerStatusAndPauseFreezesItsTimer()
    {
        var (statuses,abilities,items)=Bundled();
        var fight=ReadySwordFight(statuses,abilities,items);
        for (int i=0;i<60 && !fight.Player.Statuses.Has("shaken");i++)
            fight.UpdateCombat(0.1f);
        Assert.True(fight.Player.Statuses.Has("shaken"));
        float remaining=fight.Player.Statuses.Active.Single(s=>s.Definition.Id=="shaken")
            .RemainingSeconds;
        fight.TogglePause();
        fight.UpdateCombat(1);
        Assert.Equal(remaining,fight.Player.Statuses.Active.Single(s=>
            s.Definition.Id=="shaken").RemainingSeconds);
    }

    [Fact]
    public void SwordRequirementRangeAndStaminaGateAbilityWithoutSpendingOnFailure()
    {
        var (statuses,abilities,items)=Bundled();
        var fight=ReadySwordFight(statuses,abilities,items);
        fight.Player.Equipment.Unequip(EquipmentSlot.Weapon);
        int before=(int)fight.Player.Stamina;
        Assert.Contains("unavailable",fight.RequestAbility("sword-quick-cut"));
        Assert.Equal(before,(int)fight.Player.Stamina);
        fight.Player.Inventory.Add("iron-sword",1);
        Assert.True(fight.Player.Equipment.Equip("iron-sword"));
        fight.Player.Position=Vector3.Zero;
        Assert.Contains("range",fight.RequestAbility("sword-quick-cut"));
        Assert.Equal(before,(int)fight.Player.Stamina);
        fight.Player.Position=new(0,0,-28.5f);
        Assert.Contains("damage",fight.RequestAbility("sword-quick-cut"));
        Assert.True(fight.Player.Stamina<before);
    }

    [Fact]
    public void InstantSwordAbilitiesKeepIndependentCooldownsThatPauseWithCombat()
    {
        var (statuses,abilities,items)=Bundled();
        var fight=ReadySwordFight(statuses,abilities,items);
        Assert.Equal(0,fight.AbilityCooldownFraction("sword-quick-cut"));

        Assert.Contains("damage",fight.RequestAbility("sword-quick-cut"));
        Assert.Null(fight.CastingAbilityId);
        Assert.Equal(0,fight.AbilityCastFraction);
        Assert.Equal(1,fight.AbilityCooldownFraction("sword-quick-cut"));
        Assert.Equal(0,fight.AbilityCooldownFraction("sword-opening-break"));
        Assert.Contains("cooling down",fight.RequestAbility("sword-quick-cut"));

        Assert.Contains("damage",fight.RequestAbility("sword-opening-break"));
        Assert.Equal(1,fight.AbilityCooldownFraction("sword-opening-break"));
        fight.TogglePause();
        fight.UpdateCombat(1);
        Assert.Equal(1,fight.AbilityCooldownFraction("sword-quick-cut"));
        fight.TogglePause();
        fight.Enemy.ReceiveDamage(fight.Enemy.Health);
        for (int i=0;i<50;i++) fight.UpdateCombat(0.1f);
        Assert.Equal(0,fight.AbilityCooldownFraction("sword-quick-cut"));
        Assert.Equal(0,fight.AbilityCooldownFraction("sword-opening-break"));
    }

    private static DungeonSimulation ReadySwordFight(StatusCatalogue statuses,
        AbilityCatalogue abilities,ItemCatalogue items)
    {
        var definition=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json"));
        definition.SkillIds=["sword-novice","sword-adept"];
        definition.Attributes=definition.Attributes.With(AttributeKind.Strength,5);
        definition.AbilityIds=["sword-opening-break","sword-quick-cut",
            "sword-driving-cut","sword-finishing-cut"];
        var enemies=Settings.Required<EnemyCatalogue>(Settings.ConfigPath("enemies.json"));
        enemies.Enemies[0].Attributes=new CharacterAttributes
        {
            Strength=4,Endurance=10,Agility=4,Intelligence=4,Willpower=4,
            Speed=4,Charisma=4,Luck=4
        };
        var fight=new DungeonSimulation(
            new DungeonSettings { Combat=new() { AutoPauseOnCombatStart=false } },
            items:items,playerDefinition:definition,enemies:enemies,
            abilities:abilities,statuses:statuses,combatRoll:()=>0.5f);
        fight.Player.Inventory.Add("iron-sword",1);
        Assert.True(fight.Player.Equipment.Equip("iron-sword"));
        fight.Player.Position=new(0,0,-6.35f);
        fight.UpdateContextAim();
        Assert.True(fight.ActivateContext());
        fight.UpdateCombat(0.1f);
        fight.UpdateCombat(0.1f);
        Assert.True(fight.CombatStarted);
        fight.Player.Position=new(0,0,-28.5f);
        fight.Enemy.Position=new(0,0,-30);
        Assert.True(fight.SelectEnemyContextual());
        return fight;
    }

    private static void Cast(DungeonSimulation fight,string abilityId)
    {
        Assert.Contains("damage",fight.RequestAbility(abilityId));
        Assert.Null(fight.CastingAbilityId);
        for (int i=0;i<9;i++) fight.UpdateCombat(0.1f);
    }
}

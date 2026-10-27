using System.Numerics;
using CCM.Game;
using CCM.Application;
using Xunit;

namespace CCM.Tests;

public sealed class CombatTests
{
    [Fact]
    public void MeleeAttacksLandImmediatelyThenCooldown()
    {
        var player = new PlayerEntity(Vector3.Zero,new CharacterAttributes());
        var enemy = new EnemyEntity(new(0,0,-1),new EnemyDefinition());
        Assert.True(player.InRange(enemy));
        Assert.Equal(0,player.Profile.CastSeconds);
        Assert.True(player.UpdateAttack(0.01f,enemy,true));
        int dealt=CombatFormula.DamageAfterDefence(player.Profile.Damage,enemy.Profile);
        Assert.Equal(enemy.MaxHealth-dealt,enemy.Health);
        Assert.Equal(AttackPhase.Cooldown,player.Phase);
        Assert.False(player.UpdateAttack(player.Profile.CooldownSeconds,enemy,true));
        Assert.Equal(AttackPhase.Ready,player.Phase);
        Assert.True(enemy.UpdateAttack(0.01f,player,true));
        int received=CombatFormula.DamageAfterDefence(enemy.Profile.Damage,player.Profile);
        Assert.Equal(player.MaxHealth-received,player.Health);
    }

    [Fact]
    public void BowAttackWhiffsWhenTargetLeavesReachBeforeRelease()
    {
        var items=Settings.Required<ItemCatalogue>(Settings.ConfigPath("items.json"));
        var player=new PlayerEntity(Vector3.Zero,new CharacterAttributes(),items);
        player.Inventory.Add("short-bow",1);
        Assert.True(player.Equipment.Equip("short-bow"));
        var enemy=new EnemyEntity(new(0,0,-10),new EnemyDefinition());
        float halfCast=player.Profile.CastSeconds/2;
        Assert.True(halfCast>0);
        Assert.False(player.UpdateAttack(halfCast,enemy,true));
        Assert.Equal(AttackPhase.Casting,player.Phase);
        enemy.Position=new(0,0,-30);
        Assert.False(player.InRange(enemy));
        Assert.False(player.UpdateAttack(halfCast,enemy,true));
        Assert.Equal(AttackOutcome.Miss,player.LastAttackOutcome);
        Assert.Equal(enemy.MaxHealth,enemy.Health);
        Assert.Equal(AttackPhase.Cooldown,player.Phase);
    }

    [Fact]
    public void EnemyVisionStartsAndOptionallyPausesCombatAfterDoorOpens()
    {
        var paused = AtDoor(new());
        paused.UpdateInteraction(true);
        Assert.True(paused.World.Interactables[0].IsOpen);
        Assert.False(paused.CombatStarted);
        Assert.False(paused.Paused);
        paused.UpdateCombat(0.1f);
        Assert.False(paused.CombatStarted);
        paused.UpdateCombat(0.1f);
        Assert.True(paused.CombatStarted);
        Assert.True(paused.Paused);
        var enemyStart = paused.Enemy.Position;
        paused.Update(Vector2.UnitY,Vector2.Zero,true,0.1f);
        paused.UpdateCombat(0.1f);
        Assert.Equal(enemyStart,paused.Enemy.Position);
        paused.TogglePause();
        paused.UpdateCombat(0.1f);
        Assert.NotEqual(enemyStart,paused.Enemy.Position);

        var immediate = AtDoor(new DungeonSettings { Combat = new() { AutoPauseOnCombatStart=false } });
        immediate.UpdateInteraction(true);
        immediate.UpdateCombat(0.1f);
        immediate.UpdateCombat(0.1f);
        Assert.True(immediate.CombatStarted);
        Assert.False(immediate.Paused);
    }

    [Fact]
    public void EnemyNeedsOpenSightAndVisionRangeBeforeCombat()
    {
        var simulation = AtDoor(new DungeonSettings
            { Combat = new() { AutoPauseOnCombatStart = false } },
            new EnemyCatalogue { Enemies=[new EnemyDefinition { VisionRange=5 }] });
        simulation.UpdateCombat(0.1f);
        Assert.False(simulation.CombatStarted);
        simulation.UpdateInteraction(true);
        simulation.UpdateCombat(0.1f); // door-opening frame
        simulation.UpdateCombat(0.1f);
        Assert.False(simulation.CombatStarted);
        simulation.Player.Position = new(0,0,-27);
        simulation.UpdateCombat(0.1f);
        Assert.True(simulation.CombatStarted);
    }

    [Fact]
    public void SelectionAndAttackAreRequiredForPlayerAutoAttack()
    {
        var simulation = AtDoor(new DungeonSettings { Combat = new() { AutoPauseOnCombatStart=false } });
        simulation.UpdateInteraction(true);
        simulation.UpdateCombat(0.1f);
        simulation.UpdateCombat(0.1f);
        simulation.Player.Position = new(0,0,-27);
        simulation.Enemy.Position = new(0,0,-30);
        Assert.True(simulation.SelectEnemy());
        for (int i=0;i<20;i++) simulation.UpdateCombat(0.1f);
        Assert.Equal(simulation.Enemy.MaxHealth,simulation.Enemy.Health);
        simulation.RequestAttack();
        Assert.True(simulation.AutoAttackEnabled);
        for (int i=0;i<30;i++) simulation.UpdateCombat(0.1f);
        Assert.True(simulation.Enemy.Health < simulation.Enemy.MaxHealth);
        Assert.True(simulation.Player.Health < simulation.Player.MaxHealth);
    }

    [Fact]
    public void GameplayOptionStartsAttackingWhenEnemyIsSelected()
    {
        var option = new GameplaySettings { AutoAttackSelectedEnemy = true };
        var simulation = new DungeonSimulation(
            new DungeonSettings { Combat = new() { AutoPauseOnCombatStart = false } }, option);
        simulation.Player.Position = new(0,0,-6.35f);
        simulation.UpdateContextAim();
        Assert.True(simulation.ActivateContext());
        Assert.False(simulation.CombatStarted);
        simulation.UpdateCombat(0.1f,controllerScheme:true);
        simulation.UpdateCombat(0.1f,controllerScheme:true);
        Assert.True(simulation.CombatStarted);
        Assert.True(simulation.ContextEnemySelected);
        Assert.True(simulation.AutoAttackEnabled);
        Assert.Contains(simulation.AvailableContextActions(),
            option => option.Label == "Attack enemy" && option.Selected);
        simulation.Player.Position = new(0,0,-28);
        simulation.Enemy.Position = new(0,0,-29.5f);
        for (int i=0;i<20;i++) simulation.UpdateCombat(0.1f);
        Assert.True(simulation.Enemy.Health < simulation.Enemy.MaxHealth);
        simulation.Player.Position = new(0,0,-6.35f);
        option.AutoAttackSelectedEnemy = false;
        simulation.CycleContext(-1);
        Assert.False(simulation.AutoAttackEnabled);
        Assert.False(simulation.EnemySelected);
    }

    [Fact]
    public void ManualOptionUsesAForSelectedEnemy()
    {
        var simulation = new DungeonSimulation(
            new DungeonSettings { Combat = new() { AutoPauseOnCombatStart = false } });
        simulation.Player.Position = new(0,0,-6.35f);
        simulation.UpdateContextAim();
        Assert.True(simulation.ActivateContext());
        simulation.UpdateCombat(0.1f,controllerScheme:true);
        simulation.UpdateCombat(0.1f,controllerScheme:true);
        Assert.True(simulation.ContextEnemySelected);
        Assert.False(simulation.AutoAttackEnabled);
        Assert.True(simulation.ActivateContext());
        Assert.True(simulation.AutoAttackEnabled);
    }

    [Fact]
    public void ControllerEnemyIsSelectedEvenWhenCombatAutoPauses()
    {
        var simulation = new DungeonSimulation(new(),
            new GameplaySettings { AutoAttackSelectedEnemy = true });
        simulation.Player.Position = new(0,0,-6.35f);
        simulation.UpdateContextAim();
        Assert.True(simulation.ActivateContext());
        Assert.False(simulation.Paused);
        simulation.UpdateCombat(0.1f,controllerScheme:true);
        simulation.UpdateCombat(0.1f,controllerScheme:true);
        Assert.True(simulation.Paused);
        Assert.True(simulation.ContextEnemySelected);
        Assert.True(simulation.AutoAttackEnabled);
        var actions = simulation.AvailableContextActions();
        Assert.Contains(actions, item => item.Label == "Attack enemy" && item.Selected);
        int health = simulation.Enemy.Health;
        simulation.UpdateCombat(1);
        Assert.Equal(health,simulation.Enemy.Health);
        simulation.TogglePause();
        Assert.True(simulation.ContextEnemySelected);
    }

    [Fact]
    public void EnemyTraversesOpenDoorAndAutoAttacksThePlayer()
    {
        var simulation = AtDoor(new DungeonSettings { Combat = new() { AutoPauseOnCombatStart=false } });
        simulation.UpdateInteraction(true);
        for (int i=0;i<140;i++) simulation.UpdateCombat(0.1f);
        Assert.True(simulation.Enemy.Position.Z > -9);
        Assert.True(simulation.Player.Health < simulation.Player.MaxHealth);
    }

    [Fact]
    public void ClosedDoorBlocksMeleeLineOfSight()
    {
        var world = new DungeonWorld();
        Assert.False(world.ClearLine(new(0,1,-6),new(0,1,-10)));
        Assert.True(world.Interactables[0].Activate(world.Spawn));
        Assert.True(world.ClearLine(new(0,1,-6),new(0,1,-10)));
    }

    [Fact]
    public void InvalidCombatTuningIsRejected()
    {
        Assert.Throws<InvalidDataException>(()=>new DungeonSettings { EnemyId="" }.Validate());
        Assert.Throws<InvalidDataException>(()=>new CharacterAttributes { Endurance=0 }.Validate());
        Assert.Throws<InvalidDataException>(()=>
            new EnemyDefinition { VisionRange=float.NaN }.Validate(new ItemCatalogue()));
    }

    [Fact]
    public void AttributesAndEquippedWeaponsChangeLiveCombatValues()
    {
        var items=Settings.Required<ItemCatalogue>(Settings.ConfigPath("items.json"));
        items.Validate();
        var attributes=new CharacterAttributes();
        var player=new PlayerEntity(Vector3.Zero,attributes,items);
        var enemy=new EnemyEntity(new(0,0,-2),new EnemyDefinition(),items);
        var unarmed=player.Profile;
        Assert.False(player.InRange(enemy));
        Assert.Equal(110,unarmed.MaxHealth);
        Assert.Equal(68,enemy.MaxHealth);
        Assert.Equal(12,unarmed.Damage);
        Assert.Equal(CombatFormula.UnarmedRange,unarmed.Range);
        Assert.True(unarmed.AttacksPerSecond>0);
        Assert.True(unarmed.Armour>0);
        Assert.True(unarmed.ResistancePercent>0);

        player.Inventory.Add("iron-sword",1);
        player.Inventory.Add("leather-armour",1);
        player.Inventory.Add("leather-quiver",1);
        Assert.True(player.Equipment.Equip("iron-sword"));
        Assert.True(player.Equipment.Equip("leather-armour"));
        Assert.True(player.Equipment.Equip("leather-quiver"));
        var armed=player.Profile;
        Assert.True(player.InRange(enemy));
        Assert.Equal(2f,armed.Range);
        Assert.Equal(unarmed.Damage+items.Get("iron-sword").AttackPower,armed.Damage);
        Assert.True(armed.AttacksPerSecond>unarmed.AttacksPerSecond);
        Assert.Equal(unarmed.Armour+5,armed.Armour);
        Assert.True(player.UpdateAttack(0.01f,enemy,true));
        Assert.Equal(enemy.MaxHealth-CombatFormula.DamageAfterDefence(armed.Damage,enemy.Profile),
            enemy.Health);

        Assert.True(player.Equipment.Unequip(EquipmentSlot.Weapon));
        Assert.Equal(unarmed.Range,player.Profile.Range);
        Assert.Equal(unarmed.Damage,player.Profile.Damage);
        player.Inventory.Add("arrow",20);
        Assert.True(player.Equipment.Equip("arrow"));
        Assert.Equal(20,player.Equipment.AmmoCount);
        Assert.Equal(0,player.Inventory.Count("arrow"));
    }

    [Fact]
    public void BundledEnemyCatalogueBuildsOnlyTheRequestedEnemyType()
    {
        var items=Settings.Required<ItemCatalogue>(Settings.ConfigPath("items.json"));
        items.Validate();
        var player=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json"));
        player.Validate();
        var enemies=Settings.Required<EnemyCatalogue>(Settings.ConfigPath("enemies.json"));
        enemies.Validate(items);
        var dungeon=new DungeonSettings();
        var simulation=new DungeonSimulation(dungeon,items:items,
            playerDefinition:player,enemies:enemies);
        Assert.Equal(dungeon.EnemyId,simulation.Enemy.Definition.Id);
        Assert.Equal(74,simulation.Player.MaxHealth);
        Assert.Equal(68,simulation.Enemy.MaxHealth);
        Assert.Equal(player.Attributes.Strength,simulation.Player.Attributes.Strength);
        Assert.Equal(enemies.Get(dungeon.EnemyId).Attributes.Endurance,
            simulation.Enemy.Attributes.Endurance);
        Assert.Equal(enemies.Get(dungeon.EnemyId).MoveSpeed,
            simulation.Enemy.Definition.MoveSpeed);
        var first=simulation.Enemy;
        simulation.Reset();
        Assert.NotSame(first,simulation.Enemy);

        var armedDefinition=new EnemyDefinition
            { Id="armed-guard", WeaponId="iron-sword", ArmourId="leather-armour" };
        armedDefinition.Validate(items);
        var armedEnemy=new EnemyEntity(Vector3.Zero,armedDefinition,items);
        Assert.Equal(first.Profile.Damage+items.Get("iron-sword").AttackPower,
            armedEnemy.Profile.Damage);
        Assert.Equal(first.Profile.Armour+items.Get("leather-armour").Protection,
            armedEnemy.Profile.Armour);

        enemies.Enemies.Add(new EnemyDefinition { Id=dungeon.EnemyId });
        Assert.Throws<InvalidDataException>(()=>enemies.Validate(items));
        enemies.Enemies.RemoveAt(1);
        enemies.Enemies[0].WeaponId="arrow";
        Assert.Throws<InvalidDataException>(()=>enemies.Validate(items));
    }

    [Fact]
    public void BowStartsLoadedAndRangedAbilitiesSpendArrowsAndDelayOnHit()
    {
        var statuses=Settings.Required<StatusCatalogue>(Settings.ConfigPath("statuses.json"));
        var skills=Settings.Required<SkillCatalogue>(Settings.ConfigPath("skills.json"));
        var abilities=Settings.Required<AbilityCatalogue>(Settings.ConfigPath("abilities.json"));
        var items=Settings.Required<ItemCatalogue>(Settings.ConfigPath("items.json"));
        var player=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json"));
        var settings=Settings.Required<DungeonSettings>(Settings.ConfigPath("dungeon.json"));
        settings.Combat.AutoPauseOnCombatStart=false;
        var enemies=new EnemyCatalogue { Enemies=[new EnemyDefinition { MoveSpeed=0 }] };
        var fight=new DungeonSimulation(settings,items:items,playerDefinition:player,
            enemies:enemies,abilities:abilities,statuses:statuses,combatRoll:()=>0.5f);
        Assert.Equal("short-bow",fight.Player.Equipment.In(EquipmentSlot.Weapon));
        Assert.Equal("leather-quiver",fight.Player.Equipment.In(EquipmentSlot.Quiver));
        Assert.Equal("arrow",fight.Player.Equipment.In(EquipmentSlot.Ammo));
        Assert.Equal(40,fight.Player.Equipment.AmmoCount);
        Assert.Equal("bow-quick-shot",fight.Player.Abilities.Loadout.In(0));
        Assert.Equal("bow-aimed-shot",fight.Player.Abilities.Loadout.In(1));
        Assert.True(fight.Player.Profile.Range>10);

        fight.Player.Position=new(0,0,-14);
        fight.UpdateCombat(0.1f);
        Assert.True(fight.CombatStarted);
        Assert.True(fight.SelectEnemyContextual());
        Assert.Contains("casting",fight.RequestAbility("bow-quick-shot"));
        fight.UpdateCombat(0.1f);
        float progress=fight.AbilityCastFraction;
        Assert.True(progress>0);
        Assert.Equal(0,fight.AbilityCooldownFraction("bow-quick-shot"));
        fight.Player.ReceiveDamage(2);
        Assert.True(fight.AbilityCastFraction<progress);
        for (int i=0;i<6;i++) fight.UpdateCombat(0.1f);
        Assert.Equal("bow-quick-shot",fight.CastingAbilityId);
        for (int i=0;i<4;i++) fight.UpdateCombat(0.1f);
        Assert.Null(fight.CastingAbilityId);
        Assert.Equal(0,fight.AbilityCastFraction);
        Assert.True(fight.AbilityCooldownFraction("bow-quick-shot")>0);
        Assert.Equal(39,fight.Player.Equipment.AmmoCount);
        Assert.True(fight.Enemy.Statuses.Has("marked"));

        Assert.Contains("casting",fight.RequestAbility("bow-aimed-shot"));
        for (int i=0;i<16;i++) fight.UpdateCombat(0.1f);
        Assert.Null(fight.CastingAbilityId);
        Assert.Equal(38,fight.Player.Equipment.AmmoCount);
        Assert.False(fight.Enemy.Statuses.Has("marked"));
        Assert.True(fight.Enemy.Statuses.Has("pinned"));
        Assert.True(fight.Enemy.Health<fight.Enemy.MaxHealth);
    }

    [Fact]
    public void CastAbilitiesHitDespiteLowAttackRollsButRequireSightAtRelease()
    {
        var settings=Settings.Required<DungeonSettings>(Settings.ConfigPath("dungeon.json"));
        settings.Combat.AutoPauseOnCombatStart=false;
        var items=Settings.Required<ItemCatalogue>(Settings.ConfigPath("items.json"));
        var abilities=Settings.Required<AbilityCatalogue>(Settings.ConfigPath("abilities.json"));
        var statuses=Settings.Required<StatusCatalogue>(Settings.ConfigPath("statuses.json"));
        var player=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json"));
        var enemies=new EnemyCatalogue { Enemies=[new EnemyDefinition { MoveSpeed=0 }] };
        var fight=new DungeonSimulation(settings,items:items,playerDefinition:player,
            enemies:enemies,abilities:abilities,statuses:statuses,combatRoll:()=>0);
        fight.Player.Position=new(0,0,-6);
        fight.Enemy.Position=new(0,0,-10);
        var door=fight.World.Interactables[0];
        Assert.True(door.Activate(fight.Player.Position));
        Assert.True(fight.World.ClearLine(fight.Player.Position+Vector3.UnitY,
            fight.Enemy.Position+Vector3.UnitY));
        fight.UpdateCombat(0.1f);
        Assert.True(fight.SelectEnemyContextual());

        Assert.Contains("casting",fight.RequestAbility("bow-quick-shot"));
        for (int i=0;i<8;i++) fight.UpdateCombat(0.1f);
        Assert.Null(fight.CastingAbilityId);
        Assert.True(fight.Enemy.Health<fight.Enemy.MaxHealth);
        Assert.True(fight.Enemy.Statuses.Has("marked"));
        int health=fight.Enemy.Health;

        Assert.Contains("casting",fight.RequestAbility("bow-aimed-shot"));
        Assert.True(door.Activate(fight.Player.Position));
        Assert.False(fight.World.ClearLine(fight.Player.Position+Vector3.UnitY,
            fight.Enemy.Position+Vector3.UnitY));
        for (int i=0;i<13 && fight.CastingAbilityId is not null;i++)
            fight.UpdateCombat(0.1f);
        Assert.Null(fight.CastingAbilityId);
        Assert.Contains("missed",fight.CombatMessage);
        Assert.Equal(health,fight.Enemy.Health);
    }

    [Fact]
    public void OrdinaryBowAttackUsesRangeAndStopsWhenQuiverIsEmpty()
    {
        var settings=Settings.Required<DungeonSettings>(Settings.ConfigPath("dungeon.json"));
        settings.Combat.AutoPauseOnCombatStart=false;
        var items=Settings.Required<ItemCatalogue>(Settings.ConfigPath("items.json"));
        var player=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json"));
        var enemies=new EnemyCatalogue { Enemies=[new EnemyDefinition { MoveSpeed=0 }] };
        var fight=new DungeonSimulation(settings,items:items,playerDefinition:player,
            enemies:enemies,combatRoll:()=>0.5f);
        fight.Player.Position=new(0,0,-14);
        fight.UpdateCombat(0.1f);
        Assert.True(fight.SelectEnemyContextual());
        fight.RequestAttack();
        for (int i=0;i<12;i++) fight.UpdateCombat(0.1f);
        Assert.True(fight.Enemy.Health<fight.Enemy.MaxHealth);
        Assert.Equal(39,fight.Player.Equipment.AmmoCount);
        Assert.True(fight.Player.Equipment.Unequip(EquipmentSlot.Ammo));
        Assert.Equal(39,fight.Player.Inventory.Count("arrow"));
        fight.RequestAttack();
        fight.UpdateCombat(0.1f);
        Assert.False(fight.AutoAttackEnabled);
        Assert.Equal("No arrows equipped in quiver",fight.CombatMessage);
    }

    [Fact]
    public void MissAndDodgeDoNoDamageAndHitsDelayBowWindup()
    {
        var items=Settings.Required<ItemCatalogue>(Settings.ConfigPath("items.json"));
        var player=new PlayerEntity(Vector3.Zero,new CharacterAttributes(),items);
        var enemy=new EnemyEntity(new(0,0,-10),new EnemyDefinition());
        player.Inventory.Add("short-bow",1);
        Assert.True(player.Equipment.Equip("short-bow"));
        float cast=player.Profile.CastSeconds;
        Assert.False(player.UpdateAttack(cast/2,enemy,true));
        player.ReceiveDamage(2);
        Assert.Equal(AttackPhase.Casting,player.Phase);
        Assert.False(player.UpdateAttack(cast/2,enemy,true));
        Assert.Equal(AttackPhase.Casting,player.Phase);
        Assert.False(player.UpdateAttack(0.3f,enemy,true,accuracyRoll:0));
        Assert.Equal(AttackOutcome.Miss,player.LastAttackOutcome);
        Assert.Equal(enemy.MaxHealth,enemy.Health);

        player.UpdateAttack(player.Profile.CooldownSeconds,enemy,true);
        Assert.False(player.UpdateAttack(cast,enemy,true,dodgeRoll:0));
        Assert.Equal(AttackOutcome.Dodged,player.LastAttackOutcome);
        Assert.Equal(enemy.MaxHealth,enemy.Health);
        Assert.Equal(AttackOutcome.Hit,
            CombatFormula.RollAttack(player.Attributes,enemy.Attributes,true,0.5f,0.5f));
    }

    [Fact]
    public void MovingInterruptsBowCastsAndStopsAutoAttacksWithoutSpendingArrows()
    {
        var settings=Settings.Required<DungeonSettings>(Settings.ConfigPath("dungeon.json"));
        settings.Combat.AutoPauseOnCombatStart=false;
        var items=Settings.Required<ItemCatalogue>(Settings.ConfigPath("items.json"));
        var abilities=Settings.Required<AbilityCatalogue>(Settings.ConfigPath("abilities.json"));
        var statuses=Settings.Required<StatusCatalogue>(Settings.ConfigPath("statuses.json"));
        var player=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json"));
        var enemies=new EnemyCatalogue { Enemies=[new EnemyDefinition { MoveSpeed=0 }] };
        var fight=new DungeonSimulation(settings,items:items,playerDefinition:player,
            enemies:enemies,abilities:abilities,statuses:statuses,combatRoll:()=>0.5f);
        fight.Player.Position=new(0,0,-14);
        fight.UpdateCombat(0.1f);
        Assert.True(fight.SelectEnemyContextual());

        Assert.Contains("casting",fight.RequestAbility("bow-quick-shot"));
        fight.UpdateCombat(0.1f);
        fight.Update(Vector2.UnitY,Vector2.Zero,true,0.1f);
        Assert.Null(fight.CastingAbilityId);
        Assert.Equal("Ability interrupted by movement",fight.CombatMessage);
        Assert.Equal("Stop moving to use an ability",fight.RequestAbility("bow-quick-shot"));
        Assert.Equal(0,fight.AbilityCooldownFraction("bow-quick-shot"));
        for (int i=0;i<10;i++) fight.UpdateCombat(0.1f);
        Assert.Equal(40,fight.Player.Equipment.AmmoCount);
        Assert.Equal(fight.Enemy.MaxHealth,fight.Enemy.Health);

        fight.RequestAttack();
        fight.UpdateCombat(0.1f);
        Assert.Equal(AttackPhase.Casting,fight.Player.Phase);
        fight.Update(Vector2.UnitY,Vector2.Zero,true,0.1f);
        Assert.False(fight.AutoAttackEnabled);
        Assert.Equal(AttackPhase.Ready,fight.Player.Phase);
        for (int i=0;i<10;i++) fight.UpdateCombat(0.1f);
        Assert.Equal(40,fight.Player.Equipment.AmmoCount);
        Assert.Equal(fight.Enemy.MaxHealth,fight.Enemy.Health);
    }

    [Fact]
    public void MovingBeforeMeleeAttackStopsItAndDoesNotClearRecovery()
    {
        var fight=new DungeonSimulation(new DungeonSettings
            { Combat=new() { AutoPauseOnCombatStart=false } },
            enemies:new EnemyCatalogue { Enemies=[new EnemyDefinition { MoveSpeed=0 }] },
            combatRoll:()=>0.5f);
        fight.Player.Position=new(0,0,-28.5f);
        fight.UpdateCombat(0.1f);
        Assert.True(fight.SelectEnemyContextual());
        fight.RequestAttack();
        fight.Update(Vector2.UnitY,Vector2.Zero,true,0.1f);
        fight.RequestAttack(); // Input can issue an attack after movement in the same frame.
        fight.UpdateCombat(0.1f);
        Assert.False(fight.AutoAttackEnabled);
        Assert.Equal(fight.Enemy.MaxHealth,fight.Enemy.Health);

        fight.RequestAttack();
        fight.UpdateCombat(0.1f);
        Assert.True(fight.Enemy.Health<fight.Enemy.MaxHealth);
        Assert.Equal(AttackPhase.Cooldown,fight.Player.Phase);
        int health=fight.Enemy.Health;
        fight.Update(Vector2.UnitY,Vector2.Zero,true,0.1f);
        Assert.Equal(AttackPhase.Cooldown,fight.Player.Phase);
        fight.RequestAttack();
        fight.UpdateCombat(0.1f);
        Assert.Equal(health,fight.Enemy.Health);
    }

    [Fact]
    public void MovingAndPressingAnInstantSwordAbilityInTheSameFrameDoesNotHit()
    {
        var items=Settings.Required<ItemCatalogue>(Settings.ConfigPath("items.json"));
        var abilities=Settings.Required<AbilityCatalogue>(Settings.ConfigPath("abilities.json"));
        var statuses=Settings.Required<StatusCatalogue>(Settings.ConfigPath("statuses.json"));
        var player=new PlayerDefinition
        {
            SkillIds=["sword-novice"],
            AbilityIds=["sword-quick-cut"]
        };
        var fight=new DungeonSimulation(new DungeonSettings
            { Combat=new() { AutoPauseOnCombatStart=false } },
            items:items,playerDefinition:player,
            enemies:new EnemyCatalogue { Enemies=[new EnemyDefinition { MoveSpeed=0 }] },
            abilities:abilities,statuses:statuses,combatRoll:()=>0.5f);
        fight.Player.Inventory.Add("iron-sword",1);
        Assert.True(fight.Player.Equipment.Equip("iron-sword"));
        fight.Player.Position=new(0,0,-28.5f);
        fight.UpdateCombat(0.1f);
        Assert.True(fight.SelectEnemyContextual());

        fight.Update(Vector2.UnitY,Vector2.Zero,true,0.1f);
        Assert.Equal("Stop moving to use an ability",fight.RequestAbility("sword-quick-cut"));
        Assert.Equal(fight.Enemy.MaxHealth,fight.Enemy.Health);
        Assert.Equal(0,fight.AbilityCooldownFraction("sword-quick-cut"));
        fight.UpdateCombat(0.1f);
        Assert.Contains("damage",fight.RequestAbility("sword-quick-cut"));
        Assert.True(fight.Enemy.Health<fight.Enemy.MaxHealth);
    }

    private static DungeonSimulation AtDoor(DungeonSettings settings,EnemyCatalogue? enemies=null)
    {
        var simulation = new DungeonSimulation(settings,enemies:enemies);
        for (int i=0;i<23;i++)
            simulation.Update(Vector2.UnitY,Vector2.Zero,true,0.1f);
        simulation.UpdateInteraction(false);
        Assert.Same(simulation.World.Interactables[0],simulation.Target?.Item);
        return simulation;
    }
}

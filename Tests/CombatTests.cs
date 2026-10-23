using System.Numerics;
using CCM.Game;
using CCM.Application;
using Xunit;

namespace CCM.Tests;

public sealed class CombatTests
{
    [Fact]
    public void BothCombatantsUseTheSameCastAndCooldownRules()
    {
        var player = new PlayerEntity(Vector3.Zero,new CharacterAttributes());
        var enemy = new EnemyEntity(new(0,0,-1),new EnemyDefinition());
        Assert.True(player.InRange(enemy));
        Assert.True(enemy.InRange(player));
        float cast=player.Profile.CastSeconds;
        Assert.False(player.UpdateAttack(cast/2,enemy,true));
        Assert.Equal(AttackPhase.Casting,player.Phase);
        Assert.Equal(enemy.MaxHealth,enemy.Health);
        Assert.True(player.UpdateAttack(cast/2,enemy,true));
        int dealt=CombatFormula.DamageAfterDefence(player.Profile.Damage,enemy.Profile);
        Assert.Equal(enemy.MaxHealth-dealt,enemy.Health);
        Assert.Equal(AttackPhase.Cooldown,player.Phase);
        Assert.False(player.UpdateAttack(player.Profile.CooldownSeconds,enemy,true));
        Assert.Equal(AttackPhase.Ready,player.Phase);
        Assert.True(enemy.UpdateAttack(enemy.Profile.CastSeconds,player,true));
        int received=CombatFormula.DamageAfterDefence(enemy.Profile.Damage,player.Profile);
        Assert.Equal(player.MaxHealth-received,player.Health);
    }

    [Fact]
    public void AttackWhiffsWhenTargetLeavesReachBeforeImpact()
    {
        var player = new PlayerEntity(Vector3.Zero,new CharacterAttributes());
        var enemy = new EnemyEntity(new(0,0,-1),new EnemyDefinition());
        float halfCast=player.Profile.CastSeconds/2;
        player.UpdateAttack(halfCast,enemy,true);
        enemy.Position = new(0,0,-10);
        Assert.False(player.InRange(enemy));
        Assert.False(player.UpdateAttack(halfCast,enemy,true));
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
        Assert.Equal(100,unarmed.MaxHealth);
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
        Assert.True(player.UpdateAttack(armed.CastSeconds,enemy,true));
        Assert.Equal(enemy.MaxHealth-CombatFormula.DamageAfterDefence(armed.Damage,enemy.Profile),
            enemy.Health);

        Assert.True(player.Equipment.Unequip(EquipmentSlot.Weapon));
        Assert.Equal(unarmed.Range,player.Profile.Range);
        Assert.Equal(unarmed.Damage,player.Profile.Damage);
        player.Inventory.Add("arrow",20);
        Assert.False(player.Equipment.Equip("arrow"));
        Assert.Equal(20,player.Inventory.Count("arrow"));
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

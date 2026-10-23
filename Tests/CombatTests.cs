using System.Numerics;
using CCM.Game;
using Xunit;

namespace CCM.Tests;

public sealed class CombatTests
{
    [Fact]
    public void BothCombatantsUseTheSameCastAndCooldownRules()
    {
        var stats = new CombatSettings { CastSeconds=0.6f, CooldownSeconds=0.8f, Damage=12 };
        var player = new PlayerEntity(Vector3.Zero,stats);
        var enemy = new EnemyEntity(new(0,0,-1),stats);
        Assert.True(player.InRange(enemy));
        Assert.True(enemy.InRange(player));
        Assert.False(player.UpdateAttack(0.3f,enemy,true));
        Assert.Equal(AttackPhase.Casting,player.Phase);
        Assert.Equal(enemy.MaxHealth,enemy.Health);
        Assert.True(player.UpdateAttack(0.3f,enemy,true));
        Assert.Equal(enemy.MaxHealth-12,enemy.Health);
        Assert.Equal(AttackPhase.Cooldown,player.Phase);
        Assert.False(player.UpdateAttack(0.4f,enemy,true));
        Assert.False(player.UpdateAttack(0.4f,enemy,true));
        Assert.Equal(AttackPhase.Ready,player.Phase);
        Assert.True(enemy.UpdateAttack(0.6f,player,true));
        Assert.Equal(player.MaxHealth-12,player.Health);
    }

    [Fact]
    public void AttackWhiffsWhenTargetLeavesReachBeforeImpact()
    {
        var stats = new CombatSettings();
        var player = new PlayerEntity(Vector3.Zero,stats);
        var enemy = new EnemyEntity(new(0,0,-1),stats);
        player.UpdateAttack(0.3f,enemy,true);
        enemy.Position = new(0,0,-10);
        Assert.False(player.InRange(enemy));
        Assert.False(player.UpdateAttack(0.3f,enemy,true));
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
            { Combat = new() { AutoPauseOnCombatStart = false, VisionRange = 5 } });
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
        Assert.Throws<InvalidDataException>(()=>new DungeonSettings { Combat=new() { CastSeconds=0 } }.Validate());
        Assert.Throws<InvalidDataException>(()=>new DungeonSettings { Combat=new() { Range=float.NaN } }.Validate());
    }

    private static DungeonSimulation AtDoor(DungeonSettings settings)
    {
        var simulation = new DungeonSimulation(settings);
        for (int i=0;i<23;i++)
            simulation.Update(Vector2.UnitY,Vector2.Zero,true,0.1f);
        simulation.UpdateInteraction(false);
        Assert.Same(simulation.World.Interactables[0],simulation.Target?.Item);
        return simulation;
    }
}

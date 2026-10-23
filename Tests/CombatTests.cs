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
    public void OpeningDoorStartsAndOptionallyPausesCombat()
    {
        var paused = AtDoor(new());
        paused.UpdateInteraction(true);
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
        Assert.True(immediate.CombatStarted);
        Assert.False(immediate.Paused);
    }

    [Fact]
    public void SelectionAndAttackAreRequiredForPlayerAutoAttack()
    {
        var simulation = AtDoor(new DungeonSettings { Combat = new() { AutoPauseOnCombatStart=false } });
        simulation.UpdateInteraction(true);
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

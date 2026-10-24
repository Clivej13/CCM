using System.Numerics;
using CCM.Application;
using CCM.Game;
using Xunit;

namespace CCM.Tests;

public sealed class SkillTests
{
    [Fact]
    public void BundledSkillsRequireAttributesAndExactlyTwoSelections()
    {
        var catalogue=Settings.Required<SkillCatalogue>(Settings.ConfigPath("skills.json"));
        catalogue.Validate();
        var player=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json"));
        player.Validate();
        var build=new CharacterBuild(player.Attributes,catalogue);
        Assert.Equal(2,build.RemainingSkillPoints);
        Assert.All(Enum.GetValues<AttributeKind>(),
            kind=>Assert.Equal(4,build.Attributes.Get(kind)));
        Assert.False(build.CanComplete);
        Assert.True(build.SetAttribute(AttributeKind.Strength,2));
        Assert.False(build.ToggleSkill("power-strike"));
        Assert.True(build.SetAttribute(AttributeKind.Strength,3));
        Assert.True(build.ToggleSkill("power-strike"));
        Assert.True(build.ToggleSkill("arcane-spark"));
        Assert.False(build.ToggleSkill("quickstep"));
        Assert.True(build.CanComplete);
        var chosen=build.Complete();
        Assert.Equal(3,chosen.Attributes.Strength);
        Assert.Equal(4,chosen.Attributes.Intelligence);
        Assert.Equal(new[] { "power-strike", "arcane-spark" },chosen.SkillIds);
        var entity=new PlayerEntity(Vector3.Zero,chosen.Attributes,skillIds:chosen.SkillIds);
        Assert.Equal(chosen.SkillIds,entity.SkillIds);
        Assert.Equal(new PlayerEntity(Vector3.Zero,chosen.Attributes).Profile,entity.Profile);
    }

    [Fact]
    public void AvailableSkillsSortFirstAndKeepCatalogueOrderWithinEachGroup()
    {
        var catalogue=Settings.Required<SkillCatalogue>(Settings.ConfigPath("skills.json"));
        var starting=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json")).Attributes;
        var catalogueOrder=catalogue.Skills.Select(skill=>skill.Id).ToArray();
        var locked=starting;
        foreach (var kind in Enum.GetValues<AttributeKind>())
            locked=locked.With(kind,1);
        Assert.Equal(catalogueOrder,
            catalogue.OrderedFor(locked).Select(skill=>skill.Id));

        var attributes=locked.With(AttributeKind.Speed,3)
            .With(AttributeKind.Strength,3).With(AttributeKind.Luck,3);
        var ordered=catalogue.OrderedFor(attributes);
        Assert.Equal(new[] { "power-strike", "fleet-foot", "fortunate-find" },
            ordered.Take(3).Select(skill=>skill.Id));
        Assert.Equal(catalogue.Skills.Where(skill=>!skill.IsAvailable(attributes))
            .Select(skill=>skill.Id),ordered.Skip(3).Select(skill=>skill.Id));

        var reordered=catalogue.OrderedFor(attributes.With(AttributeKind.Strength,2));
        Assert.Equal(new[] { "fleet-foot", "fortunate-find" },
            reordered.Take(2).Select(skill=>skill.Id));
        Assert.Equal(catalogueOrder,catalogue.Skills.Select(skill=>skill.Id));
    }

    [Fact]
    public void LoweringAnAttributeRefundsInvalidSkills()
    {
        var catalogue=Settings.Required<SkillCatalogue>(Settings.ConfigPath("skills.json"));
        var build=new CharacterBuild(new CharacterAttributes { Strength=3, Agility=3 },catalogue);
        Assert.True(build.ToggleSkill("power-strike"));
        Assert.True(build.ToggleSkill("quickstep"));
        Assert.True(build.SetAttribute(AttributeKind.Strength,2));
        Assert.Equal(1,build.RemainingSkillPoints);
        Assert.DoesNotContain("power-strike",build.SelectedSkills);
        Assert.Contains("quickstep",build.SelectedSkills);
        Assert.Throws<InvalidOperationException>(()=>build.Complete());
        Assert.False(build.SetAttribute(AttributeKind.Strength,0));
        Assert.Equal(2,build.Attributes.Strength);
    }

    [Theory]
    [InlineData(AttributeKind.Speed,"fleet-foot")]
    [InlineData(AttributeKind.Charisma,"silver-tongue")]
    [InlineData(AttributeKind.Luck,"fortunate-find")]
    public void NewAttributesUnlockAndRefundTheirSkills(AttributeKind kind,string skillId)
    {
        var catalogue=Settings.Required<SkillCatalogue>(Settings.ConfigPath("skills.json"));
        catalogue.Validate();
        var starting=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json")).Attributes;
        var build=new CharacterBuild(starting,catalogue);
        Assert.True(build.SetAttribute(kind,2));
        Assert.False(build.ToggleSkill(skillId));
        Assert.True(build.SetAttribute(kind,3));
        Assert.True(build.ToggleSkill(skillId));
        Assert.Equal(3,build.Attributes.Copy().Get(kind));
        Assert.True(build.SetAttribute(kind,2));
        Assert.DoesNotContain(skillId,build.SelectedSkills);
        Assert.Equal(2,build.RemainingSkillPoints);
    }

    [Theory]
    [InlineData("vanguards-edge",2)]
    [InlineData("battle-scholar",2)]
    [InlineData("fortune-favors",3)]
    public void MultiRequirementSkillsNeedEveryAttributeAndRefundWhenAnyFalls(
        string skillId,int requirementCount)
    {
        var catalogue=Settings.Required<SkillCatalogue>(Settings.ConfigPath("skills.json"));
        var starting=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json")).Attributes;
        var skill=catalogue.Get(skillId);
        Assert.Equal(requirementCount,skill.Requirements.Count);
        var below=starting;
        foreach (var requirement in skill.Requirements)
            below=below.With(requirement.Kind,requirement.Minimum-1);
        foreach (var missing in skill.Requirements)
        {
            var build=new CharacterBuild(below,catalogue);
            foreach (var requirement in skill.Requirements.Where(item=>item!=missing))
                Assert.True(build.SetAttribute(requirement.Kind,requirement.Minimum));
            Assert.False(build.ToggleSkill(skillId));
            Assert.DoesNotContain(skillId,catalogue.OrderedFor(build.Attributes)
                .TakeWhile(item=>item.IsAvailable(build.Attributes)).Select(item=>item.Id));

            Assert.True(build.SetAttribute(missing.Kind,missing.Minimum));
            Assert.True(build.ToggleSkill(skillId));
            Assert.Contains(skillId,build.SelectedSkills);
            Assert.Contains(skillId,catalogue.OrderedFor(build.Attributes)
                .TakeWhile(item=>item.IsAvailable(build.Attributes)).Select(item=>item.Id));

            Assert.True(build.SetAttribute(missing.Kind,missing.Minimum-1));
            Assert.DoesNotContain(skillId,build.SelectedSkills);
            Assert.Equal(catalogue.SkillPoints,build.RemainingSkillPoints);
        }
    }

    [Fact]
    public void NewAttributesRespectTheExistingBounds()
    {
        Assert.Throws<InvalidDataException>(()=>new CharacterAttributes { Speed=0 }.Validate());
        Assert.Throws<InvalidDataException>(()=>new CharacterAttributes { Charisma=11 }.Validate());
        Assert.Throws<InvalidDataException>(()=>new CharacterAttributes { Luck=0 }.Validate());
    }

    [Fact]
    public void EveryAttributeCapsAtTenAndSkillRequirementsCannotExceedIt()
    {
        var catalogue=Settings.Required<SkillCatalogue>(Settings.ConfigPath("skills.json"));
        var starting=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json"));
        var build=new CharacterBuild(starting.Attributes,catalogue);
        foreach (var kind in Enum.GetValues<AttributeKind>())
        {
            Assert.True(build.SetAttribute(kind,CharacterAttributes.MaximumValue));
            Assert.False(build.SetAttribute(kind,CharacterAttributes.MaximumValue+1));
            Assert.Equal(CharacterAttributes.MaximumValue,build.Attributes.Get(kind));
            Assert.Throws<InvalidDataException>(()=>
                build.Attributes.With(kind,CharacterAttributes.MaximumValue+1).Validate());
        }
        Assert.Throws<InvalidDataException>(()=>
            new SkillRequirement { Attribute="Strength", Minimum=11 }.Validate());
    }

    [Fact]
    public void SpeedChangesMovementWithoutChangingCombatSkills()
    {
        var slow=new DungeonSimulation(new DungeonSettings(),
            playerDefinition:new PlayerDefinition
            {
                Attributes=new CharacterAttributes { Speed=1 }
            });
        var fast=new DungeonSimulation(new DungeonSettings(),
            playerDefinition:new PlayerDefinition
            {
                Attributes=new CharacterAttributes { Speed=10 }
            });
        slow.Update(new System.Numerics.Vector2(0,1),System.Numerics.Vector2.Zero,true,0.1f);
        fast.Update(new System.Numerics.Vector2(0,1),System.Numerics.Vector2.Zero,true,0.1f);
        Assert.True(fast.Player.Position.Z<slow.Player.Position.Z);
        Assert.Equal(slow.Player.Profile,fast.Player.Profile);
    }

    [Fact]
    public void SkillCatalogueRejectsBrokenDefinitions()
    {
        var catalogue=Settings.Required<SkillCatalogue>(Settings.ConfigPath("skills.json"));
        catalogue.Skills.Add(catalogue.Skills[0]);
        Assert.Throws<InvalidDataException>(catalogue.Validate);
        catalogue.Skills.RemoveAt(catalogue.Skills.Count-1);
        catalogue.Skills[0].Requirements.Add(new SkillRequirement
            { Attribute="Strength", Minimum=4 });
        Assert.Throws<InvalidDataException>(catalogue.Validate);

        var tooMany=new SkillDefinition
        {
            Id="too-many", Name="Too Many", Description="Four requirements",
            Requirements=
            [
                new() { Attribute="Strength", Minimum=2 },
                new() { Attribute="Agility", Minimum=2 },
                new() { Attribute="Luck", Minimum=2 },
                new() { Attribute="Speed", Minimum=2 }
            ]
        };
        Assert.Throws<InvalidDataException>(tooMany.Validate);
    }
}

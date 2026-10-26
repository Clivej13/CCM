using System.Numerics;
using CCM.Application;
using CCM.Game;
using Xunit;

namespace CCM.Tests;

public sealed class SkillTests
{
    private static (SkillCatalogue Skills,CharacterAttributes Attributes) Bundled()
    {
        var skills=Settings.Required<SkillCatalogue>(Settings.ConfigPath("skills.json"));
        skills.Validate();
        var attributes=Settings.Required<PlayerDefinition>(Settings.ConfigPath("player.json")).Attributes;
        return (skills,attributes);
    }

    [Fact]
    public void SwordNoviceAndAdeptSpendTwoPointsInOrder()
    {
        var (skills,attributes)=Bundled();
        var build=new CharacterBuild(attributes,skills);
        Assert.Equal(2,build.RemainingSkillPoints);
        Assert.All(Enum.GetValues<AttributeKind>(),
            kind=>Assert.Equal(4,build.Attributes.Get(kind)));
        Assert.False(build.ToggleSkill("sword-adept"));
        Assert.True(build.SetAttribute(AttributeKind.Strength,3));
        Assert.False(build.ToggleSkill("sword-novice"));
        Assert.True(build.SetAttribute(AttributeKind.Strength,4));
        Assert.True(build.ToggleSkill("sword-novice"));
        Assert.False(build.ToggleSkill("sword-adept"));
        Assert.True(build.SetAttribute(AttributeKind.Strength,5));
        Assert.True(build.ToggleSkill("sword-adept"));
        Assert.False(build.HasUnspentPoints);
        var chosen=build.Complete();
        Assert.Equal(new[] { "sword-novice","sword-adept" },chosen.SkillIds);
        var entity=new PlayerEntity(Vector3.Zero,chosen.Attributes,skillIds:chosen.SkillIds);
        Assert.Equal(chosen.SkillIds,entity.SkillIds);
        Assert.Equal(new PlayerEntity(Vector3.Zero,chosen.Attributes).Profile,entity.Profile);
    }

    [Fact]
    public void AvailableSkillsSortAheadOfLockedPrerequisites()
    {
        var (skills,attributes)=Bundled();
        var build=new CharacterBuild(attributes,skills);
        Assert.Equal(new[] { "sword-novice","bow-novice","sword-adept" },
            skills.OrderedFor(build.Attributes,build.SelectedSkills).Select(s=>s.Id));
        Assert.True(build.SetAttribute(AttributeKind.Strength,5));
        Assert.False(skills.Get("sword-adept").IsAvailable(build.Attributes,build.SelectedSkills));
        Assert.True(build.ToggleSkill("sword-novice"));
        Assert.True(skills.Get("sword-adept").IsAvailable(build.Attributes,build.SelectedSkills));
        Assert.Equal(new[] { "sword-novice","sword-adept","bow-novice" },
            skills.OrderedFor(build.Attributes,build.SelectedSkills).Select(s=>s.Id));
    }

    [Fact]
    public void RemovingNoviceOrLoweringAttributesRefundsAdept()
    {
        var (skills,attributes)=Bundled();
        var build=new CharacterBuild(attributes.With(AttributeKind.Strength,5),skills);
        Assert.True(build.ToggleSkill("sword-novice"));
        Assert.True(build.ToggleSkill("sword-adept"));
        Assert.True(build.ToggleSkill("sword-novice"));
        Assert.Empty(build.SelectedSkills);
        Assert.Equal(2,build.RemainingSkillPoints);

        Assert.True(build.ToggleSkill("sword-novice"));
        Assert.True(build.ToggleSkill("sword-adept"));
        Assert.True(build.SetAttribute(AttributeKind.Strength,4));
        Assert.Equal(new[] { "sword-novice" },build.SelectedSkills);
        Assert.Equal(1,build.RemainingSkillPoints);
        Assert.True(build.HasUnspentPoints);
        Assert.Equal(new[] { "sword-novice" },build.Complete().SkillIds);
        Assert.False(build.SetAttribute(AttributeKind.Strength,0));
        Assert.Equal(4,build.Attributes.Strength);
    }

    [Fact]
    public void AdeptNeedsBothAttributesAndNovice()
    {
        var (skills,attributes)=Bundled();
        var adept=skills.Get("sword-adept");
        Assert.Equal(2,adept.Requirements.Count);
        Assert.Equal(new[] { "sword-novice" },adept.RequiredSkills);
        foreach (var missing in adept.Requirements)
        {
            var build=new CharacterBuild(attributes,skills);
            Assert.True(build.ToggleSkill("sword-novice"));
            foreach (var requirement in adept.Requirements)
                Assert.True(build.SetAttribute(requirement.Kind,
                    requirement==missing ? requirement.Minimum-1 : requirement.Minimum));
            Assert.False(build.ToggleSkill("sword-adept"));
            Assert.True(build.SetAttribute(missing.Kind,missing.Minimum));
            Assert.True(build.ToggleSkill("sword-adept"));
            Assert.True(build.SetAttribute(missing.Kind,missing.Minimum-1));
            Assert.DoesNotContain("sword-adept",build.SelectedSkills);
        }
    }

    [Fact]
    public void EveryAttributeRemainsBoundedFromOneToTen()
    {
        var (skills,starting)=Bundled();
        var build=new CharacterBuild(starting,skills);
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
        Assert.Throws<InvalidDataException>(()=>new CharacterAttributes { Speed=0 }.Validate());
        Assert.Throws<InvalidDataException>(()=>new CharacterAttributes { Charisma=11 }.Validate());
        Assert.Throws<InvalidDataException>(()=>new CharacterAttributes { Luck=0 }.Validate());
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
        slow.Update(new Vector2(0,1),Vector2.Zero,true,0.1f);
        fast.Update(new Vector2(0,1),Vector2.Zero,true,0.1f);
        Assert.True(fast.Player.Position.Z<slow.Player.Position.Z);
        Assert.Equal(slow.Player.Profile,fast.Player.Profile);
    }

    [Fact]
    public void SkillCatalogueRejectsBrokenDefinitionsAndCycles()
    {
        var (skills,_)=Bundled();
        skills.Skills.Add(skills.Skills[0]);
        Assert.Throws<InvalidDataException>(skills.Validate);
        skills.Skills.RemoveAt(skills.Skills.Count-1);
        skills.Skills[0].Requirements.Add(new SkillRequirement
            { Attribute="Strength", Minimum=4 });
        Assert.Throws<InvalidDataException>(skills.Validate);
        skills.Skills[0].Requirements.RemoveAt(skills.Skills[0].Requirements.Count-1);

        skills.Get("sword-novice").RequiredSkills.Add("missing");
        Assert.Throws<InvalidDataException>(skills.Validate);
        skills.Get("sword-novice").RequiredSkills.Remove("missing");
        skills.Get("sword-novice").RequiredSkills.Add("sword-adept");
        Assert.Throws<InvalidDataException>(skills.Validate);

        var tooMany=new SkillDefinition
        {
            Id="too-many",Name="Too Many",Description="Four requirements",
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

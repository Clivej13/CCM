using CCM.Application;
using CCM.Game;
using Raylib_cs;

namespace CCM.Scenes;

/// <summary>Collects a player build before the dungeon is created.</summary>
public sealed class LevelUpScene : Scene
{
    private enum SetupTab { Attributes, Skills, Abilities, Finish }
    private static readonly AttributeKind[] AttributeKinds=Enum.GetValues<AttributeKind>();
    private const int VisibleRows=8;
    private const int RowTop=128;
    private const int RowStep=34;
    private readonly SessionInput input;
    private PlayerDefinition source;
    private HashSet<string> knownFromTomes=new(StringComparer.Ordinal);
    private bool returningFromGame;
    private readonly SkillCatalogue skills;
    private readonly AbilityCatalogue abilities;
    private readonly AttributeDescriptionCatalogue descriptions;
    private readonly Action<PlayerDefinition> completed;
    private readonly Action cancelled;
    private CharacterBuild build;
    private SetupTab tab;
    private int attributeIndex,skillIndex,skillScroll,abilityIndex,abilityScroll;

    public LevelUpScene(SessionInput input,PlayerDefinition startingPlayer,SkillCatalogue skills,
        AbilityCatalogue abilities,AttributeDescriptionCatalogue descriptions,
        Action<PlayerDefinition> completed,Action cancelled)
    {
        this.input=input;
        source=startingPlayer;
        this.skills=skills;
        this.abilities=abilities;
        this.descriptions=descriptions;
        this.completed=completed;
        this.cancelled=cancelled;
        build=new CharacterBuild(startingPlayer,skills,abilities);
    }

    public void Prepare(PlayerDefinition current,IEnumerable<string>? learnedFromTomes=null,
        bool fromGame=false)
    {
        source=current;
        knownFromTomes=new HashSet<string>(learnedFromTomes ?? [],StringComparer.Ordinal);
        returningFromGame=fromGame;
    }

    public override void Enter()
    {
        build=new CharacterBuild(source,skills,abilities,knownFromTomes);
        tab=returningFromGame
            ? build.RemainingSkillPoints>0 ? SetupTab.Skills
                : build.RemainingAbilityPoints>0 ? SetupTab.Abilities : SetupTab.Finish
            : SetupTab.Attributes;
        attributeIndex=skillIndex=skillScroll=abilityIndex=abilityScroll=0;
    }

    public override void Update(float seconds)
    {
        if (!Raylib.IsWindowFocused()) return;
        if (!input.UsesMouse && input.ReturnPressed())
        {
            RemoveSelectedPoint();
            return;
        }
        if (input.ReturnPressed() || input.PausePressed())
        {
            if (tab==SetupTab.Attributes) cancelled();
            else tab=(SetupTab)((int)tab-1);
            return;
        }
        if (input.UsesMouse)
        {
            if (input.SelectPressed() && HandleClick(false)) return;
            if (tab!=SetupTab.Attributes && input.AttackPressed() && HandleClick(true)) return;
        }
        if (input.UiPanePressed()) CycleTab(1);
        if (input.PanelLeftPressed()) CycleTab(-1);
        if (input.PanelRightPressed()) CycleTab(1);
        if (tab is SetupTab.Skills or SetupTab.Abilities && input.UsesMouse && input.Zoom!=0)
            ScrollList();

        if (input.UiUpPressed()) MoveSelection(-1);
        if (input.UiDownPressed()) MoveSelection(1);
        switch (tab)
        {
            case SetupTab.Skills:
                if (!input.UiConfirmPressed()) return;
                if (skillIndex==skills.Skills.Count) tab=SetupTab.Abilities;
                else ChangeSkill(OrderedSkills()[skillIndex].Id);
                break;
            case SetupTab.Abilities:
                if (!input.UiConfirmPressed()) return;
                if (abilityIndex==abilities.Abilities.Count) tab=SetupTab.Finish;
                else build.ToggleAbility(OrderedAbilities()[abilityIndex].Id);
                break;
            case SetupTab.Finish:
                if (input.UiConfirmPressed()) TryComplete();
                break;
            default:
                if (attributeIndex==AttributeKinds.Length)
                {
                    if (input.UiConfirmPressed()) tab=SetupTab.Skills;
                    return;
                }
                var kind=AttributeKinds[attributeIndex];
                int value=build.Attributes.Get(kind);
                if (input.UiLeftPressed()) ChangeAttribute(kind,value-1);
                if (input.UiRightPressed() || input.UiConfirmPressed())
                    ChangeAttribute(kind,value+1);
                break;
        }
    }

    private void RemoveSelectedPoint()
    {
        switch (tab)
        {
            case SetupTab.Attributes when attributeIndex<AttributeKinds.Length:
                var kind=AttributeKinds[attributeIndex];
                ChangeAttribute(kind,build.Attributes.Get(kind)-1);
                break;
            case SetupTab.Skills when skillIndex<skills.Skills.Count:
                var skill=OrderedSkills()[skillIndex];
                if (build.SelectedSkills.Contains(skill.Id)) ChangeSkill(skill.Id);
                break;
            case SetupTab.Abilities when abilityIndex<abilities.Abilities.Count:
                var ability=OrderedAbilities()[abilityIndex];
                if (build.SelectedAbilities.Contains(ability.Id)) build.ToggleAbility(ability.Id);
                break;
            case SetupTab.Finish:
                tab=SetupTab.Abilities;
                break;
        }
    }

    private void CycleTab(int direction) =>
        tab=(SetupTab)(((int)tab+direction+4)%4);

    private void MoveSelection(int direction)
    {
        switch (tab)
        {
            case SetupTab.Attributes:
                attributeIndex=(attributeIndex+direction+AttributeKinds.Length+1)%
                    (AttributeKinds.Length+1);
                break;
            case SetupTab.Skills:
                skillIndex=(skillIndex+direction+skills.Skills.Count+1)%
                    (skills.Skills.Count+1);
                KeepSkillVisible();
                break;
            case SetupTab.Abilities:
                abilityIndex=(abilityIndex+direction+abilities.Abilities.Count+1)%
                    (abilities.Abilities.Count+1);
                KeepAbilityVisible();
                break;
        }
    }

    private void ScrollList()
    {
        var pointer=Raylib.GetMousePosition();
        var (x,y,width)=Layout();
        if (pointer.X<x+16 || pointer.X>=x+width/2 ||
            pointer.Y<y+RowTop || pointer.Y>=y+RowTop+VisibleRows*RowStep) return;
        if (tab==SetupTab.Skills)
        {
            skillScroll=Math.Clamp(skillScroll-Math.Sign(input.Zoom),
                0,Math.Max(0,skills.Skills.Count-VisibleRows));
            skillIndex=Math.Clamp(skillIndex,skillScroll,
                Math.Min(skillScroll+VisibleRows-1,skills.Skills.Count-1));
        }
        else
        {
            abilityScroll=Math.Clamp(abilityScroll-Math.Sign(input.Zoom),
                0,Math.Max(0,abilities.Abilities.Count-VisibleRows));
            abilityIndex=Math.Clamp(abilityIndex,abilityScroll,
                Math.Min(abilityScroll+VisibleRows-1,abilities.Abilities.Count-1));
        }
    }

    private bool HandleClick(bool activate)
    {
        var pointer=Raylib.GetMousePosition();
        var (x,y,width)=Layout();
        int middle=x+width/2;
        if (!activate && pointer.Y>=y+64 && pointer.Y<y+98)
        {
            int tabWidth=TabWidth(width);
            for (int i=0;i<4;i++)
            {
                int tabX=x+20+i*(tabWidth+8);
                if (pointer.X>=tabX && pointer.X<tabX+tabWidth)
                {
                    tab=(SetupTab)i;
                    return true;
                }
            }
        }
        if (pointer.X<x+16 || pointer.X>=middle-8) return false;
        if (tab==SetupTab.Attributes && !activate)
        {
            for (int i=0;i<AttributeKinds.Length;i++)
            {
                int row=y+RowTop+i*RowStep;
                if (pointer.Y<row || pointer.Y>=row+30) continue;
                attributeIndex=i;
                int value=build.Attributes.Get(AttributeKinds[i]);
                if (pointer.X>=middle-94 && pointer.X<middle-68)
                    ChangeAttribute(AttributeKinds[i],value-1);
                else if (pointer.X>=middle-45 && pointer.X<middle-19)
                    ChangeAttribute(AttributeKinds[i],value+1);
                return true;
            }
        }
        else if (tab==SetupTab.Skills)
        {
            var ordered=OrderedSkills();
            for (int i=skillScroll;i<Math.Min(ordered.Count,skillScroll+VisibleRows);i++)
            {
                int row=y+RowTop+(i-skillScroll)*RowStep;
                if (pointer.Y<row || pointer.Y>=row+30) continue;
                skillIndex=i;
                if (activate) ChangeSkill(ordered[i].Id);
                return true;
            }
        }
        else if (tab==SetupTab.Abilities)
        {
            var ordered=OrderedAbilities();
            for (int i=abilityScroll;i<Math.Min(ordered.Count,abilityScroll+VisibleRows);i++)
            {
                int row=y+RowTop+(i-abilityScroll)*RowStep;
                if (pointer.Y<row || pointer.Y>=row+30) continue;
                abilityIndex=i;
                if (activate) build.ToggleAbility(ordered[i].Id);
                return true;
            }
        }
        if (!activate && pointer.Y>=y+408 && pointer.Y<y+446)
        {
            switch (tab)
            {
                case SetupTab.Attributes:
                    attributeIndex=AttributeKinds.Length;
                    tab=SetupTab.Skills;
                    break;
                case SetupTab.Skills:
                    skillIndex=skills.Skills.Count;
                    tab=SetupTab.Abilities;
                    break;
                case SetupTab.Abilities:
                    abilityIndex=abilities.Abilities.Count;
                    tab=SetupTab.Finish;
                    break;
                case SetupTab.Finish:
                    TryComplete();
                    break;
            }
            return true;
        }
        return false;
    }

    private IReadOnlyList<SkillDefinition> OrderedSkills() =>
        skills.OrderedFor(build.Attributes,build.SelectedSkills);
    private IReadOnlyList<AbilityDefinition> OrderedAbilities() =>
        abilities.OrderedFor(build.Attributes,build.SelectedSkills);

    private void ChangeSkill(string id)
    {
        string? selectedAbility=abilityIndex<abilities.Abilities.Count
            ? OrderedAbilities()[abilityIndex].Id : null;
        if (!build.ToggleSkill(id)) return;
        if (selectedAbility is not null) FocusAbility(selectedAbility);
        KeepAbilityVisible();
    }

    private void ChangeAttribute(AttributeKind kind,int value)
    {
        string? selectedSkill=skillIndex<skills.Skills.Count ? OrderedSkills()[skillIndex].Id : null;
        string? selectedAbility=abilityIndex<abilities.Abilities.Count
            ? OrderedAbilities()[abilityIndex].Id : null;
        if (!build.SetAttribute(kind,value)) return;
        if (selectedSkill is not null)
        {
            var ordered=OrderedSkills();
            for (int i=0;i<ordered.Count;i++)
                if (ordered[i].Id==selectedSkill) { skillIndex=i; break; }
        }
        if (selectedAbility is not null) FocusAbility(selectedAbility);
        KeepSkillVisible();
        KeepAbilityVisible();
    }

    private void FocusAbility(string id)
    {
        var ordered=OrderedAbilities();
        for (int i=0;i<ordered.Count;i++)
            if (ordered[i].Id==id) { abilityIndex=i; break; }
    }

    private void KeepAbilityVisible()
    {
        if (abilityIndex>=abilities.Abilities.Count) return;
        if (abilityIndex<abilityScroll) abilityScroll=abilityIndex;
        else if (abilityIndex>=abilityScroll+VisibleRows)
            abilityScroll=abilityIndex-VisibleRows+1;
    }

    private void KeepSkillVisible()
    {
        if (skillIndex>=skills.Skills.Count) return;
        if (skillIndex<skillScroll) skillScroll=skillIndex;
        else if (skillIndex>=skillScroll+VisibleRows)
            skillScroll=skillIndex-VisibleRows+1;
    }

    private void TryComplete() => completed(build.Complete());

    /// <summary>Exercises setup through the native startup smoke path.</summary>
    public void CompleteForSmoke()
    {
        foreach (var skill in skills.Skills.Take(skills.SkillPoints))
        {
            foreach (var requirement in skill.Requirements)
                build.SetAttribute(requirement.Kind,
                    Math.Max(build.Attributes.Get(requirement.Kind),requirement.Minimum));
            build.ToggleSkill(skill.Id);
        }
        foreach (var ability in OrderedAbilities())
        {
            if (build.RemainingAbilityPoints==0) break;
            if (ability.MeetsRequirements(build.Attributes,build.SelectedSkills))
                build.ToggleAbility(ability.Id);
        }
        TryComplete();
    }

    public override void Render()
    {
        Raylib.ClearBackground(new Color(12,16,24,255));
        var (x,y,width)=Layout();
        int middle=x+width/2;
        Raylib.DrawRectangle(x,y,width,456,new Color(30,34,42,255));
        Raylib.DrawRectangleLines(x,y,width,456,Color.Gold);
        Raylib.DrawText("LEVEL UP",x+20,y+16,30,Color.Gold);
        int tabWidth=TabWidth(width);
        DrawTab("ATTRIBUTES",x+20,y+64,tabWidth,tab==SetupTab.Attributes);
        DrawTab("SKILLS",x+28+tabWidth,y+64,tabWidth,tab==SetupTab.Skills);
        DrawTab("ABILITIES",x+36+tabWidth*2,y+64,tabWidth,tab==SetupTab.Abilities);
        DrawTab("FINISH",x+44+tabWidth*3,y+64,tabWidth,tab==SetupTab.Finish);
        Raylib.DrawRectangle(middle,y+112,1,334,Color.DarkGray);
        switch (tab)
        {
            case SetupTab.Attributes: RenderAttributes(x,y,width); break;
            case SetupTab.Skills: RenderSkills(x,y,width); break;
            case SetupTab.Abilities: RenderAbilities(x,y,width); break;
            case SetupTab.Finish: RenderFinish(x,y,width); break;
        }
    }

    private static int TabWidth(int width) => (width-64)/4;

    private static void DrawTab(string label,int x,int y,int width,bool selected)
    {
        Raylib.DrawRectangle(x,y,width,34,selected ? Color.Gold : Color.DarkGray);
        Raylib.DrawText(Fit(label,width-16,20),x+8,y+7,20,
            selected ? Color.Black : Color.RayWhite);
    }

    private void RenderAttributes(int x,int y,int width)
    {
        int middle=x+width/2;
        Raylib.DrawText("ATTRIBUTES",x+20,y+107,18,Color.RayWhite);
        for (int i=0;i<AttributeKinds.Length;i++)
        {
            int row=y+RowTop+i*RowStep;
            var kind=AttributeKinds[i];
            Raylib.DrawRectangle(x+16,row,middle-x-24,30,
                attributeIndex==i ? new Color(88,74,39,255) : new Color(45,50,59,255));
            Raylib.DrawText(kind.ToString(),x+24,row+7,16,Color.RayWhite);
            int value=build.Attributes.Get(kind);
            DrawValueButton("-",middle-94,row+2,value>CharacterAttributes.MinimumValue);
            Raylib.DrawText(value.ToString(),middle-61,row+7,16,Color.Gold);
            DrawValueButton("+",middle-45,row+2,value<CharacterAttributes.MaximumValue);
        }
        DrawAction(x+16,y+408,middle-x-24,"VIEW SKILLS",
            attributeIndex==AttributeKinds.Length,true);
        var selected=AttributeKinds[Math.Min(attributeIndex,AttributeKinds.Length-1)];
        var description=descriptions.Get(selected);
        DrawDetailsPanel(middle+8,y+124,width/2-24,322);
        int textX=middle+20,contentWidth=width/2-48;
        Raylib.DrawText(selected.ToString(),textX,y+140,24,Color.Gold);
        Raylib.DrawText($"Current value: {build.Attributes.Get(selected)} / {CharacterAttributes.MaximumValue}",
            textX,y+176,17,Color.RayWhite);
        Raylib.DrawText(Fit(description.Summary,contentWidth,18),
            textX,y+214,18,Color.Gold);
        DrawWrapped(description.Details,textX,y+249,contentWidth,16,8);
    }

    private void RenderSkills(int x,int y,int width)
    {
        int middle=x+width/2;
        Raylib.DrawText($"SKILLS - {build.RemainingSkillPoints} points left",
            x+20,y+107,18,Color.RayWhite);
        var ordered=OrderedSkills();
        for (int i=skillScroll;i<Math.Min(ordered.Count,skillScroll+VisibleRows);i++)
        {
            var skill=ordered[i];
            int row=y+RowTop+(i-skillScroll)*RowStep;
            bool available=skill.IsAvailable(build.Attributes,build.SelectedSkills);
            bool chosen=build.SelectedSkills.Contains(skill.Id);
            Raylib.DrawRectangle(x+16,row,middle-x-24,30,
                chosen ? new Color(70,81,44,255)
                    : skillIndex==i ? new Color(88,74,39,255) : new Color(45,50,59,255));
            if (skillIndex==i)
                Raylib.DrawRectangleLines(x+16,row,middle-x-24,30,Color.Gold);
            string label=$"{(chosen ? "[x]" : "[ ]")} {skill.Name}";
            Raylib.DrawText(Fit(label,middle-x-42,16),x+24,row+7,16,
                available ? Color.RayWhite : Color.Gray);
        }
        if (skills.Skills.Count>VisibleRows)
        {
            int trackHeight=VisibleRows*RowStep-4;
            int thumbHeight=Math.Max(24,trackHeight*VisibleRows/skills.Skills.Count);
            int thumbY=y+RowTop+(trackHeight-thumbHeight)*skillScroll/
                (skills.Skills.Count-VisibleRows);
            Raylib.DrawRectangle(middle-15,y+RowTop,4,trackHeight,Color.DarkGray);
            Raylib.DrawRectangle(middle-15,thumbY,4,thumbHeight,Color.Gold);
        }
        DrawAction(x+16,y+408,middle-x-24,"VIEW ABILITIES",
            skillIndex==skills.Skills.Count,true);
        var skillDetail=ordered[Math.Min(skillIndex,ordered.Count-1)];
        DrawDetailsPanel(middle+8,y+124,width/2-24,322);
        int textX=middle+20,contentWidth=width/2-48;
        Raylib.DrawText(Fit(skillDetail.Name,contentWidth,24),
            textX,y+140,24,Color.Gold);
        bool eligible=skillDetail.IsAvailable(build.Attributes,build.SelectedSkills);
        string status=build.SelectedSkills.Contains(skillDetail.Id) ? "Selected"
            : eligible ? "Available" : "Locked";
        Raylib.DrawText(status,textX,y+178,17,eligible ? Color.Gold : Color.Gray);
        DrawWrapped(skillDetail.Description,textX,y+211,contentWidth,16,4);
        Raylib.DrawText("REQUIREMENTS",textX,y+288,18,Color.Gold);
        int line=0;
        foreach (var requirement in skillDetail.Requirements)
        {
            int current=build.Attributes.Get(requirement.Kind);
            string label=$"{requirement.Attribute}: {current} / {requirement.Minimum}";
            Raylib.DrawText(Fit(label,contentWidth,16),textX,y+317+line++*22,16,
                current>=requirement.Minimum ? Color.RayWhite : Color.Gray);
        }
        foreach (var id in skillDetail.RequiredSkills)
        {
            bool learned=build.SelectedSkills.Contains(id);
            Raylib.DrawText(Fit($"Skill: {skills.Get(id).Name}",contentWidth,16),
                textX,y+317+line++*22,16,learned ? Color.RayWhite : Color.Gray);
        }
        Raylib.DrawText("Unlocks catalogue abilities",textX,y+416,14,Color.Gold);
    }

    private void RenderAbilities(int x,int y,int width)
    {
        int middle=x+width/2;
        Raylib.DrawText($"ABILITIES - {build.RemainingAbilityPoints} points left",
            x+20,y+107,18,Color.RayWhite);
        var ordered=OrderedAbilities();
        for (int i=abilityScroll;i<Math.Min(ordered.Count,abilityScroll+VisibleRows);i++)
        {
            var ability=ordered[i];
            int row=y+RowTop+(i-abilityScroll)*RowStep;
            bool available=ability.MeetsRequirements(build.Attributes,build.SelectedSkills);
            bool chosen=build.SelectedAbilities.Contains(ability.Id) ||
                knownFromTomes.Contains(ability.Id);
            Raylib.DrawRectangle(x+16,row,middle-x-24,30,
                chosen ? new Color(70,81,44,255)
                    : abilityIndex==i ? new Color(88,74,39,255) : new Color(45,50,59,255));
            if (abilityIndex==i)
                Raylib.DrawRectangleLines(x+16,row,middle-x-24,30,Color.Gold);
            Raylib.DrawText(Fit($"{(chosen ? "[x]" : "[ ]")} {ability.Name}",
                middle-x-42,16),x+24,row+7,16,available ? Color.RayWhite : Color.Gray);
        }
        if (ordered.Count>VisibleRows)
        {
            int trackHeight=VisibleRows*RowStep-4;
            int thumbHeight=Math.Max(24,trackHeight*VisibleRows/ordered.Count);
            int thumbY=y+RowTop+(trackHeight-thumbHeight)*abilityScroll/
                (ordered.Count-VisibleRows);
            Raylib.DrawRectangle(middle-15,y+RowTop,4,trackHeight,Color.DarkGray);
            Raylib.DrawRectangle(middle-15,thumbY,4,thumbHeight,Color.Gold);
        }
        DrawAction(x+16,y+408,middle-x-24,"REVIEW BUILD",
            abilityIndex==abilities.Abilities.Count,true);
        var detail=ordered[Math.Min(abilityIndex,ordered.Count-1)];
        DrawDetailsPanel(middle+8,y+124,width/2-24,322);
        int textX=middle+20,contentWidth=width/2-48;
        Raylib.DrawText(Fit(detail.Name,contentWidth,24),textX,y+140,24,Color.Gold);
        bool eligible=detail.MeetsRequirements(build.Attributes,build.SelectedSkills);
        string status=build.SelectedAbilities.Contains(detail.Id) ? "Selected"
            : knownFromTomes.Contains(detail.Id) ? "Learned from tome"
            : eligible ? "Available" : "Locked";
        Raylib.DrawText(Fit($"{detail.Path} | {status}",contentWidth,17),
            textX,y+178,17,eligible ? Color.Gold : Color.Gray);
        DrawWrapped(detail.Description,textX,y+211,contentWidth,16,3);
        Raylib.DrawText("REQUIREMENTS",textX,y+288,18,Color.Gold);
        int line=0;
        foreach (var requirement in detail.Attributes)
        {
            int current=build.Attributes.Get(requirement.Kind);
            Raylib.DrawText(Fit($"{requirement.Attribute}: {current} / {requirement.Minimum}",
                contentWidth,16),textX,y+317+line++*22,16,
                current>=requirement.Minimum ? Color.RayWhite : Color.Gray);
        }
        foreach (var id in detail.RequiredSkills)
        {
            bool learned=build.SelectedSkills.Contains(id);
            Raylib.DrawText(Fit($"Skill: {skills.Get(id).Name}",contentWidth,16),
                textX,y+317+line++*22,16,learned ? Color.RayWhite : Color.Gray);
        }
        if (detail.RequiredEquipmentSlot is { } slot)
            Raylib.DrawText(Fit($"Use with: {slot}",contentWidth,16),
                textX,y+317+line*22,16,Color.RayWhite);
        var effect=detail.Combat!;
        Raylib.DrawText(Fit($"STA {effect.StaminaCost} | Cast {effect.CastSeconds:0.00}s | Recover {effect.RecoverySeconds:0.00}s",
            contentWidth,14),textX,y+416,14,Color.Gold);
    }

    private void RenderFinish(int x,int y,int width)
    {
        int middle=x+width/2;
        int rightX=middle+20;
        int rightWidth=width/2-48;
        Raylib.DrawText("ATTRIBUTE REVIEW",x+20,y+107,18,Color.RayWhite);
        for (int i=0;i<AttributeKinds.Length;i++)
        {
            var kind=AttributeKinds[i];
            int row=y+RowTop+i*RowStep;
            Raylib.DrawRectangle(x+16,row,middle-x-24,30,new Color(45,50,59,255));
            Raylib.DrawText(kind.ToString(),x+24,row+7,16,Color.RayWhite);
            string value=build.Attributes.Get(kind).ToString();
            Raylib.DrawText(value,middle-20-Raylib.MeasureText(value,16),row+7,16,Color.Gold);
        }
        DrawAction(x+16,y+408,middle-x-24,"FINISH LEVEL UP",true,true);

        DrawDetailsPanel(middle+8,y+124,width/2-24,322);
        Raylib.DrawText("POINTS SPENT",rightX,y+140,20,Color.Gold);
        Raylib.DrawText(Fit($"Skills: {skills.SkillPoints-build.RemainingSkillPoints} / {skills.SkillPoints}",
            rightWidth,17),rightX,y+173,17,Color.RayWhite);
        Raylib.DrawText(Fit($"Abilities: {abilities.StartingAbilityPoints-build.RemainingAbilityPoints} / {abilities.StartingAbilityPoints}",
            rightWidth,17),rightX,y+197,17,Color.RayWhite);

        Raylib.DrawText("SKILLS LEARNED",rightX,y+231,18,Color.Gold);
        var chosenSkills=skills.Skills.Where(s=>build.SelectedSkills.Contains(s.Id))
            .Select(s=>s.Name).ToArray();
        DrawReviewNames(chosenSkills,rightX,y+257,rightWidth);

        Raylib.DrawText("ABILITIES LEARNED",rightX,y+326,18,Color.Gold);
        var chosenAbilities=abilities.Abilities.Where(a=>
            build.SelectedAbilities.Contains(a.Id) || knownFromTomes.Contains(a.Id))
            .Select(a=>a.Name).ToArray();
        DrawReviewNames(chosenAbilities,rightX,y+352,rightWidth);
        if (build.HasUnspentPoints)
            Raylib.DrawText(Fit("Unspent points carry into the dungeon",
                rightWidth,14),rightX,y+418,14,Color.Gold);
    }

    private static void DrawReviewNames(string[] names,int x,int y,int width)
    {
        if (names.Length==0)
        {
            Raylib.DrawText("None selected",x,y,16,Color.Gray);
            return;
        }
        for (int i=0;i<Math.Min(2,names.Length);i++)
            Raylib.DrawText(Fit(names[i],width,16),x,y+i*21,16,Color.RayWhite);
        if (names.Length>2)
            Raylib.DrawText($"+{names.Length-2} more",x,y+42,16,Color.Gray);
    }

    private static void DrawDetailsPanel(int x,int y,int width,int height)
    {
        Raylib.DrawRectangle(x,y,width,height,new Color(23,27,34,255));
        Raylib.DrawRectangleLines(x,y,width,height,Color.DarkGray);
    }

    private static void DrawAction(int x,int y,int width,string label,bool selected,bool enabled)
    {
        Raylib.DrawRectangle(x,y,width,38,enabled ? Color.Gold : Color.DarkGray);
        Raylib.DrawText(label,x+12,y+9,18,enabled ? Color.Black : Color.RayWhite);
        if (selected) Raylib.DrawRectangleLines(x,y,width,38,Color.RayWhite);
    }

    private static void DrawValueButton(string label,int x,int y,bool enabled)
    {
        Raylib.DrawRectangle(x,y,26,26,enabled ? Color.DarkGray : new Color(42,45,50,255));
        Raylib.DrawText(label,x+7,y+2,18,enabled ? Color.RayWhite : Color.Gray);
    }

    private static void DrawWrapped(string value,int x,int y,int width,int size,int maxLines)
    {
        string line="";
        int drawn=0;
        foreach (var word in value.Split(' ',StringSplitOptions.RemoveEmptyEntries))
        {
            string next=line.Length==0 ? word : line+" "+word;
            if (line.Length>0 && Raylib.MeasureText(next,size)>width)
            {
                Raylib.DrawText(line,x,y+drawn*(size+4),size,Color.RayWhite);
                if (++drawn>=maxLines) return;
                line=word;
            }
            else line=next;
        }
        if (line.Length>0 && drawn<maxLines)
            Raylib.DrawText(line,x,y+drawn*(size+4),size,Color.RayWhite);
    }

    private static string Fit(string value,int width,int size)
    {
        if (Raylib.MeasureText(value,size)<=width) return value;
        while (value.Length>0 && Raylib.MeasureText(value+"...",size)>width)
            value=value[..^1];
        return value+"...";
    }

    private static (int X,int Y,int Width) Layout()
    {
        int width=Math.Min(940,Raylib.GetScreenWidth()-24);
        return ((Raylib.GetScreenWidth()-width)/2,
            Math.Max(12,(Raylib.GetScreenHeight()-480)/2),width);
    }
}

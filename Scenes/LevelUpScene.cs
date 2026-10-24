using CCM.Application;
using CCM.Game;
using Raylib_cs;

namespace CCM.Scenes;

/// <summary>Collects a player build before the dungeon is created.</summary>
public sealed class LevelUpScene : Scene
{
    private static readonly AttributeKind[] AttributeKinds=Enum.GetValues<AttributeKind>();
    private const int VisibleRows=8;
    private const int RowTop=128;
    private const int RowStep=34;
    private readonly SessionInput input;
    private readonly PlayerDefinition startingPlayer;
    private readonly SkillCatalogue skills;
    private readonly AttributeDescriptionCatalogue descriptions;
    private readonly Action<PlayerDefinition> completed;
    private readonly Action cancelled;
    private CharacterBuild build;
    private bool skillTab;
    private int attributeIndex,skillIndex,skillScroll;

    public LevelUpScene(SessionInput input,PlayerDefinition startingPlayer,SkillCatalogue skills,
        AttributeDescriptionCatalogue descriptions,Action<PlayerDefinition> completed,Action cancelled)
    {
        this.input=input;
        this.startingPlayer=startingPlayer;
        this.skills=skills;
        this.descriptions=descriptions;
        this.completed=completed;
        this.cancelled=cancelled;
        build=new CharacterBuild(startingPlayer.Attributes,skills);
    }

    public override void Enter()
    {
        build=new CharacterBuild(startingPlayer.Attributes,skills);
        skillTab=false;
        attributeIndex=skillIndex=skillScroll=0;
    }

    public override void Update(float seconds)
    {
        if (!Raylib.IsWindowFocused()) return;
        if (input.ReturnPressed() || input.PausePressed()) { cancelled(); return; }
        if (input.UsesMouse)
        {
            if (input.SelectPressed() && HandleClick(false)) return;
            if (skillTab && input.AttackPressed() && HandleClick(true)) return;
        }
        if (input.UiPanePressed()) skillTab=!skillTab;
        if (input.PanelLeftPressed()) skillTab=false;
        if (input.PanelRightPressed()) skillTab=true;
        if (skillTab && input.UsesMouse && input.Zoom!=0) ScrollSkills();

        if (input.UiUpPressed())
        {
            if (skillTab)
            {
                skillIndex=(skillIndex+skills.Skills.Count)%(skills.Skills.Count+1);
                KeepSkillVisible();
            }
            else attributeIndex=(attributeIndex+AttributeKinds.Length)%(AttributeKinds.Length+1);
        }
        if (input.UiDownPressed())
        {
            if (skillTab)
            {
                skillIndex=(skillIndex+1)%(skills.Skills.Count+1);
                KeepSkillVisible();
            }
            else attributeIndex=(attributeIndex+1)%(AttributeKinds.Length+1);
        }

        if (skillTab)
        {
            if (!input.UiConfirmPressed()) return;
            if (skillIndex==skills.Skills.Count) TryComplete();
            else build.ToggleSkill(OrderedSkills()[skillIndex].Id);
        }
        else if (attributeIndex==AttributeKinds.Length)
        {
            if (input.UiConfirmPressed()) skillTab=true;
        }
        else
        {
            var kind=AttributeKinds[attributeIndex];
            int value=build.Attributes.Get(kind);
            if (input.UiLeftPressed()) ChangeAttribute(kind,value-1);
            if (input.UiRightPressed() || input.UiConfirmPressed())
                ChangeAttribute(kind,value+1);
        }
    }

    private void ScrollSkills()
    {
        var pointer=Raylib.GetMousePosition();
        var (x,y,width)=Layout();
        if (pointer.X<x+16 || pointer.X>=x+width/2 ||
            pointer.Y<y+RowTop || pointer.Y>=y+RowTop+VisibleRows*RowStep) return;
        skillScroll=Math.Clamp(skillScroll-Math.Sign(input.Zoom),
            0,Math.Max(0,skills.Skills.Count-VisibleRows));
        skillIndex=Math.Clamp(skillIndex,skillScroll,
            Math.Min(skillScroll+VisibleRows-1,skills.Skills.Count-1));
    }

    private bool HandleClick(bool activateSkill)
    {
        var pointer=Raylib.GetMousePosition();
        var (x,y,width)=Layout();
        int middle=x+width/2;
        if (!activateSkill && pointer.Y>=y+64 && pointer.Y<y+98)
        {
            if (pointer.X>=x+20 && pointer.X<x+180) { skillTab=false; return true; }
            if (pointer.X>=x+188 && pointer.X<x+348) { skillTab=true; return true; }
        }
        if (pointer.X>=x+16 && pointer.X<middle-8)
        {
            if (!skillTab && !activateSkill)
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
            else if (skillTab)
            {
                var ordered=OrderedSkills();
                for (int i=skillScroll;i<Math.Min(ordered.Count,skillScroll+VisibleRows);i++)
                {
                    int row=y+RowTop+(i-skillScroll)*RowStep;
                    if (pointer.Y<row || pointer.Y>=row+30) continue;
                    skillIndex=i;
                    if (activateSkill) build.ToggleSkill(ordered[i].Id);
                    return true;
                }
            }
            if (!activateSkill && pointer.Y>=y+408 && pointer.Y<y+446)
            {
                if (skillTab)
                {
                    skillIndex=skills.Skills.Count;
                    TryComplete();
                }
                else
                {
                    attributeIndex=AttributeKinds.Length;
                    skillTab=true;
                }
                return true;
            }
        }
        return false;
    }

    private IReadOnlyList<SkillDefinition> OrderedSkills() => skills.OrderedFor(build.Attributes);

    private void ChangeAttribute(AttributeKind kind,int value)
    {
        string? selectedId=skillIndex<skills.Skills.Count ? OrderedSkills()[skillIndex].Id : null;
        if (!build.SetAttribute(kind,value)) return;
        if (selectedId is not null)
        {
            var ordered=OrderedSkills();
            for (int i=0;i<ordered.Count;i++)
                if (ordered[i].Id==selectedId) { skillIndex=i; break; }
        }
        KeepSkillVisible();
    }

    private void KeepSkillVisible()
    {
        if (skillIndex>=skills.Skills.Count) return;
        if (skillIndex<skillScroll) skillScroll=skillIndex;
        else if (skillIndex>=skillScroll+VisibleRows)
            skillScroll=skillIndex-VisibleRows+1;
    }

    private void TryComplete()
    {
        if (build.CanComplete) completed(build.Complete());
    }

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
        DrawTab("ATTRIBUTES",x+20,y+64,!skillTab);
        DrawTab("SKILLS",x+188,y+64,skillTab);
        Raylib.DrawRectangle(middle,y+112,1,334,Color.DarkGray);
        if (skillTab) RenderSkills(x,y,width);
        else RenderAttributes(x,y,width);
    }

    private static void DrawTab(string label,int x,int y,bool selected)
    {
        Raylib.DrawRectangle(x,y,160,34,selected ? Color.Gold : Color.DarkGray);
        Raylib.DrawText(label,x+12,y+7,20,selected ? Color.Black : Color.RayWhite);
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
            bool available=skill.IsAvailable(build.Attributes);
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
        DrawAction(x+16,y+408,middle-x-24,
            build.CanComplete ? "ENTER DUNGEON" : $"CHOOSE {skills.SkillPoints} SKILLS",
            skillIndex==skills.Skills.Count,build.CanComplete);
        var skillDetail=ordered[Math.Min(skillIndex,ordered.Count-1)];
        DrawDetailsPanel(middle+8,y+124,width/2-24,322);
        int textX=middle+20,contentWidth=width/2-48;
        Raylib.DrawText(Fit(skillDetail.Name,contentWidth,24),
            textX,y+140,24,Color.Gold);
        bool eligible=skillDetail.IsAvailable(build.Attributes);
        string status=build.SelectedSkills.Contains(skillDetail.Id) ? "Selected"
            : eligible ? "Available" : "Locked";
        Raylib.DrawText(status,textX,y+178,17,eligible ? Color.Gold : Color.Gray);
        DrawWrapped(skillDetail.Description,textX,y+211,contentWidth,16,4);
        Raylib.DrawText("REQUIREMENTS",textX,y+288,18,Color.Gold);
        for (int i=0;i<Math.Min(SkillDefinition.MaxRequirements,skillDetail.Requirements.Count);i++)
        {
            var requirement=skillDetail.Requirements[i];
            int current=build.Attributes.Get(requirement.Kind);
            string line=$"{requirement.Attribute}: {current} / {requirement.Minimum}";
            Raylib.DrawText(Fit(line,contentWidth,16),textX,y+317+i*22,16,
                current>=requirement.Minimum ? Color.RayWhite : Color.Gray);
        }
        Raylib.DrawText("Effect not implemented yet",textX,y+416,14,Color.Gray);
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

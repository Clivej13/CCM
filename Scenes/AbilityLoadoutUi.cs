using System.Numerics;
using CCM.Application;
using CCM.Game;
using Raylib_cs;

namespace CCM.Scenes;

/// <summary>Displays one player loadout as a keyboard bar or controller radial selector.</summary>
internal sealed class AbilityLoadoutUi(SessionInput input,AbilityCatalogue? catalogue)
{
    private bool wheelOpen;
    private int wheelSlot;
    private int selectedSlot=-1;
    public bool WheelOpen => wheelOpen;
    public bool PointerConsumed { get; private set; }
    public bool WheelSelectionCommitted { get; private set; }

    public void Reset()
    {
        wheelOpen=false;
        PointerConsumed=false;
        WheelSelectionCommitted=false;
        selectedSlot=-1;
        wheelSlot=0;
    }

    public string? Update(PlayerEntity player)
    {
        PointerConsumed=false;
        WheelSelectionCommitted=false;
        if (input.UsesMouse)
        {
            wheelOpen=false;
            int slot=input.AbilitySlotPressed();
            if (slot>=0) return Select(player,slot);
            if (input.SelectPressed())
            {
                var pointer=Raylib.GetMousePosition();
                var (x,y,width,gap)=BarLayout();
                int offset=(int)pointer.X-x;
                if (offset>=0 && pointer.Y>=y && pointer.Y<y+44 &&
                    offset<(width+gap)*AbilityLoadout.SlotCount-gap &&
                    offset%(width+gap)<width)
                {
                    PointerConsumed=true;
                    return Select(player,offset/(width+gap));
                }
            }
            return null;
        }
        if (input.ControllerDisconnected)
        {
            wheelOpen=false;
            return null;
        }
        if (input.AbilityWheelHeld)
        {
            if (!wheelOpen)
            {
                wheelOpen=true;
                wheelSlot=selectedSlot is >=0 and <AbilityLoadout.WheelSlotCount
                    ? selectedSlot : 0;
            }
            Vector2 direction=input.Look;
            if (direction.LengthSquared()>.25f)
            {
                float angle=MathF.Atan2(direction.X,-direction.Y);
                if (angle<0) angle+=MathF.Tau;
                wheelSlot=(int)MathF.Round(angle/MathF.Tau*AbilityLoadout.WheelSlotCount)
                    %AbilityLoadout.WheelSlotCount;
            }
            return null;
        }
        if (!wheelOpen) return null;
        wheelOpen=false;
        WheelSelectionCommitted=true;
        return Select(player,wheelSlot);
    }

    private string Select(PlayerEntity player,int slot)
    {
        selectedSlot=slot;
        string? id=player.Abilities.Loadout.In(slot);
        if (id is null) return $"Slot {AbilityLoadout.KeyLabel(slot)} is empty";
        if (catalogue is null) return "Ability catalogue unavailable";
        var ability=catalogue.Get(id);
        return player.Abilities.CanUse(ability,player)
            ? $"{ability.Name} selected - effect not implemented"
            : $"{ability.Name} is unavailable with current requirements";
    }

    public void Render(PlayerEntity player)
    {
        if (catalogue is null) return;
        if (input.UsesMouse) RenderBar(player);
        else if (wheelOpen) RenderWheel(player);
    }

    private static (int X,int Y,int Width,int Gap) BarLayout()
    {
        int gap=3;
        int width=Math.Clamp((Raylib.GetScreenWidth()-24)/AbilityLoadout.SlotCount-gap,34,76);
        int total=(width+gap)*AbilityLoadout.SlotCount-gap;
        return ((Raylib.GetScreenWidth()-total)/2,Raylib.GetScreenHeight()-58,width,gap);
    }

    private void RenderBar(PlayerEntity player)
    {
        var (x,y,slotWidth,gap)=BarLayout();
        int total=(slotWidth+gap)*AbilityLoadout.SlotCount-gap;
        Raylib.DrawRectangle(x-8,y-8,total+16,60,new Color(0,0,0,190));
        for (int i=0;i<AbilityLoadout.SlotCount;i++)
        {
            int left=x+i*(slotWidth+gap);
            DrawSlot(player,i,left,y,slotWidth,44);
        }
    }

    private void RenderWheel(PlayerEntity player)
    {
        int cx=Raylib.GetScreenWidth()/2,cy=Raylib.GetScreenHeight()/2;
        Raylib.DrawCircle(cx,cy,192,new Color(0,0,0,190));
        Raylib.DrawCircleLines(cx,cy,192,Color.Gold);
        Raylib.DrawText("ABILITIES",cx-58,cy-12,20,Color.Gold);
        for (int i=0;i<AbilityLoadout.WheelSlotCount;i++)
        {
            float angle=i*MathF.Tau/AbilityLoadout.WheelSlotCount-MathF.PI/2;
            int x=cx+(int)(MathF.Cos(angle)*139)-49;
            int y=cy+(int)(MathF.Sin(angle)*139)-26;
            DrawSlot(player,i,x,y,98,52,i==wheelSlot);
        }
    }

    private void DrawSlot(PlayerEntity player,int index,int x,int y,int width,int height,
        bool wheelFocused=false)
    {
        string? id=player.Abilities.Loadout.In(index);
        bool focused=wheelFocused || (!wheelOpen && index==selectedSlot);
        Color background=focused ? new Color(102,82,38,255) : new Color(39,45,54,255);
        Raylib.DrawRectangle(x,y,width,height,background);
        Raylib.DrawRectangleLines(x,y,width,height,focused ? Color.Gold : Color.DarkGray);
        Raylib.DrawText(AbilityLoadout.KeyLabel(index),x+5,y+3,15,Color.Gold);
        if (id is null) return;
        var ability=catalogue!.Get(id);
        bool usable=player.Abilities.CanUse(ability,player);
        string label=ability.Name;
        while (label.Length>1 && Raylib.MeasureText(label,12)>width-8)
            label=label[..^1];
        Raylib.DrawText(label,x+4,y+height-17,12,usable ? Color.RayWhite : Color.Gray);
    }
}

using CCM.Application;
using CCM.Game;
using Raylib_cs;

namespace CCM.Scenes;

/// <summary>One modal panel for looting and equipping; it never owns item definitions.</summary>
internal sealed class InventoryPanel(ItemCatalogue catalogue)
{
    private enum PanelMode { Closed, Chest, Inventory }
    private PanelMode mode;
    private ChestInteractable? chest;
    private int selected;
    private string? notice;
    public bool IsOpen => mode!=PanelMode.Closed;

    public void OpenChest(ChestInteractable value)
    {
        chest=value;
        mode=PanelMode.Chest;
        selected=0;
        notice=null;
    }
    public void OpenInventory()
    {
        chest=null;
        mode=PanelMode.Inventory;
        selected=0;
        notice=null;
    }
    public void Close()
    {
        mode=PanelMode.Closed;
        chest=null;
        notice=null;
    }

    private IReadOnlyList<ItemStack> Entries(PlayerEntity player) =>
        mode==PanelMode.Chest ? chest!.Contents.Stacks : player.Inventory.Stacks;

    public void Update(SessionInput input,PlayerEntity player)
    {
        var entries=Entries(player);
        if (entries.Count==0) { selected=0; return; }
        selected=Math.Clamp(selected,0,entries.Count-1);
        if (input.UiUpPressed()) selected=(selected+entries.Count-1)%entries.Count;
        if (input.UiDownPressed()) selected=(selected+1)%entries.Count;
        bool confirm=input.UiConfirmPressed();
        if (input.UsesMouse && input.SelectPressed())
        {
            var pointer=Raylib.GetMousePosition();
            int index=FirstVisible(entries.Count)+(int)((pointer.Y-RowTop())/32);
            if (pointer.X>=PanelLeft() && pointer.X<PanelLeft()+PanelWidth() &&
                pointer.Y>=RowTop() && pointer.Y<RowTop()+VisibleRows()*32 &&
                index>=0 && index<entries.Count)
            {
                selected=index;
                confirm=true;
            }
        }
        if (!confirm) return;
        var stack=entries[selected];
        var definition=catalogue.Get(stack.ItemId);
        if (mode==PanelMode.Chest)
        {
            if (chest!.Contents.TransferTo(player.Inventory,stack.ItemId,stack.Quantity))
                notice=$"Took {definition.Name}" + (stack.Quantity>1 ? $" x{stack.Quantity}" : "");
        }
        else
        {
            notice=player.Equipment.Equip(stack.ItemId)
                ? $"Equipped {definition.Name}"
                : $"{definition.Name} cannot be equipped";
        }
        selected=Math.Max(0,Math.Min(selected,Entries(player).Count-1));
    }

    public void Render(PlayerEntity player)
    {
        if (!IsOpen) return;
        int x=PanelLeft(),y=PanelTop(),width=PanelWidth(),height=PanelHeight();
        Raylib.DrawRectangle(0,0,Raylib.GetScreenWidth(),Raylib.GetScreenHeight(),
            new Color(0,0,0,175));
        Raylib.DrawRectangle(x,y,width,height,new Color(30,34,42,255));
        Raylib.DrawRectangleLines(x,y,width,height,Color.Gold);
        Raylib.DrawText(mode==PanelMode.Chest ? "CHEST" : "INVENTORY",
            x+24,y+18,28,Color.RayWhite);
        Raylib.DrawText(mode==PanelMode.Chest
            ? "Enter / A / click: take selected stack"
            : "Enter / A / click: equip selected item",
            x+24,y+58,18,Color.RayWhite);
        var entries=Entries(player);
        int first=FirstVisible(entries.Count);
        if (entries.Count==0)
            Raylib.DrawText(mode==PanelMode.Chest ? "The chest is empty." : "Inventory is empty.",
                x+24,RowTop(),20,Color.RayWhite);
        for (int i=first;i<Math.Min(entries.Count,first+VisibleRows());i++)
        {
            int rowY=RowTop()+(i-first)*32;
            if (i==selected) Raylib.DrawRectangle(x+14,rowY-3,width-28,30,
                new Color(90,75,40,255));
            var stack=entries[i];
            var item=catalogue.Get(stack.ItemId);
            string equipped=mode==PanelMode.Inventory &&
                player.Equipment.In(item.EquipmentSlot)==item.Id ? " [equipped]" : "";
            if (item.EquipmentSlot==EquipmentSlot.None) equipped="";
            Raylib.DrawText($"{item.Name} x{stack.Quantity}{equipped}",
                x+24,rowY,18,i==selected ? Color.Gold : Color.RayWhite);
        }
        int foot=y+height-100;
        string weapon=NameOrDash(player.Equipment.In(EquipmentSlot.Weapon));
        string body=NameOrDash(player.Equipment.In(EquipmentSlot.Body));
        string quiver=NameOrDash(player.Equipment.In(EquipmentSlot.Quiver));
        Raylib.DrawText($"Weapon: {weapon}   Armour: {body}",x+24,foot,16,Color.RayWhite);
        Raylib.DrawText($"Quiver: {quiver}   Arrows: {player.Inventory.CountVisual(catalogue,ItemVisual.Arrow)}",
            x+24,foot+24,16,Color.RayWhite);
        if (notice is not null) Raylib.DrawText(notice,x+24,foot-26,18,Color.Gold);
        Raylib.DrawText("Up/Down, D-pad or left stick: select    I / Y / Esc / B: close",
            x+24,y+height-26,16,Color.RayWhite);
    }

    private string NameOrDash(string? id) => id is null ? "-" : catalogue.Get(id).Name;
    private static int PanelWidth() => Math.Min(640,Raylib.GetScreenWidth()-32);
    private static int PanelHeight() => Math.Min(480,Raylib.GetScreenHeight()-32);
    private static int PanelLeft() => (Raylib.GetScreenWidth()-PanelWidth())/2;
    private static int PanelTop() => (Raylib.GetScreenHeight()-PanelHeight())/2;
    private static int RowTop() => PanelTop()+100;
    private static int VisibleRows() => Math.Max(1,(PanelHeight()-225)/32);
    private int FirstVisible(int count) =>
        Math.Clamp(selected-VisibleRows()+1,0,Math.Max(0,count-VisibleRows()));
}

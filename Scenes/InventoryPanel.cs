using CCM.Application;
using CCM.Game;
using Raylib_cs;

namespace CCM.Scenes;

/// <summary>Looting and equipment share a modal panel; item data remains catalogue-owned.</summary>
internal sealed class InventoryPanel(ItemCatalogue catalogue,SkillCatalogue? skills=null,
    AbilityCatalogue? abilities=null)
{
    private static readonly EquipmentSlot[] Slots=
        [EquipmentSlot.Weapon,EquipmentSlot.Body,EquipmentSlot.Quiver];
    private static readonly ItemFilter[] Filters=
        [ItemFilter.All,ItemFilter.Weapons,ItemFilter.Apparel,ItemFilter.Tomes];
    private const int SectionTabWidth=150;
    private const int SectionTabGap=8;
    private enum PanelMode { Closed, Chest, Inventory, Character, Abilities }
    private PanelMode mode;
    private ChestInteractable? chest;
    private int selected;
    private int bagScroll;
    private int selectedSlot;
    private int abilityIndex,abilityScroll,abilitySlot;
    private bool equipmentFocused;
    private ItemFilter filter=ItemFilter.All;
    private string? notice;
    public bool IsOpen => mode!=PanelMode.Closed;
    public bool IsChest => mode==PanelMode.Chest;

    public bool CloseClicked(SessionInput input)
    {
        if (!IsOpen || !input.UsesMouse || !input.SelectPressed()) return false;
        var pointer=Raylib.GetMousePosition();
        int left=PanelLeft()+PanelWidth()-46,top=PanelTop()+14;
        return pointer.X>=left && pointer.X<left+32 &&
            pointer.Y>=top && pointer.Y<top+32;
    }

    public bool SelectTabClicked(SessionInput input)
    {
        if (mode is PanelMode.Closed or PanelMode.Chest ||
            !input.UsesMouse || !input.SelectPressed()) return false;
        var pointer=Raylib.GetMousePosition();
        int left=PanelLeft()+16,top=PanelTop()+12;
        if (pointer.Y<top || pointer.Y>=top+36) return false;
        if (pointer.X>=left && pointer.X<left+SectionTabWidth)
        {
            OpenInventory();
            return true;
        }
        int characterLeft=left+SectionTabWidth+SectionTabGap;
        if (pointer.X>=characterLeft && pointer.X<characterLeft+SectionTabWidth)
        {
            OpenCharacter();
            return true;
        }
        int abilityLeft=characterLeft+SectionTabWidth+SectionTabGap;
        if (pointer.X>=abilityLeft && pointer.X<abilityLeft+SectionTabWidth)
        {
            OpenAbilities();
            return true;
        }
        return false;
    }

    public void OpenChest(ChestInteractable value)
    {
        chest=value;
        mode=PanelMode.Chest;
        selected=0;
        notice=null;
    }
    public void OpenInventory()
    {
        if (mode==PanelMode.Inventory) return;
        bool returningFromCharacter=mode is PanelMode.Character or PanelMode.Abilities;
        chest=null;
        mode=PanelMode.Inventory;
        if (!returningFromCharacter)
        {
            selected=selectedSlot=bagScroll=0;
            equipmentFocused=false;
            filter=ItemFilter.All;
        }
        notice=null;
    }
    public void OpenCharacter()
    {
        if (mode==PanelMode.Character) return;
        chest=null;
        mode=PanelMode.Character;
        notice=null;
    }
    public void OpenAbilities()
    {
        if (mode==PanelMode.Abilities) return;
        chest=null;
        mode=PanelMode.Abilities;
        abilityIndex=abilityScroll=0;
        notice=null;
    }
    public void CycleTab(int direction)
    {
        if (mode is PanelMode.Closed or PanelMode.Chest) return;
        PanelMode[] tabs=[PanelMode.Inventory,PanelMode.Character,PanelMode.Abilities];
        int index=Array.IndexOf(tabs,mode);
        switch (tabs[(index+direction+tabs.Length)%tabs.Length])
        {
            case PanelMode.Inventory: OpenInventory(); break;
            case PanelMode.Character: OpenCharacter(); break;
            case PanelMode.Abilities: OpenAbilities(); break;
        }
    }
    public void Close()
    {
        mode=PanelMode.Closed;
        chest=null;
        notice=null;
    }

    public void Update(SessionInput input,PlayerEntity player)
    {
        if (mode==PanelMode.Chest) UpdateChest(input,player);
        else if (mode==PanelMode.Inventory) UpdateInventory(input,player);
        else if (mode==PanelMode.Abilities) UpdateAbilities(input,player);
    }

    private void UpdateAbilities(SessionInput input,PlayerEntity player)
    {
        if (abilities is null || abilities.Abilities.Count==0) return;
        int count=abilities.Abilities.Count,rows=InventoryRows();
        int slotCount=input.UsesMouse ? AbilityLoadout.SlotCount : AbilityLoadout.WheelSlotCount;
        if (input.UiUpPressed()) abilityIndex=(abilityIndex+count-1)%count;
        if (input.UiDownPressed()) abilityIndex=(abilityIndex+1)%count;
        if (input.UiLeftPressed()) abilitySlot=(abilitySlot+slotCount-1)%slotCount;
        if (input.UiRightPressed()) abilitySlot=(abilitySlot+1)%slotCount;
        abilityScroll=Math.Clamp(abilityScroll,0,Math.Max(0,count-rows));
        if (abilityIndex<abilityScroll) abilityScroll=abilityIndex;
        if (abilityIndex>=abilityScroll+rows) abilityScroll=abilityIndex-rows+1;
        int keySlot=input.AbilitySlotPressed();
        if (keySlot>=0)
        {
            abilitySlot=keySlot;
            AssignSelectedAbility(player);
        }
        if (input.UiPanePressed())
        {
            player.Abilities.Loadout.Clear(abilitySlot);
            notice=$"Cleared slot {AbilityLoadout.KeyLabel(abilitySlot)}";
        }
        if (input.UiConfirmPressed()) AssignSelectedAbility(player);
        if (!input.UsesMouse) return;
        var pointer=Raylib.GetMousePosition();
        int left=PanelLeft(),divider=left+PanelWidth()/2;
        if (pointer.X>=left+14 && pointer.X<divider && input.Zoom!=0)
            abilityScroll=Math.Clamp(abilityScroll-Math.Sign(input.Zoom),0,Math.Max(0,count-rows));
        if (input.SelectPressed() &&
            RowAt(pointer,left+14,divider-left-36,RowTop(),count,abilityScroll,rows)
                is { } index)
            abilityIndex=index;
        int slotWidth=(PanelWidth()-28)/AbilityLoadout.SlotCount;
        int slotY=PanelTop()+PanelHeight()-74;
        if (pointer.X>=left+14 && pointer.X<left+14+slotWidth*AbilityLoadout.SlotCount &&
            pointer.Y>=slotY && pointer.Y<slotY+48)
        {
            int slot=(int)(pointer.X-left-14)/slotWidth;
            if (input.SelectPressed())
            {
                abilitySlot=slot;
                AssignSelectedAbility(player);
            }
            else if (input.AttackPressed())
            {
                abilitySlot=slot;
                player.Abilities.Loadout.Clear(slot);
                notice=$"Cleared slot {AbilityLoadout.KeyLabel(slot)}";
            }
        }
    }

    private void AssignSelectedAbility(PlayerEntity player)
    {
        if (abilities is null) return;
        var ability=abilities.Abilities[abilityIndex];
        if (!player.Abilities.Knows(ability.Id))
        {
            notice="Learn this ability before assigning it";
            return;
        }
        if (player.Abilities.Loadout.In(abilitySlot)==ability.Id)
        {
            player.Abilities.Loadout.Clear(abilitySlot);
            notice=$"Cleared slot {AbilityLoadout.KeyLabel(abilitySlot)}";
        }
        else
        {
            player.Abilities.Loadout.Assign(abilitySlot,ability.Id,player.Abilities);
            notice=$"Assigned {ability.Name} to {AbilityLoadout.KeyLabel(abilitySlot)}";
        }
    }

    private void UpdateChest(SessionInput input,PlayerEntity player)
    {
        var entries=chest!.Contents.Stacks;
        selected=ClampSelection(selected,entries.Count);
        if (entries.Count==0) return;
        if (input.UiUpPressed()) selected=(selected+entries.Count-1)%entries.Count;
        if (input.UiDownPressed()) selected=(selected+1)%entries.Count;
        bool confirm=input.UiConfirmPressed();
        if (input.UsesMouse && input.SelectPressed())
        {
            var pointer=Raylib.GetMousePosition();
            if (RowAt(pointer,PanelLeft()+14,PanelWidth()-28,RowTop(),entries.Count,
                FirstVisible(selected,entries.Count,ChestRows()),ChestRows()) is { } index)
            {
                selected=index;
                confirm=true;
            }
        }
        if (!confirm) return;
        var stack=entries[selected];
        if (chest.Contents.TransferTo(player.Inventory,stack.ItemId,stack.Quantity))
        {
            var name=catalogue.Get(stack.ItemId).Name;
            notice=$"Took {name}" + (stack.Quantity>1 ? $" x{stack.Quantity}" : "");
        }
        selected=ClampSelection(selected,entries.Count);
    }

    private void UpdateInventory(SessionInput input,PlayerEntity player)
    {
        if (input.UiLeftPressed()) ChangeFilter(-1);
        if (input.UiRightPressed()) ChangeFilter(1);
        if (input.UiPanePressed()) equipmentFocused=!equipmentFocused;
        var entries=player.Inventory.Filter(catalogue,filter);
        selected=ClampSelection(selected,entries.Count);
        bagScroll=Math.Clamp(bagScroll,0,Math.Max(0,entries.Count-InventoryRows()));
        if (input.UiUpPressed())
        {
            if (equipmentFocused) selectedSlot=(selectedSlot+Slots.Length-1)%Slots.Length;
            else if (entries.Count>0)
            {
                selected=(selected+entries.Count-1)%entries.Count;
                KeepSelectedVisible(entries.Count);
            }
        }
        if (input.UiDownPressed())
        {
            if (equipmentFocused) selectedSlot=(selectedSlot+1)%Slots.Length;
            else if (entries.Count>0)
            {
                selected=(selected+1)%entries.Count;
                KeepSelectedVisible(entries.Count);
            }
        }

        int x=PanelLeft(),y=PanelTop(),width=PanelWidth(),divider=x+width/2;
        if (input.UsesMouse)
        {
            var pointer=Raylib.GetMousePosition();
            int rows=InventoryRows();
            if (pointer.X>=x+14 && pointer.X<divider &&
                pointer.Y>=RowTop()-3 && pointer.Y<RowTop()+rows*32 &&
                float.IsFinite(input.Zoom) && input.Zoom!=0)
            {
                int steps=Math.Max(1,(int)MathF.Ceiling(MathF.Abs(input.Zoom)*3));
                bagScroll=Math.Clamp(bagScroll-Math.Sign(input.Zoom)*steps,
                    0,Math.Max(0,entries.Count-rows));
            }
            if (input.SelectPressed())
            {
                if (pointer.Y>=y+62 && pointer.Y<y+94)
                {
                    int filterWidth=FilterWidth();
                    for (int i=0;i<Filters.Length;i++)
                        if (pointer.X>=x+18+i*filterWidth &&
                            pointer.X<x+18+(i+1)*filterWidth)
                        {
                            SetFilter(Filters[i]);
                            return;
                        }
                }
                if (entries.Count>rows && pointer.X>=divider-20 && pointer.X<divider-8 &&
                    pointer.Y>=RowTop()-3 && pointer.Y<RowTop()-3+rows*32)
                {
                    float portion=(pointer.Y-(RowTop()-3))/(rows*32f);
                    bagScroll=Math.Clamp((int)MathF.Round(portion*(entries.Count-rows)),
                        0,entries.Count-rows);
                    return;
                }
                if (RowAt(pointer,x+14,divider-x-36,RowTop(),
                    entries.Count,bagScroll,rows) is { } bagIndex)
                {
                    equipmentFocused=false;
                    selected=bagIndex;
                    notice=null;
                    return;
                }
                if (RowAt(pointer,divider+8,x+width-divider-22,RowTop(),
                    Slots.Length,0,Slots.Length,36) is { } slotIndex)
                {
                    equipmentFocused=true;
                    selectedSlot=slotIndex;
                    notice=null;
                    return;
                }
            }
            if (input.AttackPressed() &&
                RowAt(pointer,x+14,divider-x-36,RowTop(),
                    entries.Count,bagScroll,rows) is { } equipIndex)
            {
                equipmentFocused=false;
                selected=equipIndex;
                EquipSelected(player,entries);
                return;
            }
        }
        if (!input.UiConfirmPressed()) return;
        if (equipmentFocused)
        {
            var slot=Slots[selectedSlot];
            var id=player.Equipment.In(slot);
            if (id is not null && player.Equipment.Unequip(slot))
                notice=$"Unequipped {catalogue.Get(id).Name}";
        }
        else if (entries.Count>0) EquipSelected(player,entries);
        selected=ClampSelection(selected,player.Inventory.Filter(catalogue,filter).Count);
        bagScroll=Math.Clamp(bagScroll,0,
            Math.Max(0,player.Inventory.Filter(catalogue,filter).Count-InventoryRows()));
    }

    private void EquipSelected(PlayerEntity player,IReadOnlyList<ItemStack> entries)
    {
        var item=catalogue.Get(entries[selected].ItemId);
        if (item.ItemType==ItemType.Tome)
        {
            if (abilities is null || item.AbilityId is null)
                notice="Ability catalogue unavailable";
            else if (player.Abilities.Knows(item.AbilityId))
                notice="Ability already learned";
            else if (!abilities.Get(item.AbilityId)
                .MeetsRequirements(player.Attributes,player.SkillIds))
                notice="Ability requirements not met";
            else
                notice=player.Abilities.TryLearnFromTome(item.Id,player.Inventory,catalogue,
                    abilities,player.Attributes,player.SkillIds)
                    ? $"Learned {abilities.Get(item.AbilityId).Name}"
                    : "Could not read tome";
        }
        else notice=item.EquipmentSlot==EquipmentSlot.None
            ? $"{item.Name} cannot be equipped"
            : player.Equipment.Equip(item.Id)
                ? $"Equipped {item.Name}"
                : $"{item.Name} cannot be equipped";
        selected=ClampSelection(selected,player.Inventory.Filter(catalogue,filter).Count);
        bagScroll=Math.Clamp(bagScroll,0,
            Math.Max(0,player.Inventory.Filter(catalogue,filter).Count-InventoryRows()));
    }

    private void KeepSelectedVisible(int count)
    {
        int rows=InventoryRows();
        bagScroll=Math.Clamp(bagScroll,0,Math.Max(0,count-rows));
        if (selected<bagScroll) bagScroll=selected;
        else if (selected>=bagScroll+rows) bagScroll=selected-rows+1;
    }

    private void ChangeFilter(int direction)
    {
        int current=Array.IndexOf(Filters,filter);
        SetFilter(Filters[(current+direction+Filters.Length)%Filters.Length]);
    }
    private void SetFilter(ItemFilter next)
    {
        filter=next;
        selected=bagScroll=0;
        notice=null;
    }
    private static int ClampSelection(int index,int count) => Math.Clamp(index,0,Math.Max(0,count-1));

    public void Render(PlayerEntity player,bool usesMouse)
    {
        if (!IsOpen) return;
        int x=PanelLeft(),y=PanelTop(),width=PanelWidth(),height=PanelHeight();
        Raylib.DrawRectangle(0,0,Raylib.GetScreenWidth(),Raylib.GetScreenHeight(),
            new Color(0,0,0,175));
        Raylib.DrawRectangle(x,y,width,height,new Color(30,34,42,255));
        Raylib.DrawRectangleLines(x,y,width,height,Color.Gold);
        if (mode==PanelMode.Chest) RenderChest();
        else if (mode==PanelMode.Inventory) RenderInventory(player);
        else if (mode==PanelMode.Character) RenderCharacter(player);
        else RenderAbilities(player,usesMouse);
        if (mode is PanelMode.Inventory or PanelMode.Character or PanelMode.Abilities)
            RenderSectionTabs(usesMouse);
        if (usesMouse)
        {
            int left=x+width-46,top=y+14;
            var pointer=Raylib.GetMousePosition();
            bool hovered=pointer.X>=left && pointer.X<left+32 &&
                pointer.Y>=top && pointer.Y<top+32;
            Raylib.DrawRectangle(left,top,32,32,hovered ? Color.Gold : Color.DarkGray);
            Raylib.DrawText("X",left+9,top+5,22,hovered ? Color.Black : Color.RayWhite);
        }
    }

    private void RenderSectionTabs(bool usesMouse)
    {
        int left=PanelLeft()+16,top=PanelTop()+12;
        var pointer=usesMouse ? Raylib.GetMousePosition() : default;
        string[] labels=["INVENTORY","CHARACTER","ABILITIES"];
        for (int i=0;i<labels.Length;i++)
        {
            int tabX=left+i*(SectionTabWidth+SectionTabGap);
            bool active=mode==(PanelMode)((int)PanelMode.Inventory+i);
            bool hovered=usesMouse && pointer.X>=tabX && pointer.X<tabX+SectionTabWidth &&
                pointer.Y>=top && pointer.Y<top+36;
            Color background=active ? Color.Gold
                : hovered ? new Color(75,81,94,255) : Color.DarkGray;
            Raylib.DrawRectangle(tabX,top,SectionTabWidth,36,background);
            Raylib.DrawText(labels[i],
                tabX+(SectionTabWidth-Raylib.MeasureText(labels[i],20))/2,top+8,20,
                active ? Color.Black : Color.RayWhite);
        }
    }

    private void RenderChest()
    {
        int x=PanelLeft(),y=PanelTop(),width=PanelWidth(),height=PanelHeight();
        Raylib.DrawText("CHEST",x+24,y+18,28,Color.RayWhite);
        var entries=chest!.Contents.Stacks;
        int first=FirstVisible(selected,entries.Count,ChestRows());
        if (entries.Count==0) Raylib.DrawText("The chest is empty.",x+24,RowTop(),20,Color.RayWhite);
        for (int i=first;i<Math.Min(entries.Count,first+ChestRows());i++)
        {
            int rowY=RowTop()+(i-first)*32;
            if (i==selected) Raylib.DrawRectangle(x+14,rowY-3,width-28,30,new Color(90,75,40,255));
            var stack=entries[i];
            Raylib.DrawText($"{catalogue.Get(stack.ItemId).Name} x{stack.Quantity}",
                x+24,rowY,18,i==selected ? Color.Gold : Color.RayWhite);
        }
        if (notice is not null) Raylib.DrawText(notice,x+24,y+height-66,18,Color.Gold);
    }

    private void RenderInventory(PlayerEntity player)
    {
        int x=PanelLeft(),y=PanelTop(),width=PanelWidth(),height=PanelHeight();
        int divider=x+width/2;
        int rightX=divider+16;
        int rightWidth=x+width-24-rightX;
        Raylib.DrawRectangle(divider,y+14,1,height-68,Color.DarkGray);
        Raylib.DrawText("EQUIPPED",rightX,y+66,20,Color.RayWhite);
        for (int i=0;i<Filters.Length;i++)
        {
            int tabX=x+18+i*FilterWidth();
            Raylib.DrawRectangle(tabX,y+62,FilterWidth()-4,30,
                filter==Filters[i] ? Color.Gold : Color.DarkGray);
            Raylib.DrawText(Fit(Filters[i].ToString(),FilterWidth()-12,16),tabX+6,y+68,16,
                filter==Filters[i] ? Color.Black : Color.RayWhite);
        }
        var entries=player.Inventory.Filter(catalogue,filter);
        int rows=InventoryRows();
        int first=Math.Clamp(bagScroll,0,Math.Max(0,entries.Count-rows));
        if (entries.Count==0)
            Raylib.DrawText("No items in this filter.",x+20,RowTop(),17,Color.RayWhite);
        for (int i=first;i<Math.Min(entries.Count,first+rows);i++)
        {
            int rowY=RowTop()+(i-first)*32;
            bool highlight=!equipmentFocused && i==selected;
            if (highlight) Raylib.DrawRectangle(x+14,rowY-3,divider-x-36,30,new Color(90,75,40,255));
            var stack=entries[i];
            string label=catalogue.Get(stack.ItemId).Name + (stack.Quantity>1 ? $" x{stack.Quantity}" : "");
            Raylib.DrawText(Fit(label,divider-x-56,18),x+20,rowY,18,
                highlight ? Color.Gold : Color.RayWhite);
        }
        if (entries.Count>rows)
        {
            int trackX=divider-18,trackY=RowTop()-3,trackHeight=rows*32;
            int thumbHeight=Math.Max(20,trackHeight*rows/entries.Count);
            int thumbY=trackY+(trackHeight-thumbHeight)*first/(entries.Count-rows);
            Raylib.DrawRectangle(trackX,trackY,6,trackHeight,Color.DarkGray);
            Raylib.DrawRectangle(trackX,thumbY,6,thumbHeight,Color.Gold);
        }
        for (int i=0;i<Slots.Length;i++)
        {
            int rowY=RowTop()+i*36;
            bool highlight=equipmentFocused && i==selectedSlot;
            if (highlight) Raylib.DrawRectangle(divider+8,rowY-3,width/2-22,31,new Color(90,75,40,255));
            string name=player.Equipment.In(Slots[i]) is { } id ? catalogue.Get(id).Name : "[empty]";
            Raylib.DrawText(Fit($"{Slots[i]}: {name}",rightWidth,18),rightX,rowY,18,
                highlight ? Color.Gold : Color.RayWhite);
        }

        ItemDefinition? detail=null;
        if (equipmentFocused)
        {
            if (player.Equipment.In(Slots[selectedSlot]) is { } id) detail=catalogue.Get(id);
        }
        else if (entries.Count>0) detail=catalogue.Get(entries[selected].ItemId);
        int detailX=x+14,detailY=y+height-218,detailWidth=divider-x-28;
        Raylib.DrawRectangle(detailX,detailY,detailWidth,166,new Color(23,27,34,255));
        Raylib.DrawRectangleLines(detailX,detailY,detailWidth,166,Color.DarkGray);
        Raylib.DrawText("ITEM DETAILS",detailX+8,detailY+7,16,Color.Gold);
        RenderDetails(detail,detailX+8,detailY+30,detailWidth-16,
            equipmentFocused ? "Empty slot" : "Select an item");
        if (notice is not null)
            Raylib.DrawText(Fit(notice,rightWidth,16),rightX,y+height-76,16,Color.Gold);
    }

    private void RenderAbilities(PlayerEntity player,bool usesMouse)
    {
        int x=PanelLeft(),y=PanelTop(),width=PanelWidth(),height=PanelHeight();
        int divider=x+width/2,rightX=divider+16,rightWidth=width/2-40;
        Raylib.DrawRectangle(divider,y+64,1,height-116,Color.DarkGray);
        Raylib.DrawText("ABILITIES",x+24,y+68,20,Color.Gold);
        Raylib.DrawText("DETAILS",rightX,y+68,20,Color.Gold);
        if (abilities is null || abilities.Abilities.Count==0)
        {
            Raylib.DrawText("No abilities defined.",x+24,RowTop(),18,Color.RayWhite);
            return;
        }
        int count=abilities.Abilities.Count,rows=InventoryRows();
        int first=Math.Clamp(abilityScroll,0,Math.Max(0,count-rows));
        for (int i=first;i<Math.Min(count,first+rows);i++)
        {
            var ability=abilities.Abilities[i];
            int rowY=RowTop()+(i-first)*32;
            bool focused=i==abilityIndex,known=player.Abilities.Knows(ability.Id);
            if (focused) Raylib.DrawRectangle(x+14,rowY-3,divider-x-36,30,
                new Color(90,75,40,255));
            Raylib.DrawText(Fit($"{(known ? "[x]" : "[ ]")} {ability.Name}",
                divider-x-56,18),x+20,rowY,18,focused ? Color.Gold : Color.RayWhite);
        }
        if (count>rows)
        {
            int trackY=RowTop()-3,trackHeight=rows*32;
            int thumbHeight=Math.Max(20,trackHeight*rows/count);
            int thumbY=trackY+(trackHeight-thumbHeight)*first/(count-rows);
            Raylib.DrawRectangle(divider-18,trackY,6,trackHeight,Color.DarkGray);
            Raylib.DrawRectangle(divider-18,thumbY,6,thumbHeight,Color.Gold);
        }
        var selectedAbility=abilities.Abilities[Math.Clamp(abilityIndex,0,count-1)];
        bool learned=player.Abilities.Knows(selectedAbility.Id);
        bool eligible=selectedAbility.MeetsRequirements(player.Attributes,player.SkillIds);
        bool usable=player.Abilities.CanUse(selectedAbility,player);
        Raylib.DrawText(Fit(selectedAbility.Name,rightWidth,24),rightX,y+110,24,Color.Gold);
        Raylib.DrawText(Fit($"{selectedAbility.Path}  |  " +
            (usable ? "Learned, requirements met" : learned ? "Learned, unavailable" :
             eligible ? "Can learn from tome" : "Requirements unmet"),rightWidth,16),
            rightX,y+142,16,Color.RayWhite);
        DrawWrapped(selectedAbility.Description,rightX,y+178,rightWidth,17,4);
        Raylib.DrawText("REQUIREMENTS",rightX,y+270,18,Color.Gold);
        int line=0;
        foreach (var requirement in selectedAbility.Attributes)
        {
            int current=player.Attributes.Get(requirement.Kind);
            Raylib.DrawText(Fit($"{requirement.Kind}: {current} / {requirement.Minimum}",
                rightWidth,16),rightX,y+300+line++*24,16,
                current>=requirement.Minimum ? Color.RayWhite : Color.Gray);
        }
        foreach (var skillId in selectedAbility.RequiredSkills)
        {
            bool hasSkill=player.SkillIds.Contains(skillId);
            string name=skills?.Get(skillId).Name ?? skillId;
            Raylib.DrawText(Fit($"Skill: {name}",rightWidth,16),
                rightX,y+300+line++*24,16,hasSkill ? Color.RayWhite : Color.Gray);
        }
        if (selectedAbility.RequiredEquipmentSlot is { } slot)
        {
            bool equipped=selectedAbility.HasRequiredEquipment(player.Equipment);
            Raylib.DrawText(Fit($"Equipped: {slot}",rightWidth,16),
                rightX,y+300+line*24,16,equipped ? Color.RayWhite : Color.Gray);
        }
        int slotY=y+height-74;
        int slotCount=usesMouse ? AbilityLoadout.SlotCount : AbilityLoadout.WheelSlotCount;
        int slotWidth=(width-28)/slotCount;
        for (int i=0;i<slotCount;i++)
        {
            int slotX=x+14+i*slotWidth;
            string? id=player.Abilities.Loadout.In(i);
            bool focused=i==abilitySlot;
            Raylib.DrawRectangle(slotX,slotY,slotWidth-3,48,
                focused ? new Color(96,78,36,255) : new Color(45,50,59,255));
            if (focused) Raylib.DrawRectangleLines(slotX,slotY,slotWidth-3,48,Color.Gold);
            Raylib.DrawText(AbilityLoadout.KeyLabel(i),slotX+5,slotY+3,16,Color.Gold);
            if (id is not null)
                Raylib.DrawText(Fit(abilities.Get(id).Name,slotWidth-9,12),
                    slotX+4,slotY+26,12,Color.RayWhite);
        }
        if (notice is not null)
            Raylib.DrawText(Fit(notice,width-32,16),x+16,slotY-24,16,Color.Gold);
    }

    private void RenderCharacter(PlayerEntity player)
    {
        int x=PanelLeft(),y=PanelTop(),width=PanelWidth(),height=PanelHeight();
        int divider=x+width/2,rightX=divider+16;
        int columnWidth=width/2-40;
        var attributes=player.Attributes;
        var combat=player.Profile;
        Raylib.DrawRectangle(divider,y+68,1,height-264,Color.DarkGray);
        Raylib.DrawText("ATTRIBUTES",x+24,y+72,20,Color.Gold);
        Raylib.DrawText("COMBAT",rightX,y+72,20,Color.Gold);
        string[] innate=
        [
            $"Strength        {attributes.Strength}",
            $"Endurance       {attributes.Endurance}",
            $"Agility         {attributes.Agility}",
            $"Intelligence    {attributes.Intelligence}",
            $"Willpower       {attributes.Willpower}",
            $"Speed           {attributes.Speed}",
            $"Charisma        {attributes.Charisma}",
            $"Luck            {attributes.Luck}"
        ];
        string[] derived=
        [
            $"Health          {player.Health}/{player.MaxHealth}",
            $"Damage          {combat.Damage}",
            $"Attack range    {combat.Range:0.0} m",
            $"Attack speed    {combat.AttacksPerSecond:0.00}/s",
            $"Cast time       {combat.CastSeconds:0.00} s",
            $"Cooldown        {combat.CooldownSeconds:0.00} s",
            $"Armour          {combat.Armour}",
            $"Resistance      {combat.ResistancePercent}%"
        ];
        for (int i=0;i<innate.Length;i++)
            Raylib.DrawText(Fit(innate[i],columnWidth,18),x+24,y+108+i*22,18,Color.RayWhite);
        for (int i=0;i<derived.Length;i++)
            Raylib.DrawText(Fit(derived[i],columnWidth,18),rightX,y+108+i*22,18,Color.RayWhite);
        Raylib.DrawText("EQUIPMENT",x+24,y+height-174,20,Color.Gold);
        for (int i=0;i<Slots.Length;i++)
        {
            var equipped=player.Equipment.In(Slots[i]);
            string name=equipped is null ? "[empty]" : catalogue.Get(equipped).Name;
            Raylib.DrawText(Fit($"{Slots[i]}: {name}",columnWidth,18),
                x+24,y+height-143+i*27,18,Color.RayWhite);
        }
        Raylib.DrawText("SKILLS",rightX,y+height-174,20,Color.Gold);
        for (int i=0;i<player.SkillIds.Count;i++)
        {
            string name=skills?.Get(player.SkillIds[i]).Name ?? player.SkillIds[i];
            Raylib.DrawText(Fit(name,columnWidth,18),rightX,y+height-143+i*27,18,
                Color.RayWhite);
        }
    }

    private void RenderDetails(ItemDefinition? item,int x,int y,int width,string emptyText)
    {
        if (item is null)
        {
            Raylib.DrawText(emptyText,x,y,18,Color.RayWhite);
            return;
        }
        Raylib.DrawText(Fit(item.Name,width,20),x,y,20,Color.Gold);
        Raylib.DrawText(Fit($"{item.Type}  |  {item.Slot}",width,14),x,y+23,14,Color.RayWhite);
        Raylib.DrawText(Fit($"Weight: {item.Weight:0.##} kg  Value: {item.Value}",width,14),
            x,y+42,14,Color.RayWhite);
        Raylib.DrawText(Fit($"Damage: {item.AttackPower}  Protection: {item.Protection}",width,14),
            x,y+60,14,Color.RayWhite);
        string attack=item.ItemType==ItemType.Weapon
            ? $"Range: {item.AttackRange:0.0} m  Speed: {item.AttackSpeed:0.00}/s"
            : item.ItemType==ItemType.Tome
                ? $"Teaches: {abilities?.Get(item.AbilityId!).Name ?? item.AbilityId}"
                : item.ItemType==ItemType.Ammunition ? "Ammunition - cannot equip" : "Apparel";
        Raylib.DrawText(Fit(attack,width,14),x,y+78,14,Color.RayWhite);
        DrawWrapped(item.Description,x,y+97,width,14,2);
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
                Raylib.DrawText(line,x,y+drawn*(size+3),size,Color.RayWhite);
                if (++drawn>=maxLines) return;
                line=word;
            }
            else line=next;
        }
        if (line.Length>0 && drawn<maxLines)
            Raylib.DrawText(line,x,y+drawn*(size+3),size,Color.RayWhite);
    }
    private static string Fit(string value,int width,int size)
    {
        if (Raylib.MeasureText(value,size)<=width) return value;
        while (value.Length>0 && Raylib.MeasureText(value+"...",size)>width)
            value=value[..^1];
        return value+"...";
    }
    private static int? RowAt(System.Numerics.Vector2 pointer,int left,int width,int top,
        int count,int first,int rows,int rowHeight=32)
    {
        if (pointer.X<left || pointer.X>=left+width ||
            pointer.Y<top || pointer.Y>=top+rows*rowHeight) return null;
        int index=first+(int)((pointer.Y-top)/rowHeight);
        return index<count ? index : null;
    }
    private int FilterWidth() => (PanelWidth()/2-36)/Filters.Length;
    private int PanelWidth() => Math.Min(mode==PanelMode.Chest ? 640 : 1000,Raylib.GetScreenWidth()-24);
    private static int PanelHeight() => Math.Min(600,Raylib.GetScreenHeight()-24);
    private int PanelLeft() => (Raylib.GetScreenWidth()-PanelWidth())/2;
    private static int PanelTop() => (Raylib.GetScreenHeight()-PanelHeight())/2;
    private static int RowTop() => PanelTop()+114;
    private static int InventoryRows() => Math.Max(1,(PanelHeight()-340)/32);
    private static int ChestRows() => Math.Max(1,(PanelHeight()-260)/32);
    private static int FirstVisible(int selected,int count,int rows) =>
        Math.Clamp(selected-rows+1,0,Math.Max(0,count-rows));
}

using System.Numerics;
using CCM.Game;
using Raylib_cs;
using RaylibGameFramework.Assets;

namespace CCM.Scenes;

internal sealed class DungeonRenderer(DungeonSimulation simulation,ItemCatalogue items,
    bool debug,AssetManager? assets=null)
{
    private readonly GltfCharacter? playerAnimation=assets is null ? null :
        new GltfCharacter(assets.GetModel("player-character"),
            "Assets/Characters/human_male_locomotion.glb",1.78f);
    private readonly GltfCharacter? enemyAnimation=assets is null ? null :
        new GltfCharacter(assets.GetModel("enemy-character"),
            "Assets/Characters/orc_male_idle.glb",2.08f,
            "Assets/Characters/orc_male");

    public void Render()
    {
        Raylib.ClearBackground(new Color(20,23,30,255));
        var camera = simulation.Camera.ToRaylibCamera();
        Raylib.BeginMode3D(camera);
        for (int x = -8; x < 8; x += 2)
            for (int z = -8; z < 8; z += 2)
                Floor(new(x+1,-0.15f,z+1), ((x+z)/2 & 1) == 0);
        for (int x = -2; x < 2; x += 2)
            for (int z = -16; z < -8; z += 2) Floor(new(x+1,-0.15f,z+1), ((x+z)/2 & 1) == 0);
        for (int x = -10; x < 10; x += 2)
            for (int z = -40; z < -16; z += 2)
                Floor(new(x+1,-0.15f,z+1), ((x+z)/2 & 1) == 0);
        foreach (var block in simulation.World.StaticSolids)
        {
            Color colour = block.Size.Y < 2 ? new Color(117,91,63,255) : new Color(86,93,104,255);
            Raylib.DrawCubeV(block.Centre,block.Size,colour);
            Raylib.DrawCubeWiresV(block.Centre,block.Size,new Color(43,49,59,255));
        }
        foreach (var item in simulation.World.Interactables) DrawInteractable(item);
        var enemy = simulation.Enemy;
        if (assets is not null)
        {
            var model=assets.GetModel("enemy-character");
            enemyAnimation!.Update(model,enemy.Position,simulation.AnimationSeconds);
            DrawCharacter(model,enemy.Position,simulation.Player.Position-enemy.Position,
                enemy.Alive ? Color.White : Color.Gray);
        }
        else
            Raylib.DrawCubeV(enemy.Position+new Vector3(0,0.9f,0),new(0.9f,1.8f,0.9f),
                enemy.Alive ? new Color(170,58,58,255) : Color.DarkGray);
        if (simulation.EnemySelected && enemy.Alive)
            Raylib.DrawCubeWiresV(enemy.Position+new Vector3(0,0.9f,0),new(1,1.9f,1),Color.Gold);
        if (simulation.MoveDestination is { } destination)
            Raylib.DrawCubeV(destination+new Vector3(0,0.08f,0),new(0.4f,0.12f,0.4f),Color.Gold);
        var player = simulation.Player;
        bool armoured=player.Equipment.In(EquipmentSlot.Body) is { } body &&
            items.Get(body).VisualKind==ItemVisual.Armour;
        if (assets is not null)
        {
            var model=assets.GetModel("player-character");
            playerAnimation!.Update(model,player.Position,simulation.AnimationSeconds);
            DrawCharacter(model,player.Position,player.Forward,Color.White);
        }
        else
        {
            Raylib.DrawCubeV(player.Position + new Vector3(0,0.65f,0),new(0.6f,1.1f,0.6f),
                armoured ? new Color(116,103,76,255) : Color.SkyBlue);
            Raylib.DrawCubeV(player.Position + new Vector3(0,1.4f,0),new(0.5f,0.4f,0.5f),Color.Beige);
            Raylib.DrawCubeV(player.TransformPoint(new(0,1.4f,-0.31f)),
                new(0.18f,0.15f,0.18f),Color.Orange);
        }
        DrawEquipment(player);
        if (debug)
        {
            foreach (var block in simulation.World.Solids)
                Raylib.DrawCubeWiresV(block.Centre,block.Size + new Vector3(0.015f),Color.Lime);
            Raylib.DrawCubeWiresV(player.Position+new Vector3(0,0.8f,0),new(0.7f,1.6f,0.7f),Color.Yellow);
            Raylib.DrawLine3D(player.Position+Vector3.UnitY,player.Position+Vector3.UnitY+player.Forward,Color.Red);
        }
        if (simulation.Target is { } highlighted)
            Raylib.DrawCubeWiresV(highlighted.Item.Bounds.Centre,highlighted.Item.Bounds.Size+new Vector3(0.05f),Color.Gold);
        Raylib.EndMode3D();
    }

    private static void DrawCharacter(Model model,Vector3 position,Vector3 facing,Color tint)
    {
        facing.Y=0;
        if (facing.LengthSquared()<0.0001f) facing=-Vector3.UnitZ;
        float angle=MathF.Atan2(facing.X,facing.Z)*180f/MathF.PI;
        Raylib.DrawModelEx(model,position,Vector3.UnitY,angle,Vector3.One,tint);
    }

    private void DrawEquipment(PlayerEntity player)
    {
        if (player.Equipment.In(EquipmentSlot.Quiver) is { } quiver &&
            items.Get(quiver).VisualKind==ItemVisual.Quiver)
        {
            Raylib.DrawCubeV(player.TransformPoint(new(0.25f,0.85f,0.38f)),
                new(0.26f,0.65f,0.22f),Color.Brown);
            if (player.Equipment.AmmoCount>0)
                Raylib.DrawCubeV(player.TransformPoint(new(0.25f,1.25f,0.38f)),
                    new(0.18f,0.2f,0.16f),Color.Beige);
        }
        if (player.Equipment.In(EquipmentSlot.Weapon) is not { } weapon) return;
        var hand=player.TransformPoint(new(0.48f,0.9f,-0.1f));
        switch (items.Get(weapon).VisualKind)
        {
            case ItemVisual.Sword:
                Raylib.DrawCubeV(hand+new Vector3(0,0.45f,0),new(0.12f,0.9f,0.12f),Color.LightGray);
                Raylib.DrawCubeV(hand,new(0.38f,0.1f,0.16f),Color.Gold);
                break;
            case ItemVisual.Bow:
                Raylib.DrawCubeV(hand,new(0.12f,1.25f,0.14f),Color.Brown);
                Raylib.DrawCubeV(hand+new Vector3(0.2f,0,0),new(0.03f,1.2f,0.03f),Color.Beige);
                break;
            case ItemVisual.Staff:
                Raylib.DrawCubeV(hand+new Vector3(0,0.25f,0),new(0.14f,1.6f,0.14f),Color.Brown);
                Raylib.DrawCubeV(hand+new Vector3(0,1.1f,0),new(0.25f,0.25f,0.25f),Color.Purple);
                break;
        }
    }

    private void DrawInteractable(Interactable item)
    {
        var box = item.Bounds;
        bool selected = simulation.Target?.Item == item;
        Color wood = selected ? new Color(205,161,65,255) : new Color(125,78,42,255);
        if (item.Kind == InteractionKind.Door)
        {
            Raylib.DrawCubeV(box.Centre,box.Size,wood);
            Raylib.DrawCubeWiresV(box.Centre,box.Size,Color.DarkBrown);
            // Metal bands make the leaf recognizable from either room.
            for (float y = 0.5f; y < 3; y += 1)
                Raylib.DrawCubeV(new(box.Centre.X,y,box.Centre.Z),
                    item.IsOpen ? new(0.34f,0.1f,4) : new(4,0.1f,0.34f),Color.DarkGray);
        }
        else
        {
            var c = box.Centre;
            Raylib.DrawCubeV(new(c.X,0.12f,c.Z),new(1.4f,0.24f,1.4f),wood);
            Raylib.DrawCubeV(new(c.X,0.28f,c.Z),new(1.1f,0.06f,1.1f),new Color(35,24,18,255));
            foreach (float side in new[] { -1f,1f })
            {
                Raylib.DrawCubeV(new(c.X+side*0.62f,0.62f,c.Z),new(0.16f,0.8f,1.4f),wood);
                Raylib.DrawCubeV(new(c.X,0.62f,c.Z+side*0.62f),new(1.1f,0.8f,0.16f),wood);
            }
            Raylib.DrawCubeV(item.IsOpen ? new(c.X,1,c.Z-0.62f) : new(c.X,1.12f,c.Z),
                item.IsOpen ? new(1.4f,0.4f,0.16f) : new(1.4f,0.16f,1.4f),wood);
            Raylib.DrawCubeWiresV(box.Centre,box.Size,Color.DarkBrown);
        }
    }
    private static void Floor(Vector3 centre, bool light) =>
        Raylib.DrawCubeV(centre,new(2,0.3f,2),light ? new Color(54,61,69,255) : new Color(47,53,62,255));
}

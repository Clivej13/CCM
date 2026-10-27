using System.Numerics;
using CCM.Game;
using Raylib_cs;
using RaylibGameFramework.Assets;

namespace CCM.Scenes;

internal sealed class DungeonRenderer(DungeonSimulation simulation,ItemCatalogue items,
    bool debug,AssetManager? assets=null,float fireplaceLightIntensity=1f)
{
    private readonly GltfCharacter? playerAnimation=assets is null ? null :
        new GltfCharacter(assets.GetModel("player-character"),
            "Assets/Characters/human_male_locomotion.glb",1.78f);
    private readonly GltfCharacter? enemyAnimation=assets is null ? null :
        new GltfCharacter(assets.GetModel("enemy-character"),
            "Assets/Characters/orc_male_idle.glb",2.08f,
            "Assets/Characters/orc_male");
    private readonly DungeonLighting? lighting=assets is null ? null :
        new DungeonLighting(assets.GetModel("player-character"),
            assets.GetModel("enemy-character"),assets.GetModel("dungeon-fireplace"),
            simulation.World,DungeonScenery.ModelKeys.Select(assets.GetModel).ToArray());

    public void Dispose() => lighting?.Dispose();

    public void Render()
    {
        Raylib.ClearBackground(Color.Black);
        lighting?.Update(simulation.AnimationSeconds,fireplaceLightIntensity);
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
            if (assets is not null)
                DungeonScenery.DrawStaticBlock(assets,lighting!,block);
            else
            {
                DrawLitCube(block.Centre,block.Size,colour);
                DrawLitCubeWires(block.Centre,block.Size,new Color(43,49,59,255));
            }
        }
        if (assets is not null) DungeonScenery.DrawDetails(assets,lighting!);
        foreach (var item in simulation.World.Interactables) DrawInteractable(item);
        var enemy = simulation.Enemy;
        if (assets is not null)
        {
            var model=assets.GetModel("enemy-character");
            enemyAnimation!.Update(model,enemy.Position,simulation.AnimationSeconds);
            lighting!.SetModelReceiver(enemy.Position+new Vector3(0,1f,0));
            DrawCharacter(model,enemy.Position,simulation.Player.Position-enemy.Position,
                enemy.Alive ? Color.White : Color.Gray);
        }
        else
            DrawLitCube(enemy.Position+new Vector3(0,0.9f,0),new(0.9f,1.8f,0.9f),
                enemy.Alive ? new Color(170,58,58,255) : Color.DarkGray);
        if (simulation.EnemySelected && enemy.Alive)
            DrawLitCubeWires(enemy.Position+new Vector3(0,0.9f,0),new(1,1.9f,1),Color.Gold);
        if (simulation.MoveDestination is { } destination)
            DrawLitCube(destination+new Vector3(0,0.08f,0),new(0.4f,0.12f,0.4f),Color.Gold);
        var player = simulation.Player;
        bool armoured=player.Equipment.In(EquipmentSlot.Body) is { } body &&
            items.Get(body).VisualKind==ItemVisual.Armour;
        if (assets is not null)
        {
            var model=assets.GetModel("player-character");
            playerAnimation!.Update(model,player.Position,simulation.AnimationSeconds);
            lighting!.SetModelReceiver(player.Position+new Vector3(0,0.9f,0));
            DrawCharacter(model,player.Position,player.Forward,Color.White);
        }
        else
        {
            DrawLitCube(player.Position + new Vector3(0,0.65f,0),new(0.6f,1.1f,0.6f),
                armoured ? new Color(116,103,76,255) : Color.SkyBlue);
            DrawLitCube(player.Position + new Vector3(0,1.4f,0),new(0.5f,0.4f,0.5f),Color.Beige);
            DrawLitCube(player.TransformPoint(new(0,1.4f,-0.31f)),
                new(0.18f,0.15f,0.18f),Color.Orange);
        }
        DrawEquipment(player);
        if (debug)
        {
            foreach (var block in simulation.World.Solids)
                DrawLitCubeWires(block.Centre,block.Size + new Vector3(0.015f),Color.Lime);
            DrawLitCubeWires(player.Position+new Vector3(0,0.8f,0),new(0.7f,1.6f,0.7f),Color.Yellow);
            Raylib.DrawLine3D(player.Position+Vector3.UnitY,player.Position+Vector3.UnitY+player.Forward,Color.Red);
        }
        if (simulation.Target is { } highlighted)
            DrawLitCubeWires(highlighted.Item.Bounds.Centre,highlighted.Item.Bounds.Size+new Vector3(0.05f),Color.Gold);
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
            DrawLitCube(player.TransformPoint(new(0.25f,0.85f,0.38f)),
                new(0.26f,0.65f,0.22f),Color.Brown);
            if (player.Equipment.AmmoCount>0)
                DrawLitCube(player.TransformPoint(new(0.25f,1.25f,0.38f)),
                    new(0.18f,0.2f,0.16f),Color.Beige);
        }
        if (player.Equipment.In(EquipmentSlot.Weapon) is not { } weapon) return;
        var hand=player.TransformPoint(new(0.48f,0.9f,-0.1f));
        switch (items.Get(weapon).VisualKind)
        {
            case ItemVisual.Sword:
                DrawLitCube(hand+new Vector3(0,0.45f,0),new(0.12f,0.9f,0.12f),Color.LightGray);
                DrawLitCube(hand,new(0.38f,0.1f,0.16f),Color.Gold);
                break;
            case ItemVisual.Bow:
                DrawLitCube(hand,new(0.12f,1.25f,0.14f),Color.Brown);
                DrawLitCube(hand+new Vector3(0.2f,0,0),new(0.03f,1.2f,0.03f),Color.Beige);
                break;
            case ItemVisual.Staff:
                DrawLitCube(hand+new Vector3(0,0.25f,0),new(0.14f,1.6f,0.14f),Color.Brown);
                DrawLitCube(hand+new Vector3(0,1.1f,0),new(0.25f,0.25f,0.25f),Color.Purple);
                break;
        }
    }

    private void DrawInteractable(Interactable item)
    {
        var box = item.Bounds;
        bool selected = simulation.Target?.Item == item;
        if (item is FireplaceInteractable fireplace)
        {
            if (assets is not null)
            {
                lighting!.SetModelReceiver(fireplace.Position+new Vector3(0,1f,0));
                Raylib.DrawModelEx(assets.GetModel("dungeon-fireplace"),
                    fireplace.Position,Vector3.UnitY,fireplace.RotationDegrees,
                    Vector3.One,Color.White);
            }
            else
                DrawLitCube(box.Centre,box.Size,Color.DarkGray);
            FireplaceFlame.Draw(fireplace,simulation.AnimationSeconds);
            if (selected)
                DrawLitCubeWires(box.Centre,box.Size+new Vector3(0.03f),Color.Gold);
            return;
        }
        if (assets is not null)
        {
            if (item is DoorInteractable door)
            {
                DungeonScenery.DrawDoor(assets,lighting!,door);
                if (selected) DrawLitCubeWires(box.Centre,box.Size,Color.Gold);
                return;
            }
            if (item is ChestInteractable chest)
            {
                DungeonScenery.DrawChest(assets,lighting!,chest);
                if (selected) DrawLitCubeWires(box.Centre,box.Size,Color.Gold);
                return;
            }
        }
        Color wood = selected ? new Color(205,161,65,255) : new Color(125,78,42,255);
        if (item.Kind == InteractionKind.Door)
        {
            DrawLitCube(box.Centre,box.Size,wood);
            DrawLitCubeWires(box.Centre,box.Size,Color.DarkBrown);
            // Metal bands make the leaf recognizable from either room.
            for (float y = 0.5f; y < 3; y += 1)
                DrawLitCube(new(box.Centre.X,y,box.Centre.Z),
                    item.IsOpen ? new(0.34f,0.1f,4) : new(4,0.1f,0.34f),Color.DarkGray);
        }
        else
        {
            var c = box.Centre;
            DrawLitCube(new(c.X,0.12f,c.Z),new(1.4f,0.24f,1.4f),wood);
            DrawLitCube(new(c.X,0.28f,c.Z),new(1.1f,0.06f,1.1f),new Color(35,24,18,255));
            foreach (float side in new[] { -1f,1f })
            {
                DrawLitCube(new(c.X+side*0.62f,0.62f,c.Z),new(0.16f,0.8f,1.4f),wood);
                DrawLitCube(new(c.X,0.62f,c.Z+side*0.62f),new(1.1f,0.8f,0.16f),wood);
            }
            DrawLitCube(item.IsOpen ? new(c.X,1,c.Z-0.62f) : new(c.X,1.12f,c.Z),
                item.IsOpen ? new(1.4f,0.4f,0.16f) : new(1.4f,0.16f,1.4f),wood);
            DrawLitCubeWires(box.Centre,box.Size,Color.DarkBrown);
        }
    }
    private void DrawLitCube(Vector3 centre,Vector3 size,Color colour)
    {
        if (lighting is { } lit) lit.DrawCube(centre,size,colour);
        else Raylib.DrawCubeV(centre,size,colour);
    }

    private void DrawLitCubeWires(Vector3 centre,Vector3 size,Color colour) =>
        Raylib.DrawCubeWiresV(centre,size,lighting?.LitColor(colour,centre,size) ?? colour);

    private void Floor(Vector3 centre,bool light)
    {
        if (assets is not null)
            DungeonScenery.DrawFloor(assets,lighting!,centre);
        else
            DrawLitCube(centre,new(2,0.3f,2),
                light ? new Color(54,61,69,255) : new Color(47,53,62,255));
    }
}

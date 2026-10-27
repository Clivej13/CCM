using System.Numerics;
using CCM.Game;
using Raylib_cs;
using RaylibGameFramework.Assets;

namespace CCM.Scenes;

internal static class DungeonScenery
{
    public static readonly string[] ModelKeys=
    [
        "dungeon-floor-flagstone","dungeon-floor-cracked",
        "dungeon-wall-straight","dungeon-wall-barred-window",
        "dungeon-pillar-corner","dungeon-doorway-arch","dungeon-door-oak",
        "dungeon-chest-oak","dungeon-crate-closed","dungeon-crate-open",
        "dungeon-barrel-oak","dungeon-rubble-small","dungeon-torch-sconce"
    ];

    public static void DrawFloor(AssetManager assets,DungeonLighting lighting,Vector3 centre)
    {
        bool cracked=Math.Abs((int)centre.X*17+(int)centre.Z*31)%7==0;
        Draw(assets,lighting,cracked ? "dungeon-floor-cracked" : "dungeon-floor-flagstone",
            new(centre.X,0,centre.Z),0,Vector3.One,
            new(new(centre.X,-0.09f,centre.Z),new(2,0.18f,2)));
    }

    public static void DrawStaticBlock(AssetManager assets,DungeonLighting lighting,Block block)
    {
        if (block.Size.Y>=2.5f)
        {
            if (block.Size.X<2 && block.Size.Z<2)
            {
                Draw(assets,lighting,"dungeon-pillar-corner",
                    new(block.Centre.X,block.Min.Y,block.Centre.Z),0,
                    new(block.Size.X/0.52f,block.Size.Y/3.2f,block.Size.Z/0.52f),block);
                return;
            }
            DrawWall(assets,lighting,block);
            return;
        }

        string key=block.Centre.Z switch
        {
            < -30 => "dungeon-crate-closed",
            < -20 => "dungeon-barrel-oak",
            < 1.5f => "dungeon-crate-closed",
            _ => "dungeon-crate-open"
        };
        Vector3 native=key=="dungeon-barrel-oak"
            ? new(0.85f,1.217f,0.83f) : new(1,0.84f,0.955f);
        Draw(assets,lighting,key,new(block.Centre.X,block.Min.Y,block.Centre.Z),0,
            block.Size/native,block);
    }

    private static void DrawWall(AssetManager assets,DungeonLighting lighting,Block block)
    {
        bool alongX=block.Size.X>block.Size.Z;
        float length=alongX ? block.Size.X : block.Size.Z;
        float thickness=alongX ? block.Size.Z : block.Size.X;
        int count=Math.Max(1,(int)MathF.Round(length/2));
        float segment=length/count;
        for (int i=0;i<count;i++)
        {
            float offset=-length*0.5f+segment*(i+0.5f);
            Vector3 centre=block.Centre+
                (alongX ? new Vector3(offset,0,0) : new Vector3(0,0,offset));
            bool window=!alongX && block.Centre.X< -10 && MathF.Abs(centre.Z+29)<0.1f;
            var segmentBlock=new Block(centre,
                alongX ? new Vector3(segment,block.Size.Y,thickness)
                    : new Vector3(thickness,block.Size.Y,segment));
            Draw(assets,lighting,window ? "dungeon-wall-barred-window" : "dungeon-wall-straight",
                new(centre.X,block.Min.Y,centre.Z),alongX ? 0 : 90,
                new(segment/2,block.Size.Y/3.2f,thickness/0.39f),segmentBlock);
        }
    }

    public static void DrawDoor(AssetManager assets,DungeonLighting lighting,DoorInteractable door)
    {
        Draw(assets,lighting,"dungeon-doorway-arch",new(0,0,-8.5f),0,
            new(2,3f/3.2f,0.75f),
            new(new(0,1.5f,-8.5f),new(4,3,0.4f)));
        Draw(assets,lighting,"dungeon-door-oak",new(-1.38f,0,-8.5f),
            door.IsOpen ? 100 : 0,new(2,1,1),door.Bounds);
    }

    public static void DrawChest(AssetManager assets,DungeonLighting lighting,ChestInteractable chest)
    {
        var box=chest.Bounds;
        Draw(assets,lighting,"dungeon-chest-oak",
            new(box.Centre.X,box.Min.Y,box.Centre.Z),0,
            new(box.Size.X/1.25f,box.Size.Y/0.877f,box.Size.Z/0.84f),box);
    }

    public static void DrawDetails(AssetManager assets,DungeonLighting lighting)
    {
        foreach (var position in new[] { new Vector3(-6.7f,0,-6.3f),
                     new Vector3(7.8f,0,-30.5f) })
            Draw(assets,lighting,"dungeon-rubble-small",position,0,Vector3.One,
                new(position+new Vector3(0,0.1f,0),new(1.4f,0.3f,1.4f)));
        Draw(assets,lighting,"dungeon-torch-sconce",new(-3.2f,0,-8.02f),0,
            Vector3.One,new(new(-3.2f,1.6f,-8.02f),new(0.2f,0.8f,0.4f)));
    }

    private static void Draw(AssetManager assets,DungeonLighting lighting,string key,
        Vector3 position,float angle,Vector3 scale,Block receiver)
    {
        lighting.SetModelReceiver(receiver.Centre,receiver.Size);
        Raylib.DrawModelEx(assets.GetModel(key),position,Vector3.UnitY,angle,scale,Color.White);
    }
}

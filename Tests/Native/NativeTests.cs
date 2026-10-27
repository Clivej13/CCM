using System.Numerics;
using CCM.Application;
using CCM.Game;
using Raylib_cs;
using RaylibGameFramework.Configuration;
using RaylibGameFramework.Logging;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace CCM.NativeTests;

public sealed class NativeTests
{
    [Fact]
    public void FirelightFlickersAndExtinguishesWithTheHearth()
    {
        var fireplaces=new DungeonWorld().Fireplaces;
        float first=CCM.Scenes.FireplaceFlame.Intensity(fireplaces[0],0);
        float second=CCM.Scenes.FireplaceFlame.Intensity(fireplaces[0],0.2f);
        Assert.InRange(first,0.5f,1f);
        Assert.InRange(second,0.5f,1f);
        Assert.NotEqual(first,second);
        fireplaces[0].Activate(Vector3.Zero);
        Assert.Equal(0,CCM.Scenes.FireplaceFlame.Intensity(fireplaces[0],0.2f));
        Assert.True(CCM.Scenes.FireplaceFlame.Intensity(fireplaces[1],0.2f)>0);
    }

    [Fact]
    public void ExtinguishedFireplacesLeaveDungeonGeometryDark()
    {
        Raylib.InitWindow(800,600,"CCM fireplace lighting");
        var player=Raylib.LoadModel(Path.Combine("Assets","Characters","Library","Human","Male","Outfits","Underlayer","Animations","Generated","human_male_underlayer_locomotion.glb"));
        var enemy=Raylib.LoadModel(Path.Combine("Assets","Characters","Library","Orc","Male","Outfits","Underlayer","Animations","Generated","orc_male_underlayer_locomotion.glb"));
        var hearth=Raylib.LoadModel(Path.Combine("Assets","Props","dungeon_fireplace.glb"));
        try
        {
            var world=new DungeonWorld();
            using var lighting=new CCM.Scenes.DungeonLighting(player,enemy,hearth,world);
            var fireplaces=world.Fireplaces;
            lighting.Update(0,1);
            var surface=fireplaces[0].FireOrigin;
            Assert.True(lighting.LitColor(Color.White,surface,Vector3.Zero).R>0);
            var nearby=lighting.LitColor(Color.White,surface+Vector3.UnitZ*3,Vector3.Zero);
            Assert.InRange((int)nearby.R-(int)nearby.B,0,80);
            fireplaces[0].Activate(Vector3.Zero);
            lighting.Update(0.2f,1);
            Assert.Equal(0,lighting.LitColor(Color.White,surface,Vector3.Zero).R);
            foreach (var fireplace in fireplaces.Skip(1)) fireplace.Activate(Vector3.Zero);
            lighting.Update(0.2f,1);
            Assert.Equal(new Color(0,0,0,255),
                lighting.LitColor(Color.White,surface,Vector3.Zero));

            fireplaces[1].Activate(Vector3.Zero);
            lighting.Update(0.2f,1);
            var nearDoor=new Vector3(0,1,-6);
            Assert.Equal(0,lighting.LitColor(Color.White,nearDoor,Vector3.Zero).R);
            world.Interactables[0].Activate(world.Spawn);
            Assert.True(lighting.LitColor(Color.White,nearDoor,Vector3.Zero).R>0);

            fireplaces[1].Activate(Vector3.Zero);
            fireplaces[0].Activate(Vector3.Zero);
            lighting.Update(0.4f,1);
            var camera=new Camera3D
            {
                Position=new(0,15,3),Target=new(0,0,3),Up=-Vector3.UnitZ,
                FovY=16,Projection=CameraProjection.Orthographic
            };
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            Raylib.BeginMode3D(camera);
            lighting.DrawCube(new(0,-0.15f,3),new(16,0.3f,10),Color.White);
            Raylib.EndMode3D();
            var image=Raylib.LoadImageFromScreen();
            Raylib.EndDrawing();
            try
            {
                var nearScreen=Raylib.GetWorldToScreen(new(-6,0,3),camera);
                var farScreen=Raylib.GetWorldToScreen(new(5,0,3),camera);
                var nearPixel=Raylib.GetImageColor(image,(int)nearScreen.X,(int)nearScreen.Y);
                var farPixel=Raylib.GetImageColor(image,(int)farScreen.X,(int)farScreen.Y);
                Assert.True(nearPixel.R>farPixel.R+20,
                    $"Expected smooth fire falloff across one floor: near={nearPixel}, far={farPixel}");
            }
            finally { Raylib.UnloadImage(image); }

            fireplaces[0].Activate(Vector3.Zero);
            fireplaces[1].Activate(Vector3.Zero);
            world.Interactables[0].Activate(world.Spawn);
            var doorCamera=new Camera3D
            {
                Position=new(0,15,-8),Target=new(0,0,-8),Up=-Vector3.UnitZ,
                FovY=16,Projection=CameraProjection.Orthographic
            };
            Color DoorPixel()
            {
                lighting.Update(0.4f,1);
                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.Black);
                Raylib.BeginMode3D(doorCamera);
                lighting.DrawCube(new(0,-0.15f,-7),new(12,0.3f,10),Color.White);
                Raylib.EndMode3D();
                var capture=Raylib.LoadImageFromScreen();
                Raylib.EndDrawing();
                try
                {
                    var screen=Raylib.GetWorldToScreen(new(0,0,-6),doorCamera);
                    return Raylib.GetImageColor(capture,(int)screen.X,(int)screen.Y);
                }
                finally { Raylib.UnloadImage(capture); }
            }
            var closedPixel=DoorPixel();
            world.Interactables[0].Activate(world.Spawn);
            var openPixel=DoorPixel();
            Assert.True(openPixel.R>closedPixel.R+10,
                $"Door should block firelight: closed={closedPixel}, open={openPixel}");
        }
        finally
        {
            Raylib.UnloadModel(hearth);
            Raylib.UnloadModel(enemy);
            Raylib.UnloadModel(player);
            Raylib.CloseWindow();
        }
    }

    [Fact]
    public void FloorTilesMatchContinuousPerPixelLighting()
    {
        Raylib.InitWindow(800,600,"CCM floor lighting seams");
        var player=Raylib.LoadModel(Path.Combine("Assets","Characters","Library","Human","Male","Outfits","Underlayer","Animations","Generated","human_male_underlayer_locomotion.glb"));
        var enemy=Raylib.LoadModel(Path.Combine("Assets","Characters","Library","Orc","Male","Outfits","Underlayer","Animations","Generated","orc_male_underlayer_locomotion.glb"));
        var hearth=Raylib.LoadModel(Path.Combine("Assets","Props","dungeon_fireplace.glb"));
        try
        {
            var world=new DungeonWorld();
            world.Fireplaces[1].Activate(Vector3.Zero);
            world.Fireplaces[2].Activate(Vector3.Zero);
            using var lighting=new CCM.Scenes.DungeonLighting(player,enemy,hearth,world);
            lighting.Update(0.2f,1);
            var camera=new Camera3D
            {
                Position=new(0,20,0),Target=Vector3.Zero,Up=-Vector3.UnitZ,
                FovY=18,Projection=CameraProjection.Orthographic
            };
            var samples=new List<Vector3>();
            for (int x=-7;x<=7;x+=2)
                for (int z=-7;z<=7;z+=2) samples.Add(new(x,0,z));
            Color[] Capture(bool tiled)
            {
                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.Black);
                Raylib.BeginMode3D(camera);
                if (tiled)
                {
                    foreach (var point in samples)
                        lighting.DrawCube(new(point.X,-0.15f,point.Z),
                            new(2,0.3f,2),Color.White);
                }
                else lighting.DrawCube(new(0,-0.15f,0),new(16,0.3f,16),Color.White);
                Raylib.EndMode3D();
                var image=Raylib.LoadImageFromScreen();
                Raylib.EndDrawing();
                try
                {
                    return samples.Select(point=>
                    {
                        var screen=Raylib.GetWorldToScreen(point,camera);
                        return Raylib.GetImageColor(image,(int)screen.X,(int)screen.Y);
                    }).ToArray();
                }
                finally { Raylib.UnloadImage(image); }
            }
            var continuous=Capture(false);
            var tiles=Capture(true);
            Assert.Contains(continuous,color=>color.R>30);
            for (int i=0;i<samples.Count;i++)
            {
                Assert.InRange(Math.Abs((int)tiles[i].R-continuous[i].R),0,4);
                Assert.InRange(Math.Abs((int)tiles[i].G-continuous[i].G),0,4);
                Assert.InRange(Math.Abs((int)tiles[i].B-continuous[i].B),0,4);
            }
        }
        finally
        {
            Raylib.UnloadModel(hearth);
            Raylib.UnloadModel(enemy);
            Raylib.UnloadModel(player);
            Raylib.CloseWindow();
        }
    }

    [Fact]
    public void CharacterShadowFollowsFireAndDisappearsWhenExtinguished()
    {
        Raylib.InitWindow(800,600,"CCM character fire shadow");
        var player=Raylib.LoadModel(Path.Combine("Assets","Characters","Library","Human","Male","Outfits","Underlayer","Animations","Generated","human_male_underlayer_locomotion.glb"));
        var enemy=Raylib.LoadModel(Path.Combine("Assets","Characters","Library","Orc","Male","Outfits","Underlayer","Animations","Generated","orc_male_underlayer_locomotion.glb"));
        var hearth=Raylib.LoadModel(Path.Combine("Assets","Props","dungeon_fireplace.glb"));
        try
        {
            var world=new DungeonWorld();
            world.Fireplaces[1].Activate(Vector3.Zero);
            world.Fireplaces[2].Activate(Vector3.Zero);
            using var lighting=new CCM.Scenes.DungeonLighting(player,enemy,hearth,world);
            var camera=new Camera3D
            {
                Position=new(0,15,3),Target=new(0,0,3),Up=-Vector3.UnitZ,
                FovY=16,Projection=CameraProjection.Orthographic
            };
            Color FloorPixel()
            {
                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.Black);
                Raylib.BeginMode3D(camera);
                lighting.DrawCube(new(0,-0.15f,3),new(16,0.3f,10),Color.White);
                Raylib.EndMode3D();
                var image=Raylib.LoadImageFromScreen();
                Raylib.EndDrawing();
                try
                {
                    var screen=Raylib.GetWorldToScreen(new(0,0,3),camera);
                    return Raylib.GetImageColor(image,(int)screen.X,(int)screen.Y);
                }
                finally { Raylib.UnloadImage(image); }
            }
            lighting.Update(0.2f,1);
            lighting.SetCharacterCasters(null,null);
            var clear=FloorPixel();
            lighting.SetCharacterCasters(new Vector3(-3,0,3),null);
            var shadow=FloorPixel();
            Assert.True(clear.R>shadow.R+10,
                $"Character should shade firelit floor: clear={clear}, shadow={shadow}");
            lighting.SetCharacterCasters(null,new Vector3(-3,0,3));
            var guardShadow=FloorPixel();
            Assert.True(clear.R>guardShadow.R+10,
                $"Guard should shade firelit floor: clear={clear}, shadow={guardShadow}");
            world.Fireplaces[0].Activate(Vector3.Zero);
            lighting.Update(0.2f,1);
            Assert.Equal(0,FloorPixel().R);
        }
        finally
        {
            Raylib.UnloadModel(hearth);
            Raylib.UnloadModel(enemy);
            Raylib.UnloadModel(player);
            Raylib.CloseWindow();
        }
    }

    [Theory]
    [InlineData("Human/Male/Outfits/Underlayer/Animations/Generated/human_male_underlayer_locomotion.glb",1.78f)]
    [InlineData("Orc/Male/Outfits/Underlayer/Animations/Generated/orc_male_underlayer_locomotion.glb",2.08f)]
    public void CharacterClipsStayAtWorldHeight(string file,float height)
    {
        Raylib.InitWindow(800,600,"CCM animation bounds");
        var path=Path.Combine("Assets","Characters","Library",file);
        var model=Raylib.LoadModel(path);
        try
        {
            Assert.True(model.Transform==Matrix4x4.Identity,$"Model transform: {model.Transform}");
            var character=new CCM.Scenes.GltfCharacter(model,path,height);
            var positions=new[]
            {
                (new Vector3(0,0,0),0f),
                (new Vector3(0,0,0),0.5f),
                (new Vector3(0,0,-0.2f),0.6f),
                (new Vector3(0,0,-0.8f),0.8f),
                (new Vector3(0,0,-1.6f),1f),
                (new Vector3(0,0,-3.2f),1.2f),
                (new Vector3(0,0,-4.8f),1.4f)
            };
            foreach (var (position,time) in positions)
            {
                character.Update(model,position,time);
                var min=character.BoundsMin;
                var max=character.BoundsMax;
                Assert.InRange(max.Y-min.Y,height*0.9f,height*1.1f);
                if (time==0)
                {
                    Assert.InRange(min.Y,-0.02f,0.02f);
                    Assert.InRange(max.Y,height-0.02f,height+0.02f);
                }
                Assert.True(min.Y>-0.1f && max.Y<height+0.1f &&
                    max.X-min.X<height*0.7f && max.Z-min.Z<height*0.7f,
                    $"{file} t={time}: min={min}, max={max}, height={height}");
            }
        }
        finally { Raylib.UnloadModel(model); Raylib.CloseWindow(); }
    }

    [Theory]
    [InlineData("Helmet/human_male_leather_helmet")]
    [InlineData("Torso/human_male_leather_torso")]
    [InlineData("Shoulders/human_male_leather_shoulders")]
    [InlineData("Belt/human_male_leather_belt")]
    [InlineData("Gauntlets/human_male_leather_gauntlets")]
    [InlineData("Greaves/human_male_leather_greaves")]
    [InlineData("ShinBoots/human_male_leather_shinboots")]
    public void LeatherPiecesFollowCharacterPoseAtWorldScale(string piece)
    {
        Raylib.InitWindow(800,600,"CCM leather armour alignment");
        var root=Path.Combine("Assets","Characters","Library","Human","Male");
        var bodyPath=Path.Combine(root,"Outfits","Underlayer","Animations","Generated",
            "human_male_underlayer_locomotion.glb");
        var parts=piece.Split('/');
        var armourPath=Path.Combine(root,"Outfits","Armour","Leather","Pieces",parts[0],
            "Generated",parts[1]+".glb");
        var bodyModel=Raylib.LoadModel(bodyPath);
        var armourModel=Raylib.LoadModel(armourPath);
        try
        {
            var body=new CCM.Scenes.GltfCharacter(bodyModel,bodyPath,1.78f);
            var armour=new CCM.Scenes.GltfCharacter(armourModel,armourPath,1.78f,
                reference:body);
            foreach (var (position,time) in new[]
            {
                (Vector3.Zero,0f),
                (Vector3.Zero,0.5f),
                (new Vector3(0,0,-0.5f),0.7f),
                (new Vector3(0,0,-2.5f),1.0f)
            })
            {
                body.Update(bodyModel,position,time);
                armour.Update(armourModel,position,time,body);
                Assert.True(armour.BoundsMin.Y>=body.BoundsMin.Y-0.12f &&
                    armour.BoundsMax.Y<=body.BoundsMax.Y+0.12f &&
                    armour.BoundsMin.X>=body.BoundsMin.X-0.15f &&
                    armour.BoundsMax.X<=body.BoundsMax.X+0.15f &&
                    armour.BoundsMin.Z>=body.BoundsMin.Z-0.15f &&
                    armour.BoundsMax.Z<=body.BoundsMax.Z+0.15f,
                    $"{piece} t={time}: body={body.BoundsMin}..{body.BoundsMax}, " +
                    $"piece={armour.BoundsMin}..{armour.BoundsMax}");
            }
        }
        finally
        {
            Raylib.UnloadModel(armourModel);
            Raylib.UnloadModel(bodyModel);
            Raylib.CloseWindow();
        }
    }

    [Theory]
    [InlineData("regular.json", "MainMenu")]
    [InlineData("regular-debug.json", "MainMenu")]
    [InlineData("test-level-debug.json", "LevelUp")]
    public void RealWindowLoadsAssetsRoutesAndCleansUp(string profile, string destination)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CCM-native-"+Guid.NewGuid());
        var logger = new RotatingFileLogger(directory);
        var debug = Settings.Required<DebugConfig>(Settings.ConfigPath(profile)).Debug;
        var app = Settings.Required<AppSettings>(Settings.ConfigPath("application.json"));
        var display = new DisplaySettings { Width = 800, Height = 600 };
        try
        {
            new GameApplication(app, debug, new Diagnostics(debug, logger),
                new JsonSettingsStore<DisplaySettings>(Path.Combine(directory, "display.json")), display).Run(smoke: true);
            Assert.False(Raylib.IsWindowReady());
            string log = File.ReadAllText(logger.FilePath);
            Assert.Contains("State InitialLoading -> Splash", log);
            Assert.Contains("State Splash -> Title", log);
            Assert.Contains("State Title -> Loading", log);
            Assert.Contains("Selected controls: Keyboard / mouse", log);
            string[] expectedOrder = ["State InitialLoading -> Splash", "State Splash -> Title",
                "Selected controls:", "State Title -> Loading", "State Loading -> "+destination];
            int previousIndex = -1;
            foreach (string entry in expectedOrder)
            {
                int index = log.IndexOf(entry, StringComparison.Ordinal);
                Assert.True(index > previousIndex, $"Out-of-order startup: {entry}");
                previousIndex = index;
            }
            Assert.Contains("Loaded 1 assets.", log); // presentation catalogue
            Assert.Contains("Loaded 24 assets.", log); // foundation, characters, fireplace and dungeon scenery
            Assert.Contains("State Loading -> "+destination, log);
            Assert.Contains("State MainMenu -> LevelUp", log);
            Assert.Contains("State LevelUp -> Game", log);
            Assert.Contains("State Game -> MainMenu", log);
            Assert.Contains("Clean shutdown.", log);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void TopDownScreenRayReachesTheGroundUnderTheCursor()
    {
        Raylib.InitWindow(800,600,"CCM top-down camera validation");
        try
        {
            var simulation = new DungeonSimulation(new());
            simulation.AdjustZoom(-6);
            var camera = new Camera3D
            {
                Position=simulation.CameraPosition,
                Target=simulation.CameraTarget,
                Up=-Vector3.UnitZ,
                FovY=60,
                Projection=CameraProjection.Perspective
            };
            var ray = Raylib.GetScreenToWorldRay(new(400,300),camera);
            Assert.True(ray.Direction.Y < -0.9f);
            float distance = -ray.Position.Y/ray.Direction.Y;
            var ground = ray.Position+ray.Direction*distance;
            Assert.InRange(Vector3.Distance(ground,simulation.FreeCameraCentre),0,0.05f);
            for (int i=0;i<12;i++) simulation.UpdateTopDown(new(0,1),0.1f);
            camera.Position=simulation.CameraPosition;
            camera.Target=simulation.CameraTarget;
            var door=simulation.World.Interactables[0];
            var screen=Raylib.GetWorldToScreen(door.Bounds.Centre,camera);
            var objectRay=Raylib.GetScreenToWorldRay(screen,camera);
            Assert.Same(door,InteractionQuery.FindVisible(simulation.World,
                objectRay.Position,objectRay.Direction)?.Item);
        }
        finally { Raylib.CloseWindow(); }
    }

    [Fact]
    public void FollowCameraPointerRaySelectsOffCentreEnemy()
    {
        Raylib.InitWindow(800,600,"CCM follow pointer validation");
        try
        {
            var simulation = new DungeonSimulation(new DungeonSettings
                { Combat = new() { AutoPauseOnCombatStart=false } });
            simulation.Player.Position = new(0,0,-6.35f);
            simulation.UpdateAim(new(0,1.2f,0),-Vector3.UnitZ);
            Assert.True(simulation.TryInteractVisible());
            simulation.UpdateCombat(0.1f);
            simulation.UpdateCombat(0.1f);
            simulation.Player.Position = new(0,0,-25);
            simulation.Enemy.Position = new(2,0,-30);
            var camera = new Camera3D
            {
                Position=simulation.CameraPosition,
                Target=simulation.CameraTarget,
                Up=Vector3.UnitY,
                FovY=60,
                Projection=CameraProjection.Perspective
            };
            var screen = Raylib.GetWorldToScreen(simulation.Enemy.Position+new Vector3(0,0.9f,0),camera);
            Assert.True(screen.X > 0 && screen.X < 800);
            Assert.True(screen.Y > 0 && screen.Y < 600);
            Assert.True(Math.Abs(screen.X-400)>20);
            var ray = Raylib.GetScreenToWorldRay(screen,camera);
            Assert.True(simulation.SelectEnemy(ray.Position,ray.Direction));
        }
        finally { Raylib.CloseWindow(); }
    }

    [Fact]
    public void DisplayChangesAndSavedSettingsRoundTripWithRealWindow()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CCM-display-"+Guid.NewGuid());
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
        Raylib.InitWindow(800, 600, "CCM display validation");
        try
        {
            Assert.True(Raylib.IsWindowReady());
            var display = new DisplayManager();
            var windowed = new DisplaySettings { Width = 640, Height = 480 };
            display.Apply(windowed);
            Assert.Equal(windowed, display.Current);
            var size = display.Available(true).Last();
            display.Apply(new() { Width = size.Width, Height = size.Height, Fullscreen = true });
            Assert.True(Raylib.IsWindowFullscreen());
            // Drivers can choose a different actual mode; the application reads it back.
            var actualMode = new Resolution(display.Current.Width, display.Current.Height);
            Assert.Contains(actualMode, display.Available(true));
            display.Apply(windowed);
            Assert.False(Raylib.IsWindowFullscreen());
            Assert.Equal(windowed, display.Current);
            var store = new JsonSettingsStore<DisplaySettings>(Path.Combine(directory, "display.json"));
            store.Save(display.Current);
            Assert.Equal(windowed, store.Load());
        }
        finally
        {
            if (Raylib.IsWindowFullscreen()) Raylib.ToggleFullscreen();
            Raylib.CloseWindow();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}

using System.Numerics;
using CCM.Application;
using CCM.Game;
using Raylib_cs;
namespace CCM.Scenes;

public sealed class GameScene(SessionInput input, DungeonSettings settings, bool debug, Action returnToMenu) : Scene
{
    private readonly DungeonSimulation simulation = new(settings);
    private bool captured, ignoreLook;
    public override void Enter()
    {
        simulation.Reset();
        ignoreLook = true;
        UpdateCapture();
    }
    private void UpdateCapture()
    {
        bool capture = input.UsesMouse && Raylib.IsWindowFocused();
        if (capture == captured) return;
        if (capture) Raylib.DisableCursor(); else Raylib.EnableCursor();
        captured = capture;
        ignoreLook = true;
    }
    public override void Update(float seconds)
    {
        UpdateCapture();
        if (!Raylib.IsWindowFocused()) return;
        if (input.ReturnPressed()) { returnToMenu(); return; }
        simulation.Update(input.Move, ignoreLook ? Vector2.Zero : input.Look, input.UsesMouse, seconds);
        ignoreLook = false;
    }
    public override void Exit()
    {
        if (captured) Raylib.EnableCursor();
        captured = false;
    }
    public override void Render()
    {
        Raylib.ClearBackground(new Color(20,23,30,255));
        var camera = new Camera3D
        {
            Position = simulation.CameraPosition, Target = simulation.CameraTarget,
            Up = Vector3.UnitY, FovY = 60, Projection = CameraProjection.Perspective
        };
        Raylib.BeginMode3D(camera);
        for (int x = -8; x < 8; x += 2)
            for (int z = -8; z < 8; z += 2)
                Floor(new(x+1,-0.15f,z+1), ((x+z)/2 & 1) == 0);
        for (int x = -2; x < 2; x += 2)
            for (int z = -16; z < -8; z += 2) Floor(new(x+1,-0.15f,z+1), ((x+z)/2 & 1) == 0);
        foreach (var block in simulation.World.Solids)
        {
            Color colour = block.Size.Y < 2 ? new Color(117,91,63,255) : new Color(86,93,104,255);
            Raylib.DrawCubeV(block.Centre,block.Size,colour);
            Raylib.DrawCubeWiresV(block.Centre,block.Size,new Color(43,49,59,255));
        }
        var player = simulation.Player;
        Raylib.DrawCubeV(player.Position + new Vector3(0,0.65f,0),new(0.6f,1.1f,0.6f),Color.SkyBlue);
        Raylib.DrawCubeV(player.Position + new Vector3(0,1.4f,0),new(0.5f,0.4f,0.5f),Color.Beige);
        // A bright front marker makes facing visible without a model or animation.
        Raylib.DrawCubeV(player.TransformPoint(new(0,1.4f,-0.31f)),new(0.18f,0.15f,0.18f),Color.Orange);
        if (debug)
        {
            foreach (var block in simulation.World.Solids)
                Raylib.DrawCubeWiresV(block.Centre,block.Size + new Vector3(0.015f),Color.Lime);
            Raylib.DrawCubeWiresV(player.Position+new Vector3(0,0.8f,0),new(0.7f,1.6f,0.7f),Color.Yellow);
            Raylib.DrawLine3D(player.Position+Vector3.UnitY,player.Position+Vector3.UnitY+player.Forward,Color.Red);
        }
        Raylib.EndMode3D();
        string hint = input.ControllerDisconnected ? "Controller disconnected - Escape to return to menu"
            : input.UsesMouse ? "WASD move | Mouse look | Escape menu" : "Left stick move | Right stick look | B menu";
        Raylib.DrawRectangle(0,Raylib.GetScreenHeight()-34,Raylib.GetScreenWidth(),34,new Color(0,0,0,190));
        Raylib.DrawText(hint,16,Raylib.GetScreenHeight()-26,18,Color.RayWhite);
        if (debug)
            Raylib.DrawText($"Position {player.Position.X:0.00}, {player.Position.Y:0.00}, {player.Position.Z:0.00} | Camera {Vector3.Distance(camera.Position,camera.Target):0.00}m",
                8,50,16,Color.Lime);
    }
    private static void Floor(Vector3 centre, bool light) =>
        Raylib.DrawCubeV(centre,new(2,0.3f,2),light ? new Color(54,61,69,255) : new Color(47,53,62,255));
}

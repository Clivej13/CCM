using System.Numerics;
using CCM.Application;
using CCM.Game;
using Raylib_cs;
namespace CCM.Scenes;

public sealed class GameScene(SessionInput input, DungeonSettings settings, bool debug, Action returnToMenu) : Scene
{
    private readonly DungeonSimulation simulation = new(settings);
    private bool captured, ignoreLook;
    private string? feedback;
    private float feedbackSeconds;
    public override void Enter()
    {
        simulation.Reset();
        feedback = null; feedbackSeconds = 0;
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
        feedbackSeconds = Math.Max(0,feedbackSeconds-seconds);
        if (input.ReturnPressed()) { returnToMenu(); return; }
        simulation.Update(input.Move, ignoreLook ? Vector2.Zero : input.Look, input.UsesMouse, seconds);
        simulation.UpdateInteraction(!ignoreLook && input.InteractPressed());
        if (simulation.InteractionMessage is { } message) { feedback = message; feedbackSeconds = 1.5f; }
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
        for (int x = -6; x < 6; x += 2)
            for (int z = -28; z < -16; z += 2)
                Floor(new(x+1,-0.15f,z+1), ((x+z)/2 & 1) == 0);
        foreach (var block in simulation.World.StaticSolids)
        {
            Color colour = block.Size.Y < 2 ? new Color(117,91,63,255) : new Color(86,93,104,255);
            Raylib.DrawCubeV(block.Centre,block.Size,colour);
            Raylib.DrawCubeWiresV(block.Centre,block.Size,new Color(43,49,59,255));
        }
        foreach (var item in simulation.World.Interactables) DrawInteractable(item);
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
        if (simulation.Target is { } highlighted)
            Raylib.DrawCubeWiresV(highlighted.Item.Bounds.Centre,highlighted.Item.Bounds.Size+new Vector3(0.05f),Color.Gold);
        Raylib.EndMode3D();
        if (simulation.Target is { } target)
            DrawPrompt($"[{input.InteractionBinding}] {target.Item.Action}",Raylib.GetScreenHeight()/2+60,Color.Gold);
        if (feedbackSeconds > 0 && feedback is not null)
            DrawPrompt(feedback,Raylib.GetScreenHeight()-70,Color.RayWhite);
        string hint = input.ControllerDisconnected ? "Controller disconnected - Escape to return to menu"
            : input.UsesMouse ? "WASD move | Mouse look | E interact | Escape menu" : "Left stick move | Right stick look | A interact | B menu";
        Raylib.DrawRectangle(0,Raylib.GetScreenHeight()-34,Raylib.GetScreenWidth(),34,new Color(0,0,0,190));
        Raylib.DrawText(hint,16,Raylib.GetScreenHeight()-26,18,Color.RayWhite);
        if (debug)
            Raylib.DrawText($"Position {player.Position.X:0.00}, {player.Position.Y:0.00}, {player.Position.Z:0.00} | Camera {Vector3.Distance(camera.Position,camera.Target):0.00}m",
                8,50,16,Color.Lime);
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
    private static void DrawPrompt(string text,int y,Color colour)
    {
        int width = Raylib.MeasureText(text,20);
        int x = (Raylib.GetScreenWidth()-width)/2;
        Raylib.DrawRectangle(x-12,y-6,width+24,32,new Color(0,0,0,210));
        Raylib.DrawText(text,x,y,20,colour);
    }
    private static void Floor(Vector3 centre, bool light) =>
        Raylib.DrawCubeV(centre,new(2,0.3f,2),light ? new Color(54,61,69,255) : new Color(47,53,62,255));
}

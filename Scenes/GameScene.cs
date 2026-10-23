using System.Numerics;
using CCM.Application;
using CCM.Game;
using Raylib_cs;
namespace CCM.Scenes;

public sealed class GameScene(SessionInput input, DungeonSettings settings, bool debug,
    Action returnToMenu, GameplaySettings? gameplay = null) : Scene
{
    private readonly DungeonSimulation simulation = new(settings, gameplay);
    private bool captured, ignoreLook, rightDragged, rightClickPending;
    private Vector2 rightStart;
    private float rightDragDistance;
    private Vector2 controllerCursor;
    private bool wasTopDown;
    private bool controllerContextMode;
    private string? feedback;
    private float feedbackSeconds;
    public override void Enter()
    {
        simulation.Reset();
        feedback = null; feedbackSeconds = 0;
        ignoreLook = true;
        rightDragged = rightClickPending = false;
        rightDragDistance = 0;
        wasTopDown = false;
        controllerContextMode = false;
        controllerCursor = new(Raylib.GetScreenWidth()/2f,Raylib.GetScreenHeight()/2f);
        UpdateCapture();
    }
    private void UpdateCapture()
    {
        bool capture = input.UsesMouse && !simulation.TopDown &&
            Raylib.IsWindowFocused() && input.AttackHeld;
        if (capture == captured) return;
        if (capture)
        {
            rightStart = Raylib.GetMousePosition();
            rightDragged = false;
            rightDragDistance = 0;
            Raylib.DisableCursor();
        }
        else if (captured)
        {
            Raylib.EnableCursor();
            Raylib.SetMousePosition((int)rightStart.X,(int)rightStart.Y);
            rightClickPending = !simulation.TopDown && Raylib.IsWindowFocused() &&
                !input.AttackHeld && !rightDragged;
        }
        captured = capture;
        ignoreLook = true;
    }
    public override void Update(float seconds)
    {
        UpdateCapture();
        if (!Raylib.IsWindowFocused()) { rightClickPending = false; return; }
        feedbackSeconds = Math.Max(0,feedbackSeconds-seconds);
        if (input.ReturnPressed()) { returnToMenu(); return; }
        if (input.PausePressed())
        {
            simulation.TogglePause();
            ignoreLook = true;
        }
        if (simulation.Paused) { rightClickPending = false; return; }
        simulation.AdjustZoom(input.Zoom*settings.MouseZoomStep);
        if (!input.UsesMouse && input.CycleViewPressed()) simulation.CycleView();
        if (simulation.TopDown != wasTopDown)
        {
            wasTopDown = simulation.TopDown;
            controllerContextMode = false;
            controllerCursor = new(Raylib.GetScreenWidth()/2f,Raylib.GetScreenHeight()/2f);
            ignoreLook = true;
        }
        UpdateCapture();
        simulation.ClearInteractionMessage();
        if (simulation.TopDown)
        {
            simulation.UpdateTopDown(input.Move,seconds);
            if (!input.UsesMouse)
                controllerCursor += input.Look*settings.ControllerCursorSpeed*seconds;
            controllerCursor = Vector2.Clamp(controllerCursor,Vector2.Zero,
                new(Raylib.GetScreenWidth()-1,Raylib.GetScreenHeight()-1));
        }
        else
        {
            if (captured && !ignoreLook)
            {
                rightDragDistance += input.Look.Length();
                if (rightDragDistance > 6) rightDragged = true;
            }
            var look = ignoreLook || input.UsesMouse && !captured
                ? Vector2.Zero : input.Look;
            var move = input.Move;
            float turn = 0;
            if (input.UsesMouse)
            {
                turn = captured ? 0 : move.X;
                move.X = input.Strafe + (captured ? move.X : 0);
            }
            simulation.Update(move,look,input.UsesMouse,seconds,turn,faceCamera:input.UsesMouse);
        }
        var camera = CreateCamera();
        Ray aim = default;
        if (!input.UsesMouse && simulation.TopDown && input.Look.LengthSquared() > 0.01f)
            controllerContextMode = false;
        if (input.UsesMouse || simulation.TopDown && !controllerContextMode)
        {
            Vector2 screenPoint = input.UsesMouse ? Raylib.GetMousePosition() : controllerCursor;
            aim = Raylib.GetScreenToWorldRay(screenPoint,camera);
            simulation.UpdateAim(aim.Position,aim.Direction);
        }
        else simulation.UpdateContextAim();
        if (!input.UsesMouse)
        {
            bool left = input.CycleActionLeftPressed();
            bool right = input.CycleActionRightPressed();
            if (left || right)
                controllerContextMode = simulation.CycleContext(left ? -1 : 1);
        }
        bool combatBeforeFrame = simulation.CombatStarted;
        if (!ignoreLook || rightClickPending)
        {
            bool primary = !ignoreLook && (input.UsesMouse ? input.SelectPressed() : input.InteractPressed());
            if (primary)
            {
                if (!simulation.TopDown && input.UsesMouse)
                    simulation.SelectEnemy(aim.Position,aim.Direction);
                else if (!simulation.TopDown || controllerContextMode)
                    simulation.ActivateContext();
                else if (!input.UsesMouse)
                {
                    if (simulation.SelectEnemy(aim.Position,aim.Direction))
                        simulation.RequestAttack();
                    else if (simulation.VisibleTarget is { } visible)
                        simulation.RequestInteraction(visible.Item);
                    else SetGroundDestination(aim);
                }
                else if (!simulation.TryInteractVisible())
                {
                    if (simulation.SelectEnemy(aim.Position,aim.Direction))
                        simulation.RequestAttack();
                    else SetGroundDestination(aim);
                }
            }
            if (!ignoreLook && input.UsesMouse && !primary && input.InteractPressed())
                simulation.TryInteractVisible();
            bool attack = rightClickPending ||
                (!ignoreLook && input.UsesMouse && simulation.TopDown && input.AttackPressed());
            if (attack)
            {
                if (simulation.TopDown)
                {
                    if (simulation.SelectEnemy(aim.Position,aim.Direction))
                        simulation.RequestAttack();
                    else if (simulation.VisibleTarget is { } visible)
                        simulation.RequestInteraction(visible.Item);
                }
                else if (input.UsesMouse)
                {
                    if (!simulation.TryInteractVisible())
                    {
                        simulation.SelectEnemy(aim.Position,aim.Direction);
                        simulation.RequestAttack();
                    }
                }
            }
            rightClickPending = false;
        }
        simulation.UpdateCombat(seconds, controllerScheme: !input.UsesMouse);
        if (!input.UsesMouse && !combatBeforeFrame && simulation.CombatStarted)
            controllerContextMode = simulation.TopDown;
        if (simulation.InteractionMessage is { } interaction) { feedback = interaction; feedbackSeconds = 1.5f; }
        if (simulation.CombatMessage is { } combat) { feedback = combat; feedbackSeconds = 1.5f; }
        ignoreLook = false;
    }
    private void SetGroundDestination(Ray aim)
    {
        if (aim.Direction.Y >= -0.0001f) return;
        float distance = -aim.Position.Y/aim.Direction.Y;
        if (distance <= 0) return;
        simulation.SetMoveDestination(aim.Position+aim.Direction*distance);
    }
    private Camera3D CreateCamera() => new()
    {
        Position = simulation.CameraPosition, Target = simulation.CameraTarget,
        Up = simulation.TopDown ? -Vector3.UnitZ : Vector3.UnitY,
        FovY = 60, Projection = CameraProjection.Perspective
    };
    public override void Exit()
    {
        if (captured)
        {
            Raylib.EnableCursor();
            Raylib.SetMousePosition((int)rightStart.X,(int)rightStart.Y);
        }
        captured = false;
        rightClickPending = false;
    }
    public override void Render()
    {
        Raylib.ClearBackground(new Color(20,23,30,255));
        var camera = CreateCamera();
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
        Raylib.DrawCubeV(enemy.Position+new Vector3(0,0.9f,0),new(0.9f,1.8f,0.9f),
            enemy.Alive ? new Color(170,58,58,255) : Color.DarkGray);
        if (simulation.EnemySelected && enemy.Alive)
            Raylib.DrawCubeWiresV(enemy.Position+new Vector3(0,0.9f,0),new(1,1.9f,1),Color.Gold);
        if (simulation.MoveDestination is { } destination)
            Raylib.DrawCubeV(destination+new Vector3(0,0.08f,0),new(0.4f,0.12f,0.4f),Color.Gold);
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
        if (simulation.VisibleTarget is { } visible)
        {
            string control = simulation.TopDown ? (input.UsesMouse ? "Left click" : "A")
                : input.UsesMouse ? "Right click" : "A";
            string prompt = simulation.Target is not null
                ? $"{control}: {visible.Item.Action}"
                : simulation.TopDown
                    ? $"{(input.UsesMouse ? "Right click" : "A")}: approach + {visible.Item.Action}"
                    : $"{visible.Item.Name} - move closer";
            if (!input.UsesMouse) prompt += " | LB/RB: choose";
            DrawPrompt(prompt,Raylib.GetScreenHeight()/2+60,
                simulation.Target is not null ? Color.Gold : Color.RayWhite);
        }
        if (!input.UsesMouse && simulation.ContextEnemySelected)
            DrawPrompt("A: attack selected enemy | LB/RB: choose",
                Raylib.GetScreenHeight()/2+60,Color.Gold);
        if (!input.UsesMouse) DrawContextOptions();
        if (simulation.TopDown && !input.UsesMouse && !controllerContextMode)
            Raylib.DrawCircleV(controllerCursor,6,Color.Gold);
        if (feedbackSeconds > 0 && feedback is not null)
            DrawPrompt(feedback,Raylib.GetScreenHeight()-70,Color.RayWhite);
        DrawHealthBar(16,78,player.Health,player.MaxHealth,"Player",Color.SkyBlue);
        if (simulation.CombatStarted)
            DrawHealthBar(Raylib.GetScreenWidth()-236,78,enemy.Health,enemy.MaxHealth,
                simulation.EnemySelected ? "Enemy [selected]" : "Enemy",Color.Red);
        if (simulation.AutoAttackEnabled && enemy.Alive)
            Raylib.DrawText($"Auto attack: {player.Phase} {player.AttackProgress:P0}",16,116,16,Color.Gold);
        if (simulation.CombatStarted && enemy.Alive)
            Raylib.DrawText($"Enemy: {enemy.Phase} {enemy.AttackProgress:P0}",
                Raylib.GetScreenWidth()-236,116,16,Color.Red);
        string hint1 = input.ControllerDisconnected ? "Controller disconnected - Escape to menu"
            : input.UsesMouse
                ? simulation.TopDown ? "WASD pan camera | Wheel zoom | LMB move / use / select"
                    : "W/S move | A/D turn | Q/E strafe | RMB hold look"
                : simulation.TopDown ? "Left stick pan | Right stick cursor | R3 view"
                    : "Left stick move | Right stick look | R3 view";
        string hint2 = input.ControllerDisconnected ? "" : input.UsesMouse
            ? simulation.TopDown ? "RMB approach + use / attack | Space pause | Esc menu"
                : "LMB select | RMB use / attack | F use | Wheel zoom"
            : simulation.TopDown
                ? "A approach / use / attack / move | LB/RB choose | Start pause | B menu"
                : "LB/RB choose | A use / attack | Start pause | B menu";
        Raylib.DrawRectangle(0,Raylib.GetScreenHeight()-56,Raylib.GetScreenWidth(),56,new Color(0,0,0,190));
        Raylib.DrawText(hint1,16,Raylib.GetScreenHeight()-50,16,Color.RayWhite);
        Raylib.DrawText(hint2,16,Raylib.GetScreenHeight()-27,16,Color.RayWhite);
        if (debug)
            Raylib.DrawText($"Position {player.Position.X:0.00}, {player.Position.Y:0.00}, {player.Position.Z:0.00} | Camera {Vector3.Distance(camera.Position,camera.Target):0.00}m",
                8,50,16,Color.Lime);
        if (simulation.Paused)
        {
            Raylib.DrawRectangle(0,0,Raylib.GetScreenWidth(),Raylib.GetScreenHeight(),new Color(0,0,0,160));
            DrawPrompt("PAUSED - Space / Start to resume",Raylib.GetScreenHeight()/2,Color.RayWhite);
        }
    }
    private void DrawContextOptions()
    {
        var options = simulation.AvailableContextActions();
        if (options.Count == 0) return;
        const int fontSize = 16;
        const int gap = 18;
        int width = options.Sum(option => Raylib.MeasureText(option.Label,fontSize)) +
            gap * (options.Count-1) + 32;
        int x = Math.Max(8,(Raylib.GetScreenWidth()-width)/2);
        int y = 148;
        Raylib.DrawRectangle(x-8,y-6,Math.Min(width+16,Raylib.GetScreenWidth()-x),30,
            new Color(0,0,0,190));
        x += 8;
        foreach (var option in options)
        {
            Raylib.DrawText(option.Label,x,y,fontSize,option.Selected ? Color.Gold : Color.RayWhite);
            x += Raylib.MeasureText(option.Label,fontSize)+gap;
        }
    }
    private static void DrawHealthBar(int x,int y,int health,int maximum,string label,Color colour)
    {
        Raylib.DrawText($"{label} {health}/{maximum}",x,y-22,18,Color.RayWhite);
        Raylib.DrawRectangle(x,y,220,14,Color.DarkGray);
        Raylib.DrawRectangle(x,y,(int)(220f*health/maximum),14,colour);
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

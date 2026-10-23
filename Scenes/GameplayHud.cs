using System.Numerics;
using CCM.Application;
using CCM.Game;
using Raylib_cs;

namespace CCM.Scenes;

internal sealed class GameplayHud(SessionInput input,DungeonSimulation simulation,
    GameplayTargeting targeting,bool debug)
{
    public void Render(string? feedback,float feedbackSeconds)
    {
        var camera=simulation.Camera.ToRaylibCamera();
        var player=simulation.Player;
        var enemy=simulation.Enemy;
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
        Raylib.DrawText(input.UsesMouse ? "I: Inventory" : "Y: Inventory",
            16,150,16,Color.RayWhite);
        if (simulation.TopDown && !input.UsesMouse && !targeting.ControllerContextMode)
            Raylib.DrawCircleV(targeting.ControllerCursor,6,Color.Gold);
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
        string hint1 = input.ControllerDisconnected ? "Controller disconnected - Esc: pause menu"
            : input.UsesMouse
                ? simulation.TopDown ? "WASD pan camera | Wheel zoom | LMB move / use / select"
                    : "W/S move | A/D turn | Q/E strafe | RMB hold look"
                : simulation.TopDown ? "Left stick pan | Right stick cursor | R3 view"
                    : "Left stick move | Right stick look | R3 view";
        string hint2 = input.ControllerDisconnected ? "" : input.UsesMouse
            ? simulation.TopDown ? "RMB approach + use / attack | Space combat pause | Esc menu"
                : "LMB select | RMB use / attack | F use | Wheel zoom"
            : simulation.TopDown
                ? "A use / move | LB/RB choose | Back combat pause | Start menu"
                : "LB/RB choose | A use / attack | Back combat pause | Start menu";
        Raylib.DrawRectangle(0,Raylib.GetScreenHeight()-56,Raylib.GetScreenWidth(),56,new Color(0,0,0,190));
        Raylib.DrawText(hint1,16,Raylib.GetScreenHeight()-50,16,Color.RayWhite);
        Raylib.DrawText(hint2,16,Raylib.GetScreenHeight()-27,16,Color.RayWhite);
        if (debug)
            Raylib.DrawText($"Position {player.Position.X:0.00}, {player.Position.Y:0.00}, {player.Position.Z:0.00} | Camera {Vector3.Distance(camera.Position,camera.Target):0.00}m",
                8,50,16,Color.Lime);
        if (simulation.Paused)
        {
            Raylib.DrawRectangle(0,0,Raylib.GetScreenWidth(),Raylib.GetScreenHeight(),new Color(0,0,0,160));
            DrawPrompt("COMBAT PAUSED - Space / Back to resume",Raylib.GetScreenHeight()/2,Color.RayWhite);
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
    private static void DrawPrompt(string text,int y,Color colour)
    {
        int width = Raylib.MeasureText(text,20);
        int x = (Raylib.GetScreenWidth()-width)/2;
        Raylib.DrawRectangle(x-12,y-6,width+24,32,new Color(0,0,0,210));
        Raylib.DrawText(text,x,y,20,colour);
    }
}

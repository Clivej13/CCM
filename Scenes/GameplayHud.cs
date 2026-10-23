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
            string prompt = simulation.Target is not null
                ? $"{visible.Item.Name}: {visible.Item.Action}"
                : $"{visible.Item.Name} - move closer";
            DrawPrompt(prompt,Raylib.GetScreenHeight()/2+60,
                simulation.Target is not null ? Color.Gold : Color.RayWhite);
        }
        if (!input.UsesMouse && simulation.ContextEnemySelected)
            DrawPrompt("Enemy selected",
                Raylib.GetScreenHeight()/2+60,Color.Gold);
        if (!input.UsesMouse) DrawContextOptions();
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
        if (debug)
            Raylib.DrawText($"Position {player.Position.X:0.00}, {player.Position.Y:0.00}, {player.Position.Z:0.00} | Camera {Vector3.Distance(camera.Position,camera.Target):0.00}m",
                8,50,16,Color.Lime);
        if (simulation.Paused)
        {
            Raylib.DrawRectangle(0,0,Raylib.GetScreenWidth(),Raylib.GetScreenHeight(),new Color(0,0,0,160));
            DrawPrompt("COMBAT PAUSED",Raylib.GetScreenHeight()/2,Color.RayWhite);
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

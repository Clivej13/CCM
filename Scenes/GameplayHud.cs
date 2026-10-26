using System.Numerics;
using CCM.Application;
using CCM.Game;
using Raylib_cs;

namespace CCM.Scenes;

internal sealed class GameplayHud(SessionInput input,DungeonSimulation simulation,
    GameplayTargeting targeting,bool debug)
{
    public void Render(string? feedback,float feedbackSeconds,
        int unspentSkillPoints,int unspentAbilityPoints)
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
            DrawPrompt("Enemy selected",Raylib.GetScreenHeight()/2+60,Color.Gold);
        if (!input.UsesMouse) DrawContextOptions();
        if (simulation.TopDown && !input.UsesMouse && !targeting.ControllerContextMode)
            Raylib.DrawCircleV(targeting.ControllerCursor,6,Color.Gold);
        if (feedbackSeconds>0 && feedback is not null)
            DrawPrompt(feedback,Raylib.GetScreenHeight()-(input.UsesMouse ? 110 : 70),
                Color.RayWhite);

        DrawPlayerPanel(player);
        if (unspentSkillPoints>0 || unspentAbilityPoints>0)
            Raylib.DrawText($"Unspent: {unspentSkillPoints} skill | {unspentAbilityPoints} ability",
                16,84,18,Color.Gold);
        int enemyStatusRows=0;
        if (simulation.CombatStarted)
        {
            DrawHealthBar(16,134,enemy.Health,enemy.MaxHealth,
                simulation.EnemySelected ? "Enemy [selected]" : "Enemy",Color.Red);
            if (enemy.Alive)
            {
                enemyStatusRows=Math.Min(6,enemy.Statuses.Active.Count);
                if (enemyStatusRows>0)
                {
                    Raylib.DrawText("ENEMY STATUS",16,158,15,Color.Gold);
                    DrawStatuses(enemy,16,178,220);
                }
            }
        }
        int attackY=enemyStatusRows>0 ? 184+enemyStatusRows*29 : 174;
        if (simulation.AutoAttackEnabled && enemy.Alive)
            Raylib.DrawText($"Auto attack: {player.Phase} {player.AttackProgress:P0}",
                16,attackY,16,Color.Gold);
        if (simulation.CombatStarted && enemy.Alive)
            Raylib.DrawText($"Enemy: {enemy.Phase} {enemy.AttackProgress:P0}",
                16,attackY+23,16,Color.Red);
        if (debug)
            Raylib.DrawText($"Position {player.Position.X:0.00}, {player.Position.Y:0.00}, {player.Position.Z:0.00} | Camera {Vector3.Distance(camera.Position,camera.Target):0.00}m",
                8,50,16,Color.Lime);
        if (simulation.Paused)
        {
            Raylib.DrawRectangle(0,0,Raylib.GetScreenWidth(),Raylib.GetScreenHeight(),
                new Color(0,0,0,160));
            DrawPrompt("COMBAT PAUSED",Raylib.GetScreenHeight()/2,Color.RayWhite);
        }
    }

    private static void DrawPlayerPanel(PlayerEntity player)
    {
        int x=Raylib.GetScreenWidth()-320,y=72;
        Raylib.DrawRectangle(x,y,304,112,new Color(15,20,29,225));
        Raylib.DrawRectangleLines(x,y,304,112,Color.Gold);
        // Temporary block portrait follows the existing asset-free character style.
        Raylib.DrawRectangle(x+12,y+15,60,80,new Color(46,55,68,255));
        Raylib.DrawCircle(x+42,y+39,16,Color.SkyBlue);
        Raylib.DrawRectangle(x+25,y+55,34,30,Color.SkyBlue);
        Raylib.DrawRectangleLines(x+12,y+15,60,80,Color.Gold);
        Raylib.DrawText("PLAYER",x+84,y+8,17,Color.RayWhite);
        DrawMeter(x+84,y+35,204,player.Health,player.MaxHealth,Color.Red,
            $"HP {player.Health}/{player.MaxHealth}");
        DrawMeter(x+84,y+74,204,player.Stamina,player.MaxStamina,Color.Gold,
            $"STA {player.Stamina:0}/{player.MaxStamina}");
        DrawStatuses(player,x,y+120,304);
    }

    private static void DrawMeter(int x,int y,int width,float value,float maximum,
        Color fill,string label)
    {
        Raylib.DrawText(label,x,y-17,15,Color.RayWhite);
        Raylib.DrawRectangle(x,y,width,13,Color.DarkGray);
        Raylib.DrawRectangle(x,y,(int)(width*Math.Clamp(value/maximum,0,1)),13,fill);
        Raylib.DrawRectangleLines(x,y,width,13,Color.RayWhite);
    }

    private static void DrawStatuses(CombatEntity entity,int x,int y,int width)
    {
        int row=0;
        foreach (var status in entity.Statuses.Active.OrderBy(s=>s.Definition.Name))
        {
            if (row>=6) break;
            int top=y+row++*29;
            Color colour=status.Definition.DisplayTone switch
            {
                StatusTone.Beneficial => Color.Lime,
                StatusTone.Dangerous => Color.Red,
                _ => Color.Gold
            };
            Raylib.DrawRectangle(x,top,width,25,new Color(15,20,29,225));
            Raylib.DrawRectangleLines(x,top,width,25,colour);
            Raylib.DrawText(status.Definition.Name,x+8,top+4,16,colour);
            string timer=$"{status.RemainingSeconds:0.0}s";
            Raylib.DrawText(timer,x+width-Raylib.MeasureText(timer,16)-8,top+4,16,
                Color.RayWhite);
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

    private static void DrawPrompt(string value,int y,Color colour)
    {
        const int font=20;
        string message=value;
        int maxWidth=Raylib.GetScreenWidth()-40;
        while (message.Length>1 && Raylib.MeasureText(message,font)>maxWidth)
            message=message[..^1];
        int width=Raylib.MeasureText(message,font);
        int x=(Raylib.GetScreenWidth()-width)/2;
        Raylib.DrawRectangle(x-12,y-6,width+24,32,new Color(0,0,0,210));
        Raylib.DrawText(message,x,y,font,colour);
    }
}

using System.Numerics;
using CCM.Game;
using Raylib_cs;

namespace CCM.Scenes;

internal static class FireplaceFlame
{
    public static float Intensity(FireplaceInteractable fireplace,float seconds)
    {
        if (!fireplace.IsLit) return 0;
        float phase=seconds+fireplace.PhaseOffset;
        return Math.Clamp(0.78f+0.12f*MathF.Sin(phase*13f)+
            0.08f*MathF.Sin(phase*23f+1.2f),0.5f,1f);
    }

    public static void Draw(FireplaceInteractable fireplace,float seconds)
    {
        if (!fireplace.IsLit) return;
        Vector3 origin=fireplace.FireOrigin;
        float flicker=Intensity(fireplace,seconds);
        seconds+=fireplace.PhaseOffset;
        Raylib.DrawSphere(origin+new Vector3(0,0.11f,0),0.16f,
            new Color(255,105,27,230));
        for (int i=0;i<18;i++)
        {
            float phase=seconds*1.6f+i*0.618034f;
            float age=phase-MathF.Floor(phase);
            float x=MathF.Sin(i*17.7f+seconds*5f)*0.24f*(0.4f+age);
            float z=MathF.Sin(i*11.3f+seconds*3f)*0.12f;
            float radius=(0.10f-0.055f*age)*(0.7f+0.3f*flicker);
            var colour=age<0.4f ? new Color(255,204,76,220)
                : age<0.75f ? new Color(255,132,32,190)
                    : new Color(190,52,22,120);
            Raylib.DrawSphere(origin+new Vector3(x,age*0.82f,z),radius,colour);
        }
    }
}

using System.Numerics;
using CCM.Game;
using Raylib_cs;

namespace CCM.Scenes;

internal static class TorchFlame
{
    public static float Intensity(TorchInteractable torch,float seconds)
    {
        if (!torch.IsLit) return 0;
        float phase=seconds+torch.PhaseOffset;
        return Math.Clamp(0.88f+0.09f*MathF.Sin(phase*11f)+
            0.05f*MathF.Sin(phase*19f+0.7f),0.7f,1.05f);
    }

    public static void Draw(TorchInteractable torch,float seconds)
    {
        if (!torch.IsLit) return;
        Vector3 origin=torch.FireOrigin;
        float intensity=Intensity(torch,seconds);
        Raylib.DrawSphere(origin,0.12f,new Color(255,185,65,240));
        for (int i=0;i<9;i++)
        {
            float phase=(seconds+torch.PhaseOffset)*2f+i*0.618034f;
            float age=phase-MathF.Floor(phase);
            float x=MathF.Sin(i*13.7f+seconds*4f)*0.085f;
            float z=MathF.Sin(i*9.3f+seconds*3f)*0.075f;
            float radius=(0.075f-0.04f*age)*intensity;
            Color colour=age<0.55f
                ? new Color(255,205,75,225) : new Color(255,100,25,170);
            Raylib.DrawSphere(origin+new Vector3(x,age*0.38f,z),radius,colour);
        }
    }
}

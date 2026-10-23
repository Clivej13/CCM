using System.Numerics;
using Raylib_cs;

namespace CCM.Game;

/// <summary>Owns both camera modes; the simulation supplies the current player position.</summary>
public sealed class DungeonCamera(DungeonSettings settings, DungeonWorld world, Func<Vector3> playerPosition)
{
    private Vector3 freeCentre;
    public float Yaw { get; private set; }
    public float Pitch { get; private set; }
    public float ZoomDistance { get; private set; }
    public bool TopDown => ZoomDistance >= settings.TopDownThreshold;
    public Vector3 FreeCentre => freeCentre;
    private Vector3 Anchor => playerPosition() + new Vector3(0,1.2f,0);
    public Vector3 Target => TopDown ? freeCentre
        : Anchor + new Vector3(-MathF.Sin(Yaw),0,-MathF.Cos(Yaw)) * 1.5f - new Vector3(0,0.3f,0);
    public Vector3 Position => TopDown ? freeCentre + Vector3.UnitY * ZoomDistance
        : world.CameraPosition(Anchor, Anchor +
            new Vector3(MathF.Sin(Yaw)*MathF.Cos(Pitch),MathF.Sin(Pitch),
                MathF.Cos(Yaw)*MathF.Cos(Pitch))*ZoomDistance);

    public void Reset()
    {
        freeCentre = playerPosition();
        ZoomDistance = settings.CameraDistance;
        Yaw = 0;
        Pitch = settings.CameraPitch;
    }

    /// <returns>Whether the camera changed between follow and top-down modes.</returns>
    public bool AdjustZoom(float amount)
    {
        if (!float.IsFinite(amount) || amount == 0) return false;
        bool wasTopDown = TopDown;
        ZoomDistance = Math.Clamp(ZoomDistance-amount,2f,settings.MaxZoomDistance);
        if (TopDown != wasTopDown) freeCentre = playerPosition();
        return TopDown != wasTopDown;
    }

    public bool CycleView()
    {
        float next = ZoomDistance < settings.WideCameraDistance - 0.01f
            ? settings.WideCameraDistance
            : ZoomDistance < settings.TopDownThreshold - 0.01f
                ? settings.TopDownCameraDistance : settings.CameraDistance;
        return AdjustZoom(ZoomDistance-next);
    }

    public void Pan(Vector2 pan,float seconds)
    {
        if (!TopDown || !float.IsFinite(seconds) || seconds <= 0 ||
            !float.IsFinite(pan.X) || !float.IsFinite(pan.Y)) return;
        seconds = Math.Min(seconds,0.1f);
        if (pan.LengthSquared()>1) pan=Vector2.Normalize(pan);
        freeCentre.X = Math.Clamp(freeCentre.X+pan.X*settings.FreeCameraSpeed*seconds,-10,10);
        freeCentre.Z = Math.Clamp(freeCentre.Z-pan.Y*settings.FreeCameraSpeed*seconds,-40,8);
    }

    public void Rotate(Vector2 look,bool mouseLook,float seconds,float turn)
    {
        float sensitivity = mouseLook ? settings.MouseSensitivity : settings.ControllerLookSpeed*seconds;
        Yaw = MathF.IEEERemainder(Yaw-look.X*sensitivity-
            turn*settings.KeyboardTurnSpeed*seconds,2*MathF.PI);
        Pitch = Math.Clamp(Pitch+look.Y*sensitivity,-0.18f,1.25f);
    }

    public Camera3D ToRaylibCamera() => new()
    {
        Position=Position,Target=Target,
        Up=TopDown ? -Vector3.UnitZ : Vector3.UnitY,
        FovY=60,Projection=CameraProjection.Perspective
    };
}

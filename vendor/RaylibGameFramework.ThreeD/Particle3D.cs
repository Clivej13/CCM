using System.Numerics;
using Raylib_cs;

namespace RaylibGameFramework.ThreeD;

public struct Particle3D
{
    public Vector3 Position;
    public Vector3 Velocity;
    public Vector3 Acceleration;
    public float Age;
    public float Lifetime;
    public float Size;
    public float Rotation;
    public Color Color;
    public bool Active;

    public readonly float NormalizedAge => Lifetime <= 0 ? 1f : Math.Clamp(Age / Lifetime, 0, 1);
}

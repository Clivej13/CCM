using System.Numerics;

namespace RaylibGameFramework.ThreeD;

/// <summary>A reusable position, quaternion rotation, and non-uniform scale.</summary>
public struct Transform3D
{
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public Vector3 Scale { get; set; }

    public Transform3D(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
    }

    public static Transform3D Identity => new(Vector3.Zero, Quaternion.Identity, Vector3.One);

    /// <summary>Transforms a point from this transform's local space into world space.</summary>
    public readonly Vector3 TransformPoint(Vector3 localPoint) =>
        Vector3.Transform(localPoint, WorldMatrix);

    /// <summary>Transforms a direction without applying position or scale.</summary>
    public readonly Vector3 TransformDirection(Vector3 localDirection) =>
        Vector3.Transform(localDirection, Rotation);

    /// <summary>The System.Numerics matrix used for local-to-world rendering and geometry.</summary>
    public readonly Matrix4x4 WorldMatrix =>
        Matrix4x4.CreateScale(Scale) *
        Matrix4x4.CreateFromQuaternion(Rotation) *
        Matrix4x4.CreateTranslation(Position);

    public readonly Vector3 Forward => TransformDirection(-Vector3.UnitZ);
    public readonly Vector3 Up => TransformDirection(Vector3.UnitY);
    public readonly Vector3 Right => TransformDirection(Vector3.UnitX);
}

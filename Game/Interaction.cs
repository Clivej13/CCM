using System.Numerics;
namespace CCM.Game;

public enum InteractionKind { Door, Box }

/// <summary>State and visible bounds for the two prototype interactables.</summary>
public sealed class Interactable(InteractionKind kind)
{
    public InteractionKind Kind { get; } = kind;
    public bool IsOpen { get; private set; }
    public string Name => Kind == InteractionKind.Door ? "Door" : "Box";
    public string Action => (IsOpen ? "Close " : "Open ") + Name.ToLowerInvariant();
    public Block ClosedBounds => Kind == InteractionKind.Door
        ? new(new(0,1.5f,-8.5f),new(4,3,0.3f))
        : new(new(3.1f,0.6f,-7.2f),new(1.4f,1.2f,1.4f));
    public Block Bounds => Kind == InteractionKind.Door && IsOpen
        ? new(new(-1.85f,1.5f,-10.35f),new(0.3f,3,4))
        : ClosedBounds;
    public void Reset() => IsOpen = false;
    public bool Activate(Vector3 playerFeet)
    {
        // Both door poses must be clear before an instant open/close; never trap the player.
        if (Kind == InteractionKind.Door)
        {
            Block destination = IsOpen ? ClosedBounds : new(new(-1.85f,1.5f,-10.35f),new(0.3f,3,4));
            if (DungeonWorld.OverlapsPlayer(destination,playerFeet)) return false;
        }
        IsOpen = !IsOpen;
        return true;
    }
}

public readonly record struct InteractionTarget(Interactable Item, Vector3 Point);

public static class InteractionQuery
{
    /// <summary>Nearest visible centre-ray hit only, then player reach and line of sight.</summary>
    public static InteractionTarget? Find(DungeonWorld world, Vector3 eye, Vector3 camera, Vector3 direction, float range)
    {
        if (!float.IsFinite(range) || range <= 0 || !Finite(eye) || !Finite(camera) || !Finite(direction) ||
            direction.LengthSquared() < 0.000001f) return null;
        var ray = Vector3.Normalize(direction) * 100;
        float nearest = 1;
        Interactable? selected = null;
        // Test walls first: ties at a wall face are occluded, not actionable.
        foreach (var block in world.StaticSolids)
            if (DungeonWorld.SegmentHit(camera,ray,block.Min,block.Max,out float hit) && hit < nearest)
                nearest = hit;
        foreach (var item in world.Interactables)
            if (DungeonWorld.SegmentHit(camera,ray,item.Bounds.Min,item.Bounds.Max,out float hit) && hit < nearest)
            {
                nearest = hit;
                selected = item;
            }
        if (selected is null) return null;
        var point = camera + ray * nearest;
        if (Vector3.Distance(eye,point) > range) return null;
        foreach (var block in world.StaticSolids)
            if (Blocked(eye,point,block)) return null;
        foreach (var item in world.Interactables)
            if (item != selected && Blocked(eye,point,item.Bounds)) return null;
        return new(selected,point);
    }
    private static bool Blocked(Vector3 eye,Vector3 point,Block block) =>
        DungeonWorld.SegmentHit(eye,point-eye,block.Min,block.Max,out float hit) && hit < 0.999f;
    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}

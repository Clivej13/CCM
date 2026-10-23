using System.Numerics;
namespace CCM.Game;

public enum InteractionKind { Door, Chest }

/// <summary>World objects expose only their common targeting and activation contract.</summary>
public abstract class Interactable
{
    public abstract InteractionKind Kind { get; }
    public bool IsOpen { get; protected set; }
    public abstract string Name { get; }
    public virtual string Action => (IsOpen ? "Close " : "Open ") + Name.ToLowerInvariant();
    public abstract Block ClosedBounds { get; }
    public abstract Block Bounds { get; }
    public virtual void Reset() => IsOpen=false;
    public abstract bool Activate(Vector3 playerFeet);
}

public sealed class DoorInteractable : Interactable
{
    public override InteractionKind Kind => InteractionKind.Door;
    public override string Name => "Door";
    public override Block ClosedBounds => new(new(0,1.5f,-8.5f),new(4,3,0.3f));
    public override Block Bounds => IsOpen
        ? new(new(-1.85f,1.5f,-10.35f),new(0.3f,3,4)) : ClosedBounds;
    public override bool Activate(Vector3 playerFeet)
    {
        // Check the destination pose before opening or closing.
        Block destination=IsOpen ? ClosedBounds
            : new(new(-1.85f,1.5f,-10.35f),new(0.3f,3,4));
        if (DungeonWorld.OverlapsPlayer(destination,playerFeet)) return false;
        IsOpen=!IsOpen;
        return true;
    }
}

public sealed class ChestInteractable : Interactable
{
    private readonly ItemStack[] startingContents;
    public ChestInteractable(IEnumerable<ItemStack>? contents=null)
    {
        startingContents=contents?.ToArray() ?? [];
        Contents=new Inventory(startingContents);
    }
    public Inventory Contents { get; }
    public override InteractionKind Kind => InteractionKind.Chest;
    public override string Name => "Chest";
    public override string Action => IsOpen ? "Search chest" : "Open chest";
    public override Block ClosedBounds => new(new(3.1f,0.6f,-7.2f),new(1.4f,1.2f,1.4f));
    public override Block Bounds => ClosedBounds;
    public override void Reset()
    {
        base.Reset();
        Contents.Reset(startingContents);
    }
    public override bool Activate(Vector3 playerFeet)
    {
        IsOpen=true;
        return true;
    }
}

public readonly record struct InteractionTarget(Interactable Item, Vector3 Point);

public static class InteractionQuery
{
    public static InteractionTarget? Find(DungeonWorld world, Vector3 eye, Vector3 camera, Vector3 direction, float range)
    {
        var target = FindVisible(world,camera,direction);
        return target is { } found && CanReach(world,eye,found.Item,range) ? found : null;
    }

    /// <summary>Nearest object under a screen ray, independent of player reach.</summary>
    public static InteractionTarget? FindVisible(DungeonWorld world, Vector3 camera, Vector3 direction)
    {
        if (!Finite(camera) || !Finite(direction) || direction.LengthSquared() < 0.000001f) return null;
        var ray = Vector3.Normalize(direction) * 1000;
        float nearest = 1;
        Interactable? selected = null;
        foreach (var block in world.StaticSolids)
            if (DungeonWorld.SegmentHit(camera,ray,block.Min,block.Max,out float hit) && hit < nearest)
                nearest = hit;
        foreach (var item in world.Interactables)
            if (DungeonWorld.SegmentHit(camera,ray,item.Bounds.Min,item.Bounds.Max,out float hit) && hit < nearest)
            {
                nearest = hit;
                selected = item;
            }
        return selected is null ? null : new(selected,camera+ray*nearest);
    }

    /// <summary>Every reachable action, including choices outside the current camera direction.</summary>
    public static IReadOnlyList<InteractionTarget> FindReachable(DungeonWorld world, Vector3 eye, float range)
    {
        if (!Finite(eye) || !float.IsFinite(range) || range <= 0) return [];
        var choices = new List<InteractionTarget>();
        foreach (var item in world.Interactables)
        {
            if (!CanReach(world,eye,item,range)) continue;
            var box = item.Bounds;
            var point = new Vector3(Math.Clamp(eye.X,box.Min.X,box.Max.X),
                Math.Clamp(eye.Y,box.Min.Y,box.Max.Y),
                Math.Clamp(eye.Z,box.Min.Z,box.Max.Z));
            choices.Add(new(item,point));
        }
        return choices;
    }

    /// <summary>Default controller action follows camera direction; D-pad can cycle all reachable actions.</summary>
    public static InteractionTarget? FindNearby(DungeonWorld world, Vector3 eye, Vector3 forward, float range)
    {
        if (!Finite(forward)) return null;
        forward.Y = 0;
        if (forward.LengthSquared() < 0.000001f) return null;
        forward = Vector3.Normalize(forward);
        InteractionTarget? best = null;
        float bestScore = float.MaxValue;
        foreach (var choice in FindReachable(world,eye,range))
        {
            var offset = new Vector3(choice.Point.X-eye.X,0,choice.Point.Z-eye.Z);
            float distance = offset.Length();
            float facing = distance < 0.1f ? 1 : Vector3.Dot(offset/distance,forward);
            if (facing < 0.25f) continue;
            // Facing wins when two nearby actions overlap; distance breaks close ties.
            float score = distance + (1-facing)*range*4;
            if (score >= bestScore) continue;
            bestScore = score;
            best = choice;
        }
        return best;
    }

    /// <summary>Reach is measured from the player's ground position to the nearest object footprint.</summary>
    public static bool CanReach(DungeonWorld world, Vector3 eye, Interactable item, float range)
    {
        if (!Finite(eye) || !float.IsFinite(range) || range <= 0) return false;
        var box = item.Bounds;
        var closest = new Vector3(Math.Clamp(eye.X,box.Min.X,box.Max.X),
            Math.Clamp(eye.Y,box.Min.Y,box.Max.Y),
            Math.Clamp(eye.Z,box.Min.Z,box.Max.Z));
        if (Vector2.Distance(new(eye.X,eye.Z),new(closest.X,closest.Z)) > range) return false;
        foreach (var block in world.StaticSolids)
            if (Blocked(eye,closest,block)) return false;
        foreach (var other in world.Interactables)
            if (other != item && Blocked(eye,closest,other.Bounds)) return false;
        return true;
    }
    private static bool Blocked(Vector3 eye,Vector3 point,Block block) =>
        DungeonWorld.SegmentHit(eye,point-eye,block.Min,block.Max,out float hit) && hit < 0.999f;
    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}

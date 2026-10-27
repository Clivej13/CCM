using System.Numerics;
namespace CCM.Game;

public readonly record struct Block(Vector3 Centre, Vector3 Size)
{
    public Vector3 Min => Centre - Size / 2;
    public Vector3 Max => Centre + Size / 2;
}

/// <summary>Two rooms joined by a north corridor and an interactive door. Floors are flat; no jumping or stairs yet.</summary>
public sealed class DungeonWorld
{
    public Vector3 Spawn => new(0, 0, 4);
    public IReadOnlyList<Block> StaticSolids { get; } =
    [
        new(new(-8.5f,1.5f,0),new(1,3,18)), new(new(8.5f,1.5f,0),new(1,3,18)),
        new(new(0,1.5f,8.5f),new(16,3,1)),
        new(new(-5,1.5f,-8.5f),new(6,3,1)), new(new(5,1.5f,-8.5f),new(6,3,1)),
        new(new(-2.5f,1.5f,-12),new(1,3,8)), new(new(2.5f,1.5f,-12),new(1,3,8)),
        new(new(-6,1.5f,-16.5f),new(8,3,1)), new(new(6,1.5f,-16.5f),new(8,3,1)),
        new(new(-10.5f,1.5f,-28),new(1,3,24)), new(new(10.5f,1.5f,-28),new(1,3,24)),
        new(new(0,1.5f,-40.5f),new(22,3,1)),
        new(new(-4,0.6f,-27),new(2,1.2f,2)),
        new(new(5,0.7f,-33),new(2,1.4f,2)),
        new(new(-4,1.5f,-3),new(1.2f,3,1.2f)), new(new(4,1.5f,-3),new(1.2f,3,1.2f)),
        new(new(-3,0.55f,2),new(2,1.1f,1.5f)), new(new(3,0.55f,1),new(1.5f,1.1f,2))
    ];
    public IReadOnlyList<Interactable> Interactables { get; }
    public IReadOnlyList<FireplaceInteractable> Fireplaces { get; }
    public DungeonWorld(IEnumerable<ItemStack>? chestContents=null)
    {
        Fireplaces=
        [
            new(new(-7.55f,0,3),90,0),
            new(new(1.55f,0,-12),-90,0.9f),
            new(new(5,0,-39.65f),0,1.8f)
        ];
        Interactables=[new DoorInteractable(),new ChestInteractable(chestContents),..Fireplaces];
    }
    public IEnumerable<Block> Solids => StaticSolids.Concat(Interactables.Select(item => item.Bounds));
    public void Reset() { foreach (var item in Interactables) item.Reset(); }
    public bool ClearLine(Vector3 from, Vector3 to)
    {
        foreach (var block in Solids)
            if (SegmentHit(from,to-from,block.Min,block.Max,out float hit) && hit < 0.999f)
                return false;
        return true;
    }
    public static bool OverlapsPlayer(Block box, Vector3 feet) =>
        feet.Y < box.Max.Y && feet.Y+1.6f > box.Min.Y &&
        feet.X > box.Min.X - PlayerRadius && feet.X < box.Max.X + PlayerRadius &&
        feet.Z > box.Min.Z - PlayerRadius && feet.Z < box.Max.Z + PlayerRadius;
    public const float PlayerRadius = 0.35f;
    public bool CanStand(Vector3 feet)
    {
        foreach (var box in Solids)
        {
            if (OverlapsPlayer(box,feet))
                return false;
        }
        return true;
    }
    public Vector3 Move(Vector3 position, Vector3 displacement)
    {
        // Small conservative steps prevent crossing thin walls; separate axes slide along them.
        int count = Math.Max(1, (int)MathF.Ceiling(displacement.Length() / 0.1f));
        var step = displacement / count;
        for (int i = 0; i < count; i++)
        {
            var next = position + new Vector3(step.X, 0, 0);
            if (CanStand(next)) position = next;
            next = position + new Vector3(0, 0, step.Z);
            if (CanStand(next)) position = next;
        }
        position.Y = 0;
        return position;
    }
    public Vector3 CameraPosition(Vector3 target, Vector3 desired)
    {
        var delta = desired - target;
        float fraction = 1;
        foreach (var block in Solids)
            if (SegmentHit(target, delta, block.Min - new Vector3(0.2f), block.Max + new Vector3(0.2f), out float hit))
                fraction = Math.Min(fraction, Math.Max(0, hit - 0.01f));
        return target + delta * fraction;
    }
    public static bool SegmentHit(Vector3 origin, Vector3 direction, Vector3 min, Vector3 max, out float hit)
    {
        float enter = 0, exit = 1;
        for (int axis = 0; axis < 3; axis++)
        {
            float o = axis == 0 ? origin.X : axis == 1 ? origin.Y : origin.Z;
            float d = axis == 0 ? direction.X : axis == 1 ? direction.Y : direction.Z;
            float lo = axis == 0 ? min.X : axis == 1 ? min.Y : min.Z;
            float hi = axis == 0 ? max.X : axis == 1 ? max.Y : max.Z;
            if (Math.Abs(d) < 0.00001f)
            {
                if (o < lo || o > hi) { hit = 0; return false; }
                continue;
            }
            float a = (lo-o)/d, b = (hi-o)/d;
            enter = Math.Max(enter, Math.Min(a,b));
            exit = Math.Min(exit, Math.Max(a,b));
            if (enter > exit) { hit = 0; return false; }
        }
        hit = enter;
        return true;
    }
}

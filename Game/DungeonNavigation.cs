using System.Numerics;

namespace CCM.Game;

/// <summary>Small grid route planner for click-to-move around dungeon walls and props.</summary>
public static class DungeonNavigation
{
    private const float Step = 0.5f;
    private const float MinX = -10;
    private const float MinZ = -40;
    private const int Columns = 41;
    private const int Rows = 97;

    public static bool IsFloor(Vector3 point) =>
        (Math.Abs(point.X) <= 8 && point.Z >= -8 && point.Z <= 8) ||
        (Math.Abs(point.X) <= 2 && point.Z >= -16 && point.Z <= -8) ||
        (Math.Abs(point.X) <= 10 && point.Z >= -40 && point.Z <= -16);

    public static IReadOnlyList<Vector3> FindPath(DungeonWorld world, Vector3 start, Vector3 destination)
    {
        destination.Y = 0;
        if (!IsFloor(destination) || !world.CanStand(destination)) return [];
        int first = ClosestCell(world,start);
        int last = ClosestCell(world,destination);
        if (first < 0 || last < 0) return [];
        var previous = Enumerable.Repeat(-1, Columns*Rows).ToArray();
        var visited = new bool[previous.Length];
        var queue = new Queue<int>();
        queue.Enqueue(first);
        visited[first] = true;
        while (queue.Count > 0 && !visited[last])
        {
            int current = queue.Dequeue();
            int x = current % Columns, z = current / Columns;
            Visit(x+1,z); Visit(x-1,z); Visit(x,z+1); Visit(x,z-1);
            void Visit(int nextX,int nextZ)
            {
                if (nextX < 0 || nextX >= Columns || nextZ < 0 || nextZ >= Rows) return;
                int next = nextZ*Columns+nextX;
                if (visited[next]) return;
                var point = Cell(next);
                if (!IsFloor(point) || !world.CanStand(point)) return;
                visited[next] = true;
                previous[next] = current;
                queue.Enqueue(next);
            }
        }
        if (!visited[last]) return [];
        var path = new List<Vector3>();
        for (int cell=last; cell!=first; cell=previous[cell]) path.Add(Cell(cell));
        path.Reverse();
        if (path.Count == 0 || Vector3.Distance(path[^1],destination) > 0.05f)
            path.Add(destination);
        return path;
    }

    /// <summary>Finds a standable point where the chosen object can actually be used.</summary>
    public static IReadOnlyList<Vector3> FindPathToInteraction(
        DungeonWorld world, Vector3 start, Interactable item, float range)
    {
        if (!float.IsFinite(range) || range <= 0) return [];
        return FindPathToGoal(world,start,point => InteractionQuery.CanReach(
            world,point+new Vector3(0,1.2f,0),item,range));
    }

    /// <summary>Finds the closest reachable firing position with a clear melee line.</summary>
    public static IReadOnlyList<Vector3> FindPathToRange(
        DungeonWorld world, Vector3 start, Vector3 target, float range)
    {
        if (!float.IsFinite(range) || range <= 0) return [];
        return FindPathToGoal(world,start,point =>
            Vector2.Distance(new(point.X,point.Z),new(target.X,target.Z)) <= range-0.25f &&
            world.ClearLine(point+Vector3.UnitY,target+Vector3.UnitY));
    }

    private static IReadOnlyList<Vector3> FindPathToGoal(
        DungeonWorld world, Vector3 start, Func<Vector3,bool> goal)
    {
        int first = ClosestCell(world,start);
        if (first < 0) return [];
        var previous = Enumerable.Repeat(-1, Columns*Rows).ToArray();
        var visited = new bool[previous.Length];
        var queue = new Queue<int>();
        queue.Enqueue(first);
        visited[first] = true;
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            var point = Cell(current);
            if (goal(point))
            {
                var path = new List<Vector3>();
                for (int cell=current; cell!=first; cell=previous[cell]) path.Add(Cell(cell));
                path.Reverse();
                if (path.Count == 0 && Vector3.DistanceSquared(start,point) > 0.0025f)
                    path.Add(point);
                return path;
            }
            int x = current % Columns, z = current / Columns;
            Visit(x+1,z); Visit(x-1,z); Visit(x,z+1); Visit(x,z-1);
            void Visit(int nextX,int nextZ)
            {
                if (nextX < 0 || nextX >= Columns || nextZ < 0 || nextZ >= Rows) return;
                int next = nextZ*Columns+nextX;
                if (visited[next]) return;
                var candidate = Cell(next);
                if (!IsFloor(candidate) || !world.CanStand(candidate)) return;
                visited[next] = true;
                previous[next] = current;
                queue.Enqueue(next);
            }
        }
        return [];
    }

    private static int ClosestCell(DungeonWorld world, Vector3 point)
    {
        int centreX = (int)MathF.Round((point.X-MinX)/Step);
        int centreZ = (int)MathF.Round((point.Z-MinZ)/Step);
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int dz=-2;dz<=2;dz++)
            for (int dx=-2;dx<=2;dx++)
            {
                int x=centreX+dx,z=centreZ+dz;
                if (x<0 || x>=Columns || z<0 || z>=Rows) continue;
                int cell=z*Columns+x;
                var candidate=Cell(cell);
                if (!IsFloor(candidate) || !world.CanStand(candidate)) continue;
                float distance=Vector3.DistanceSquared(candidate,point);
                if (distance<bestDistance) { bestDistance=distance; best=cell; }
            }
        return best;
    }
    private static Vector3 Cell(int index) =>
        new(MinX+(index%Columns)*Step,0,MinZ+(index/Columns)*Step);
}

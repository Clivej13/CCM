using System.Numerics;

namespace CCM.Game;

public readonly record struct ContextActionOption(string Label, bool Selected);

public sealed class DungeonSimulation
{
    private readonly DungeonSettings settings;
    private readonly GameplaySettings gameplay;
    private readonly ItemCatalogue items;
    private readonly List<Vector3> movementPath = [];
    private Interactable? pendingInteraction;
    private int waypoint;
    private bool attackRequested;
    private float chaseRepathSeconds;
    private Vector3 chaseEnemyPosition;
    private Vector3 aimOrigin, aimDirection;
    private bool contextualAim;
    private Interactable? chosenContext;
    private bool chosenEnemy;
    private bool doorJustOpened;
    public DungeonWorld World { get; }
    public PlayerEntity Player { get; private set; }
    public EnemyEntity Enemy { get; private set; }
    public InteractionTarget? VisibleTarget { get; private set; }
    public InteractionTarget? Target { get; private set; }
    public Vector3? MoveDestination => waypoint < movementPath.Count ? movementPath[^1] : null;
    public string? InteractionMessage { get; private set; }
    public ChestInteractable? ChestOpened { get; private set; }
    public string? CombatMessage { get; private set; }
    public bool CombatStarted { get; private set; }
    public bool Paused { get; private set; }
    public bool EnemySelected { get; private set; }
    public bool AutoAttackEnabled => attackRequested;
    public bool ContextEnemySelected => chosenEnemy && EnemySelected && Enemy.Alive;
    public DungeonCamera Camera { get; }
    public float Yaw => Camera.Yaw;
    public float Pitch => Camera.Pitch;
    public float ZoomDistance => Camera.ZoomDistance;
    public bool TopDown => Camera.TopDown;
    public Vector3 FreeCameraCentre => Camera.FreeCentre;
    private Vector3 CameraAnchor => Player.Position + new Vector3(0,1.2f,0);
    public Vector3 CameraTarget => Camera.Target;
    public Vector3 CameraPosition => Camera.Position;

    public DungeonSimulation(DungeonSettings settings, GameplaySettings? gameplay = null,
        ItemCatalogue? items=null,IEnumerable<ItemStack>? chestContents=null)
    {
        settings.Validate();
        this.settings = settings;
        this.gameplay = gameplay ?? new GameplaySettings();
        this.items=items ?? new ItemCatalogue();
        World=new DungeonWorld(chestContents);
        Player = new(World.Spawn,settings.Combat,this.items);
        Enemy = new(new(0,0,-30),settings.Combat);
        Camera = new DungeonCamera(settings,World,()=>Player.Position);
        Reset();
    }
    public void Reset()
    {
        World.Reset();
        Player = new(World.Spawn,settings.Combat,items);
        Enemy = new(new(0,0,-30),settings.Combat);
        ChestOpened=null;
        VisibleTarget = Target = null;
        InteractionMessage = CombatMessage = null;
        CombatStarted = Paused = EnemySelected = attackRequested = false;
        movementPath.Clear();
        pendingInteraction = null;
        waypoint = 0;
        chaseRepathSeconds = 0;
        Camera.Reset();
        aimOrigin = CameraPosition;
        aimDirection = CameraTarget-CameraPosition;
        contextualAim = false;
        chosenContext = null;
        chosenEnemy = false;
        doorJustOpened = false;
    }
    public void TogglePause() => Paused = !Paused;
    public void ClearInteractionMessage()
    {
        InteractionMessage=null;
        ChestOpened=null;
    }
    public void CycleView()
    {
        if (Camera.CycleView()) ClearOnCameraModeChange();
    }
    public void AdjustZoom(float amount)
    {
        if (Camera.AdjustZoom(amount)) ClearOnCameraModeChange();
    }
    private void ClearOnCameraModeChange()
    {
        movementPath.Clear();
        pendingInteraction = null;
        waypoint = 0;
        chaseRepathSeconds = 0;
        chosenContext = null;
        chosenEnemy = false;
        VisibleTarget = Target = null;
    }
    public void UpdateAim(Vector3 origin, Vector3 direction)
    {
        contextualAim = false;
        chosenEnemy = false;
        aimOrigin = origin;
        aimDirection = direction;
        VisibleTarget = InteractionQuery.FindVisible(World,origin,direction);
        Target = VisibleTarget is { } visible &&
            InteractionQuery.CanReach(World,CameraAnchor,visible.Item,settings.InteractionRange)
            ? visible : null;
    }
    public void UpdateContextAim()
    {
        contextualAim = true;
        if (chosenEnemy && CombatStarted && Enemy.Alive)
        {
            Target = VisibleTarget = null;
            return;
        }
        chosenEnemy = false;
        Target = null;
        foreach (var choice in InteractionQuery.FindReachable(World,CameraAnchor,settings.InteractionRange))
            if (choice.Item == chosenContext) { Target = choice; break; }
        if (Target is null)
        {
            chosenContext = null;
            var forward = new Vector3(-MathF.Sin(Yaw),0,-MathF.Cos(Yaw));
            Target = InteractionQuery.FindNearby(World,CameraAnchor,forward,settings.InteractionRange);
        }
        VisibleTarget = Target;
    }
    public IReadOnlyList<ContextActionOption> AvailableContextActions()
    {
        var actions = new List<ContextActionOption>();
        foreach (var choice in InteractionQuery.FindReachable(World,CameraAnchor,settings.InteractionRange))
            actions.Add(new(choice.Item.Action, !chosenEnemy && Target?.Item == choice.Item));
        if (CombatStarted && Enemy.Alive)
            actions.Add(new("Attack enemy", ContextEnemySelected));
        return actions;
    }
    public void TargetEnemyContextually()
    {
        if (!CombatStarted || !Enemy.Alive || !Player.Alive) return;
        chosenContext = null;
        chosenEnemy = true;
        pendingInteraction = null;
        movementPath.Clear();
        waypoint = 0;
        contextualAim = true;
        Target = VisibleTarget = null;
        EnemySelected = true;
        attackRequested = gameplay.AutoAttackSelectedEnemy;
    }
    public bool CycleContext(int direction)
    {
        if (Paused || !Player.Alive || direction == 0) return false;
        var choices = InteractionQuery.FindReachable(World,CameraAnchor,settings.InteractionRange);
        bool enemyAvailable = CombatStarted && Enemy.Alive;
        if (choices.Count == 0 && !enemyAvailable) return false;
        int count = choices.Count + (enemyAvailable ? 1 : 0);
        int current = chosenEnemy && enemyAvailable ? choices.Count : -1;
        for (int i=0;i<choices.Count;i++)
            if (choices[i].Item == Target?.Item) { current = i; break; }
        int next = current < 0 ? (direction > 0 ? 0 : count-1)
            : (current + Math.Sign(direction) + count) % count;
        chosenEnemy = enemyAvailable && next == choices.Count;
        if (chosenEnemy)
        {
            chosenContext = null;
            pendingInteraction = null;
            movementPath.Clear();
            waypoint = 0;
            Target = VisibleTarget = null;
            SelectEnemyContextual();
        }
        else
        {
            chosenContext = choices[next].Item;
            Target = VisibleTarget = choices[next];
            EnemySelected = attackRequested = false;
            pendingInteraction = null;
            Player.CancelAttack();
            movementPath.Clear();
            waypoint = 0;
        }
        contextualAim = true;
        return true;
    }
    public bool ActivateContext()
    {
        if (ContextEnemySelected)
        {
            RequestAttack();
            return true;
        }
        return TryInteractVisible();
    }
    public void UpdateInteraction(bool activate)
    {
        UpdateAim(CameraPosition,CameraTarget-CameraPosition);
        InteractionMessage = null;
        if (activate) TryInteractVisible();
    }
    /// <summary>Consumes a click on an interactable even when it is too far to use.</summary>
    public bool TryInteractVisible()
    {
        InteractionMessage = null;
        if (Paused || !Player.Alive || VisibleTarget is not { } visible) return false;
        pendingInteraction = null;
        movementPath.Clear();
        waypoint = 0;
        return TryInteractItem(visible.Item);
    }
    private bool TryInteractItem(Interactable item)
    {
        if (!InteractionQuery.CanReach(World,CameraAnchor,item,settings.InteractionRange))
        {
            InteractionMessage = "Move closer to interact";
            return true;
        }
        bool wasOpen=item.IsOpen;
        bool changed = item.Activate(Player.Position);
        InteractionMessage = changed
            ? item is ChestInteractable && wasOpen ? "Chest searched"
                : $"{item.Name} {(item.IsOpen ? "opened" : "closed")}"
            : "Step away from the doorway";
        if (changed && item.Kind == InteractionKind.Door && item.IsOpen)
            doorJustOpened = true;
        if (changed && item is ChestInteractable chest)
            ChestOpened=chest;
        if (contextualAim) UpdateContextAim();
        else UpdateAim(aimOrigin,aimDirection);
        return true;
    }

    public bool RequestInteraction(Interactable item)
    {
        if (!TopDown || Paused || !Player.Alive || !World.Interactables.Contains(item)) return false;
        pendingInteraction = null;
        movementPath.Clear();
        waypoint = 0;
        attackRequested = false;
        Player.CancelAttack();
        if (InteractionQuery.CanReach(World,CameraAnchor,item,settings.InteractionRange))
            return TryInteractItem(item);
        var route = DungeonNavigation.FindPathToInteraction(World,Player.Position,item,settings.InteractionRange);
        if (route.Count == 0)
        {
            InteractionMessage = $"No clear route to {item.Name.ToLowerInvariant()}";
            return false;
        }
        movementPath.AddRange(route);
        pendingInteraction = item;
        InteractionMessage = $"Moving to {item.Name.ToLowerInvariant()}";
        return true;
    }

    public bool SelectEnemy() => SelectEnemy(CameraPosition,CameraTarget-CameraPosition);
    public bool SelectEnemy(Vector3 origin,Vector3 direction)
    {
        if (Paused || !CombatStarted || !Player.Alive || !Enemy.Alive ||
            !Finite(origin) || !Finite(direction) || direction.LengthSquared()<0.000001f) return false;
        var ray = Vector3.Normalize(direction) * 1000;
        var bounds = new Block(Enemy.Position+new Vector3(0,0.9f,0),new(1,1.8f,1));
        if (!DungeonWorld.SegmentHit(origin,ray,bounds.Min,bounds.Max,out float hit)) return false;
        var point = origin + ray*hit;
        if (!World.ClearLine(origin,point)) return false;
        EnemySelected = true;
        if (gameplay.AutoAttackSelectedEnemy) RequestAttack();
        return true;
    }
    public bool SelectEnemyContextual()
    {
        if (Paused || !CombatStarted || !Player.Alive || !Enemy.Alive) return false;
        EnemySelected = true;
        if (gameplay.AutoAttackSelectedEnemy) RequestAttack();
        return true;
    }
    public void RequestAttack()
    {
        if (!Paused && Player.Alive && EnemySelected && Enemy.Alive)
        {
            attackRequested = true;
            pendingInteraction = null;
            movementPath.Clear();
            waypoint = 0;
            chaseRepathSeconds = 0;
        }
    }
    public bool SetMoveDestination(Vector3 destination)
    {
        destination.Y = 0;
        if (!TopDown || Paused || !Player.Alive) return false;
        pendingInteraction = null;
        attackRequested = false;
        Player.CancelAttack();
        movementPath.Clear();
        waypoint = 0;
        var route = DungeonNavigation.FindPath(World,Player.Position,destination);
        if (route.Count == 0)
        {
            InteractionMessage = "No clear route";
            return false;
        }
        movementPath.AddRange(route);
        InteractionMessage = null;
        return true;
    }
    public void UpdateTopDown(Vector2 pan, float seconds)
    {
        if (!TopDown || Paused || !Player.Alive || !float.IsFinite(seconds) || seconds <= 0 ||
            !float.IsFinite(pan.X) || !float.IsFinite(pan.Y)) return;
        seconds = Math.Min(seconds,0.1f);
        if (pan.LengthSquared()>1) pan=Vector2.Normalize(pan);
        Camera.Pan(pan,seconds);
        UpdateChase(seconds);
        AdvancePath(seconds);
        if (pendingInteraction is { } item)
        {
            if (InteractionQuery.CanReach(World,CameraAnchor,item,settings.InteractionRange))
            {
                pendingInteraction = null;
                movementPath.Clear();
                waypoint = 0;
                TryInteractItem(item);
            }
            else if (MoveDestination is null)
            {
                pendingInteraction = null;
                InteractionMessage = $"Could not reach {item.Name.ToLowerInvariant()}";
            }
        }
    }
    private void UpdateChase(float seconds)
    {
        if (!attackRequested || !EnemySelected || !Enemy.Alive) return;
        bool canAttack = Player.InRange(Enemy) &&
            World.ClearLine(Player.Position+Vector3.UnitY,Enemy.Position+Vector3.UnitY);
        if (canAttack)
        {
            movementPath.Clear();
            waypoint = 0;
            return;
        }
        chaseRepathSeconds -= seconds;
        if (chaseRepathSeconds > 0 &&
            Vector3.DistanceSquared(chaseEnemyPosition,Enemy.Position) < 0.25f) return;
        chaseEnemyPosition = Enemy.Position;
        chaseRepathSeconds = 0.25f;
        var route = DungeonNavigation.FindPathToRange(World,Player.Position,Enemy.Position,settings.Combat.Range);
        movementPath.Clear();
        movementPath.AddRange(route);
        waypoint = 0;
        if (route.Count == 0) InteractionMessage = "No clear route to enemy";
    }
    private void AdvancePath(float seconds)
    {
        if (waypoint>=movementPath.Count) return;
        var target=movementPath[waypoint];
        var offset=target-Player.Position;
        float distance=offset.Length();
        if (distance<=0.1f)
        {
            Player.Position=World.Move(Player.Position,offset);
            if (Vector3.DistanceSquared(Player.Position,target)<=0.0025f) waypoint++;
            else { movementPath.Clear(); waypoint=0; }
            return;
        }
        var delta=offset/distance*Math.Min(distance,settings.MoveSpeed*seconds);
        var moved=World.Move(Player.Position,delta);
        if (Vector3.DistanceSquared(moved,Player.Position)<0.000001f)
        {
            movementPath.Clear();
            waypoint=0;
            return;
        }
        Player.Position=moved;
        Player.Rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.Atan2(-delta.X,-delta.Z));
        if (Vector3.DistanceSquared(Player.Position,target)<=0.0025f) waypoint++;
    }

    public void UpdateCombat(float seconds, bool controllerScheme = false)
    {
        CombatMessage = null;
        if (Paused || !float.IsFinite(seconds) || seconds <= 0 || !Enemy.Alive || !Player.Alive) return;
        seconds = Math.Min(seconds,0.1f);
        if (!CombatStarted)
        {
            if (doorJustOpened) { doorJustOpened = false; return; }
            var enemyEye = Enemy.Position + Vector3.UnitY;
            var playerEye = Player.Position + Vector3.UnitY;
            if (Vector3.Distance(enemyEye,playerEye) > settings.Combat.VisionRange ||
                !World.ClearLine(enemyEye,playerEye)) return;
            CombatStarted = true;
            Paused = settings.Combat.AutoPauseOnCombatStart;
            if (controllerScheme) TargetEnemyContextually();
            CombatMessage = Paused ? "Enemy spotted you - combat paused" : "Enemy spotted you - combat started";
            if (Paused) return;
        }
        var distance = Vector2.Distance(new(Enemy.Position.X,Enemy.Position.Z),
            new(Player.Position.X,Player.Position.Z));
        if (distance > settings.Combat.Range*0.85f)
        {
            var offset = Player.Position-Enemy.Position;
            offset.Y = 0;
            if (offset.LengthSquared()>0.0001f)
                Enemy.Position = World.Move(Enemy.Position,Vector3.Normalize(offset)*settings.Combat.EnemySpeed*seconds);
        }
        bool clear = World.ClearLine(Player.Position+Vector3.UnitY,Enemy.Position+Vector3.UnitY);
        bool playerCanHit = Player.InRange(Enemy) && clear;
        bool enemyCanHit = Enemy.InRange(Player) && clear;
        if (attackRequested && Player.UpdateAttack(seconds,Enemy,playerCanHit))
            CombatMessage = $"Enemy hit - {Enemy.Health}/{Enemy.MaxHealth} HP";
        else if (!attackRequested) Player.CancelAttack();
        if (Enemy.Alive && Enemy.UpdateAttack(seconds,Player,enemyCanHit))
            CombatMessage = $"Player hit - {Player.Health}/{Player.MaxHealth} HP";
        if (!Enemy.Alive)
        {
            attackRequested = false;
            movementPath.Clear();
            waypoint = 0;
            CombatMessage = "Enemy defeated";
        }
        else if (!Player.Alive)
        {
            attackRequested = false;
            CombatMessage = "Player defeated - open pause menu to return";
        }
    }

    public void Update(Vector2 move, Vector2 look, bool mouseLook, float seconds,
        float turn = 0, bool faceCamera = false)
    {
        if (Paused || !Player.Alive || !float.IsFinite(seconds) || seconds <= 0) return;
        seconds = Math.Min(seconds, 0.1f);
        if (!float.IsFinite(move.X) || !float.IsFinite(move.Y) ||
            !float.IsFinite(look.X) || !float.IsFinite(look.Y) || !float.IsFinite(turn)) return;
        Camera.Rotate(look,mouseLook,seconds,turn);
        if (move.LengthSquared() > 1) move = Vector2.Normalize(move);
        var forward = new Vector3(-MathF.Sin(Yaw),0,-MathF.Cos(Yaw));
        var right = new Vector3(MathF.Cos(Yaw),0,-MathF.Sin(Yaw));
        var direction = right * move.X + forward * move.Y;
        Player.Position = World.Move(Player.Position,direction * (settings.MoveSpeed * seconds));
        if (faceCamera)
            Player.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY,Yaw);
        else if (direction.LengthSquared() > 0.0001f)
            Player.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.Atan2(-direction.X,-direction.Z));
        else if (Math.Abs(turn) > 0.0001f || mouseLook && Math.Abs(look.X) > 0.0001f)
            Player.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY,Yaw);
    }
    private static bool Finite(Vector3 v) =>
        float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}

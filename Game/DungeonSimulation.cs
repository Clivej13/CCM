using System.Numerics;

namespace CCM.Game;

public readonly record struct ContextActionOption(string Label, bool Selected);

public sealed class DungeonSimulation
{
    private readonly DungeonSettings settings;
    private readonly GameplaySettings gameplay;
    private readonly ItemCatalogue items;
    private PlayerDefinition playerDefinition;
    private readonly EnemyDefinition enemyDefinition;
    private readonly AbilityCatalogue? abilities;
    private readonly StatusCatalogue? statuses;
    private AbilityDefinition? castingAbility;
    private readonly Func<float> combatRoll;
    private float abilityCastSeconds;
    private readonly Dictionary<string,(float Remaining,float Duration)> abilityCooldowns=
        new(StringComparer.Ordinal);
    private readonly List<Vector3> movementPath = [];
    private Interactable? pendingInteraction;
    private int waypoint;
    private bool attackRequested;
    private bool manualMovementSinceCombatUpdate;
    private string? pendingCombatMessage;
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
    public float AnimationSeconds { get; private set; }
    public string? CastingAbilityId => castingAbility?.Id;
    public float AbilityCastFraction => castingAbility?.Combat is { } combat
        ? Math.Clamp(1-abilityCastSeconds/combat.CastSeconds,0,1) : 0;
    public float AbilityCooldownFraction(string id) =>
        abilityCooldowns.TryGetValue(id,out var cooldown)
            ? Math.Clamp(cooldown.Remaining/cooldown.Duration,0,1) : 0;
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
        ItemCatalogue? items=null,IEnumerable<ItemStack>? chestContents=null,
        PlayerDefinition? playerDefinition=null,EnemyCatalogue? enemies=null,
        AbilityCatalogue? abilities=null,StatusCatalogue? statuses=null,
        Func<float>? combatRoll=null)
    {
        settings.Validate();
        this.settings=settings;
        this.gameplay=gameplay ?? new GameplaySettings();
        this.items=items ?? new ItemCatalogue();
        this.abilities=abilities;
        this.statuses=statuses;
        var random=new Random(1729);
        this.combatRoll=combatRoll ?? random.NextSingle;
        playerDefinition ??= new PlayerDefinition();
        playerDefinition.Validate();
        this.playerDefinition=playerDefinition;
        enemies ??= new EnemyCatalogue { Enemies=[new EnemyDefinition()] };
        enemies.Validate(this.items,statuses);
        enemyDefinition=enemies.Get(settings.EnemyId);
        World=new DungeonWorld(chestContents);
        Player=new(World.Spawn,this.playerDefinition.Attributes,this.items,
            this.playerDefinition.SkillIds,this.playerDefinition.AbilityIds);
        Enemy=new(new(0,0,-30),enemyDefinition,this.items);
        Camera=new DungeonCamera(settings,World,()=>Player.Position);
        Reset();
    }
    public void Reset()
    {
        Player.Damaged-=OnPlayerDamaged;
        World.Reset();
        Player=new(World.Spawn,playerDefinition.Attributes,items,playerDefinition.SkillIds,
            playerDefinition.AbilityIds);
        foreach (var stack in settings.StartingItems)
        {
            var item=items.Get(stack.ItemId);
            if (!item.Stackable && stack.Quantity!=1)
                throw new InvalidDataException($"Invalid starting quantity for {item.Id}.");
            Player.Inventory.Add(stack.ItemId,stack.Quantity);
        }
        foreach (var id in settings.StartingEquipped)
            if (!Player.Equipment.Equip(id))
                throw new InvalidDataException($"Cannot equip starting item {id}.");
        for (int i=0;i<Math.Min(playerDefinition.AbilityIds.Count,AbilityLoadout.SlotCount);i++)
            Player.Abilities.Loadout.Assign(i,playerDefinition.AbilityIds[i],Player.Abilities);
        Player.Damaged+=OnPlayerDamaged;
        Enemy=new(new(0,0,-30),enemyDefinition,items);
        ChestOpened=null;
        VisibleTarget = Target = null;
        InteractionMessage = CombatMessage = null;
        CombatStarted = Paused = EnemySelected = attackRequested = false;
        manualMovementSinceCombatUpdate=false;
        pendingCombatMessage=null;
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
        castingAbility=null;
        abilityCastSeconds=0;
        abilityCooldowns.Clear();
        AnimationSeconds=0;
    }
    public void ApplyPlayerBuild(PlayerDefinition previous,PlayerDefinition next)
    {
        Player.ApplyBuild(previous,next);
        playerDefinition=next;
        castingAbility=null;
        abilityCastSeconds=0;
        abilityCooldowns.Clear();
        CombatMessage=null;
    }

    private void OnPlayerDamaged(int damage)
    {
        if (castingAbility is not null) abilityCastSeconds+=0.3f;
    }

    private void InterruptPlayerAttackForMovement(bool stopAutoAttack)
    {
        if (stopAutoAttack)
        {
            attackRequested=false;
            manualMovementSinceCombatUpdate=true;
        }
        Player.InterruptAttack();
        if (castingAbility is null) return;
        castingAbility=null;
        abilityCastSeconds=0;
        CombatMessage=pendingCombatMessage="Ability interrupted by movement";
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
            ? item is FireplaceInteractable fireplace
                ? fireplace.IsLit ? "Fireplace lit" : "Fireplace extinguished"
                : item is ChestInteractable && wasOpen ? "Chest searched"
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
    public string RequestAbility(string id)
    {
        if (abilities is null || statuses is null) return "Ability combat is unavailable";
        var ability=abilities.Get(id);
        if (!Player.Abilities.CanUse(ability,Player)) return $"{ability.Name} is unavailable";
        if (ability.Combat is null) return $"{ability.Name} has no combat effect yet";
        if (Paused || !Player.Alive || Player.Statuses.BlocksActions)
            return "Cannot use an ability now";
        if (manualMovementSinceCombatUpdate) return "Stop moving to use an ability";
        if (castingAbility is not null) return "Finish the current action first";
        if (abilityCooldowns.ContainsKey(id)) return $"{ability.Name} is cooling down";
        if (!EnemySelected || !Enemy.Alive) return "Select an enemy first";
        if (!Player.InRange(Enemy) ||
            !World.ClearLine(Player.Position+Vector3.UnitY,Enemy.Position+Vector3.UnitY))
            return "Move into ability range";
        if (!Player.TrySpendStamina(ability.Combat.StaminaCost))
            return "Not enough stamina";
        Player.CancelAttack();
        if (ability.Combat.CastSeconds==0)
            return ResolveAbility(ability,true);
        castingAbility=ability;
        abilityCastSeconds=ability.Combat.CastSeconds;
        return $"{ability.Name} casting";
    }

    private void UpdateAbility(float seconds,bool clear)
    {
        TickAbilityCooldowns(seconds);
        if (castingAbility is null) return;
        if (Player.Statuses.BlocksActions)
        {
            castingAbility=null;
            abilityCastSeconds=0;
            CombatMessage="Ability interrupted";
            return;
        }
        abilityCastSeconds=Math.Max(0,abilityCastSeconds-seconds);
        if (abilityCastSeconds>0) return;
        var ability=castingAbility;
        castingAbility=null;
        CombatMessage=ResolveAbility(ability,clear);
    }

    private string ResolveAbility(AbilityDefinition ability,bool clear)
    {
        var effect=ability.Combat!;
        if (effect.RecoverySeconds>0)
            abilityCooldowns[ability.Id]=(effect.RecoverySeconds,effect.RecoverySeconds);
        bool ranged=ability.RequiredWeaponVisual=="Bow";
        int attackDamage=Player.Profile.Damage;
        if (ranged && !Player.Equipment.ConsumeAmmo())
            return $"{ability.Name}: no arrows in quiver";
        if (!Enemy.Alive || !Player.InRange(Enemy) || !clear)
            return $"{ability.Name} missed";
        if (effect.CastSeconds==0)
        {
            var outcome=CombatFormula.RollAttack(Player.Attributes,Enemy.Attributes,ranged,
                combatRoll(),combatRoll(),!Enemy.Statuses.BlocksActions);
            if (outcome!=AttackOutcome.Hit)
                return $"{ability.Name} {(outcome==AttackOutcome.Dodged ? "dodged" : "missed")}";
        }
        bool combo=effect.ConsumeStatusId is { } needed && Enemy.Statuses.Consume(needed);
        float multiplier=effect.DamageMultiplier*
            (combo ? effect.ConditionalDamageMultiplier : 1f);
        int raw=Math.Max(1,(int)MathF.Round(attackDamage*multiplier));
        int before=Enemy.Health;
        Enemy.ReceiveDamage(raw);
        if (Enemy.Alive)
        {
            string? applied=combo && effect.ConditionalApplyStatusId is not null
                ? effect.ConditionalApplyStatusId : effect.ApplyStatusId;
            if (applied is not null) Enemy.Statuses.Apply(statuses!.Get(applied));
        }
        return $"{ability.Name}{(combo ? " combo" : "")}: {before-Enemy.Health} damage";
    }

    private void TickAbilityCooldowns(float seconds)
    {
        foreach (var id in abilityCooldowns.Keys.ToArray())
        {
            var cooldown=abilityCooldowns[id];
            float remaining=cooldown.Remaining-seconds;
            if (remaining<=0.0001f) abilityCooldowns.Remove(id);
            else abilityCooldowns[id]=(remaining,cooldown.Duration);
        }
    }

    public bool SetMoveDestination(Vector3 destination)
    {
        destination.Y = 0;
        if (!TopDown || Paused || !Player.Alive) return false;
        pendingInteraction = null;
        InterruptPlayerAttackForMovement(true);
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
        if (Player.Statuses.BlocksActions) return;
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
        var route = DungeonNavigation.FindPathToRange(World,Player.Position,Enemy.Position,Player.Profile.Range);
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
            var nearbyPosition=World.Move(Player.Position,offset);
            if (Vector3.DistanceSquared(nearbyPosition,Player.Position)>0.000001f)
                InterruptPlayerAttackForMovement(!attackRequested);
            Player.Position=nearbyPosition;
            if (Vector3.DistanceSquared(Player.Position,target)<=0.0025f) waypoint++;
            else { movementPath.Clear(); waypoint=0; }
            return;
        }
        var delta=offset/distance*Math.Min(distance,settings.MoveSpeed*Player.Attributes.MovementMultiplier*seconds);
        var moved=World.Move(Player.Position,delta);
        if (Vector3.DistanceSquared(moved,Player.Position)<0.000001f)
        {
            movementPath.Clear();
            waypoint=0;
            return;
        }
        InterruptPlayerAttackForMovement(!attackRequested);
        Player.Position=moved;
        Player.Rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.Atan2(-delta.X,-delta.Z));
        if (Vector3.DistanceSquared(Player.Position,target)<=0.0025f) waypoint++;
    }

    public void UpdateCombat(float seconds, bool controllerScheme = false)
    {
        CombatMessage = pendingCombatMessage;
        pendingCombatMessage=null;
        if (Paused || !float.IsFinite(seconds) || seconds <= 0 || !Player.Alive) return;
        if (manualMovementSinceCombatUpdate)
        {
            attackRequested=false;
            manualMovementSinceCombatUpdate=false;
            Player.InterruptAttack();
        }
        seconds = Math.Min(seconds,0.1f);
        AnimationSeconds+=seconds;
        Player.Statuses.Tick(seconds);
        Enemy.Statuses.Tick(seconds);
        Player.RegenerateStamina(seconds);
        if (!Enemy.Alive)
        {
            UpdateAbility(seconds,false);
            return;
        }
        if (!CombatStarted)
        {
            if (doorJustOpened) { doorJustOpened = false; return; }
            var enemyEye = Enemy.Position + Vector3.UnitY;
            var playerEye = Player.Position + Vector3.UnitY;
            if (Vector3.Distance(enemyEye,playerEye) > Enemy.Definition.VisionRange ||
                !World.ClearLine(enemyEye,playerEye)) return;
            CombatStarted = true;
            Paused = settings.Combat.AutoPauseOnCombatStart;
            if (controllerScheme) TargetEnemyContextually();
            CombatMessage = Paused ? "Enemy spotted you - combat paused" : "Enemy spotted you - combat started";
            if (Paused) return;
        }
        var distance = Vector2.Distance(new(Enemy.Position.X,Enemy.Position.Z),
            new(Player.Position.X,Player.Position.Z));
        if (!Enemy.Statuses.BlocksActions && distance > Enemy.Profile.Range*0.85f)
        {
            var offset = Player.Position-Enemy.Position;
            offset.Y = 0;
            if (offset.LengthSquared()>0.0001f)
                Enemy.Position = World.Move(Enemy.Position,Vector3.Normalize(offset)*Enemy.Definition.MoveSpeed*Enemy.Attributes.MovementMultiplier*seconds);
        }
        bool clear = World.ClearLine(Player.Position+Vector3.UnitY,Enemy.Position+Vector3.UnitY);
        UpdateAbility(seconds,clear);
        if (attackRequested && castingAbility is null)
        {
            if (Player.UsesBow && !Player.HasAmmo)
            {
                attackRequested=false;
                Player.CancelAttack();
                CombatMessage="No arrows equipped in quiver";
            }
            else
            {
                Player.UpdateAttack(seconds,Enemy,clear,
                    combatRoll(),combatRoll());
                if (Player.LastAttackOutcome!=AttackOutcome.None)
                {
                    var outcome=Player.LastAttackOutcome;
                    if (Player.UsesBow) Player.Equipment.ConsumeAmmo();
                    CombatMessage=outcome switch
                    {
                        AttackOutcome.Hit => $"Enemy hit - {Enemy.Health}/{Enemy.MaxHealth} HP",
                        AttackOutcome.Dodged => "Enemy dodged the attack",
                        _ => "Attack missed"
                    };
                }
            }
        }
        else Player.InterruptAttack();
        if (Enemy.Alive)
        {
            Enemy.UpdateAttack(seconds,Player,clear,
                combatRoll(),combatRoll());
            if (Enemy.LastAttackOutcome!=AttackOutcome.None)
            {
                var outcome=Enemy.LastAttackOutcome;
                CombatMessage=outcome switch
                {
                    AttackOutcome.Hit => $"Player hit - {Player.Health}/{Player.MaxHealth} HP",
                    AttackOutcome.Dodged => "Player dodged the attack",
                    _ => "Enemy attack missed"
                };
                if (outcome==AttackOutcome.Hit)
                {
                    if (Enemy.Definition.OnHitStatusId is { } id &&
                        statuses is not null && Player.Alive)
                    {
                        Player.Statuses.Apply(statuses.Get(id));
                        CombatMessage += $" | {statuses.Get(id).Name}";
                    }
                }
            }
        }
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
        if (Player.Statuses.BlocksActions) return;
        if (move.LengthSquared() > 1) move = Vector2.Normalize(move);
        var forward = new Vector3(-MathF.Sin(Yaw),0,-MathF.Cos(Yaw));
        var right = new Vector3(MathF.Cos(Yaw),0,-MathF.Sin(Yaw));
        var direction = right * move.X + forward * move.Y;
        var moved=World.Move(Player.Position,direction * (settings.MoveSpeed * Player.Attributes.MovementMultiplier * seconds));
        if (Vector3.DistanceSquared(moved,Player.Position)>0.000001f)
            InterruptPlayerAttackForMovement(true);
        Player.Position=moved;
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

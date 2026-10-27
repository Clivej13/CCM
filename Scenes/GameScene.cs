using CCM.Application;
using CCM.Game;
using Raylib_cs;
using RaylibGameFramework.Input;
using RaylibGameFramework.Assets;
namespace CCM.Scenes;

public sealed class GameScene : Scene
{
    private readonly SessionInput input;
    private readonly DungeonSettings settings;
    private readonly Action returnToMenu;
    private readonly Action requestLevelUp;
    private readonly DungeonSimulation simulation;
    private readonly int skillPointLimit,abilityPointLimit;
    private PlayerDefinition currentBuild;
    private readonly GameplayTargeting targeting;
    private readonly DungeonRenderer renderer;
    private readonly GameplayHud hud;
    private readonly AbilityLoadoutUi abilityUi;
    private readonly InventoryPanel inventoryPanel;
    private readonly PauseMenu pauseMenu;
    private readonly IGameplayControls mouseFollow,mouseTopDown,gamepadFollow,gamepadTopDown;
    private IGameplayControls activeControls;
    private bool ignoreLook,wasTopDown;
    private string? feedback;
    private float feedbackSeconds;

    public GameScene(SessionInput input,DungeonSettings settings,bool debug,
        Action returnToMenu,Action requestLevelUp,InputController menuInput,InputConfig bindings,
        GameplaySettings? gameplay=null,ItemCatalogue? items=null,
        IEnumerable<ItemStack>? chestContents=null,
        PlayerDefinition? playerDefinition=null,EnemyCatalogue? enemies=null,
        SkillCatalogue? skills=null,AbilityCatalogue? abilities=null,
        StatusCatalogue? statuses=null,AssetManager? assets=null)
    {
        this.input=input;
        this.settings=settings;
        this.returnToMenu=returnToMenu;
        this.requestLevelUp=requestLevelUp;
        currentBuild=playerDefinition ?? new PlayerDefinition();
        skillPointLimit=skills?.SkillPoints ?? 0;
        abilityPointLimit=abilities?.StartingAbilityPoints ?? 0;
        items ??=new ItemCatalogue();
        simulation=new(settings,gameplay,items,chestContents,playerDefinition,enemies,
            abilities,statuses);
        targeting=new(simulation,settings);
        inventoryPanel=new(items,skills,abilities,statuses);
        pauseMenu=new(menuInput,bindings);
        renderer=new(simulation,items,debug,assets);
        hud=new(input,simulation,targeting,debug);
        abilityUi=new(input,abilities);
        mouseFollow=new MouseFollowControls(input,simulation,targeting);
        mouseTopDown=new MouseTopDownControls(input,simulation,targeting);
        gamepadFollow=new GamepadFollowControls(input,simulation);
        gamepadTopDown=new GamepadTopDownControls(input,simulation,targeting);
        activeControls=SelectControls();
    }

    private IGameplayControls SelectControls() => (input.UsesMouse,simulation.TopDown) switch
    {
        (true,false) => mouseFollow,
        (true,true) => mouseTopDown,
        (false,false) => gamepadFollow,
        _ => gamepadTopDown
    };

    public IReadOnlyCollection<string> TomeLearnedAbilityIds =>
        simulation.Player.Abilities.LearnedIds.Except(currentBuild.AbilityIds).ToArray();

    public void ApplyBuild(PlayerDefinition previous,PlayerDefinition next)
    {
        simulation.ApplyPlayerBuild(previous,next);
        currentBuild=next;
    }

    public override void Enter()
    {
        inventoryPanel.Close();
        pauseMenu.Close();
        targeting.Reset();
        abilityUi.Reset();
        feedback=null;
        feedbackSeconds=0;
        ignoreLook=true;
        wasTopDown=simulation.TopDown;
        activeControls=SelectControls();
        activeControls.Enter();
        activeControls.BeforeFrame();
    }

    public override void Update(float seconds)
    {
        if (pauseMenu.IsOpen)
        {
            if (!Raylib.IsWindowFocused()) return;
            if (input.PausePressed() || input.ReturnPressed()) { ClosePauseMenu(); return; }
            switch (pauseMenu.Update())
            {
                case "ResumeGame": ClosePauseMenu(); break;
                case "OpenLevelUp": requestLevelUp(); break;
                case "ReturnToMainMenu": returnToMenu(); break;
            }
            return;
        }
        if (inventoryPanel.IsOpen)
        {
            if (!Raylib.IsWindowFocused()) return;
            if (input.PausePressed())
            {
                inventoryPanel.Close();
                OpenPauseMenu();
                return;
            }
            if (input.ReturnPressed() || (!input.UsesMouse && input.InventoryPressed()) ||
                inventoryPanel.CloseClicked(input))
            {
                CloseInventoryPanel();
                return;
            }
            if (!inventoryPanel.IsChest)
            {
                if (inventoryPanel.SelectTabClicked(input)) return;
                if (input.AbilityPressed()) inventoryPanel.OpenAbilities();
                else if (input.CharacterPressed()) inventoryPanel.OpenCharacter();
                else if (input.InventoryPressed()) inventoryPanel.OpenInventory();
                else if (input.PanelRightPressed()) inventoryPanel.CycleTab(1);
                else if (input.PanelLeftPressed()) inventoryPanel.CycleTab(-1);
            }
            inventoryPanel.Update(input,simulation.Player);
            return;
        }
        if (activeControls.BeforeFrame()) ignoreLook=true;
        if (!Raylib.IsWindowFocused()) { activeControls.CancelPendingAction(); return; }
        if (input.InventoryPressed() || input.CharacterPressed() || input.AbilityPressed())
        {
            activeControls.Exit();
            abilityUi.Reset();
            if (input.AbilityPressed()) inventoryPanel.OpenAbilities();
            else if (input.CharacterPressed()) inventoryPanel.OpenCharacter();
            else inventoryPanel.OpenInventory();
            ignoreLook=true;
            return;
        }
        feedbackSeconds=Math.Max(0,feedbackSeconds-seconds);
        if (input.PausePressed()) { OpenPauseMenu(); return; }
        if (input.CombatPausePressed() && simulation.CombatStarted)
        {
            simulation.TogglePause();
            ignoreLook=true;
        }
        string? abilityFeedback=abilityUi.Update(simulation.Player);
        if (abilityUi.ActivatedAbilityId is { } abilityId &&
            (simulation.Paused || abilityUi.PointerConsumed ||
             abilityUi.WheelSelectionCommitted || abilityUi.WheelOpen))
            abilityFeedback=simulation.RequestAbility(abilityId);
        if (abilityFeedback is not null)
        {
            feedback=abilityFeedback;
            feedbackSeconds=1.5f;
        }
        if (simulation.Paused) { activeControls.CancelPendingAction(); return; }
        if (abilityUi.PointerConsumed || abilityUi.WheelSelectionCommitted)
        {
            activeControls.CancelPendingAction();
            simulation.UpdateCombat(seconds,controllerScheme:!input.UsesMouse);
            return;
        }
        if (abilityUi.WheelOpen)
        {
            activeControls.CancelPendingAction();
            simulation.UpdateCombat(seconds,controllerScheme:true);
            return;
        }

        simulation.AdjustZoom(input.Zoom*settings.MouseZoomStep);
        if (!input.UsesMouse && input.CycleViewPressed()) simulation.CycleView();
        if (simulation.TopDown!=wasTopDown)
        {
            wasTopDown=simulation.TopDown;
            activeControls.Exit();
            activeControls=SelectControls();
            activeControls.Enter();
            targeting.Reset();
            ignoreLook=true;
        }
        if (activeControls.BeforeFrame()) ignoreLook=true;
        simulation.ClearInteractionMessage();
        activeControls.Move(seconds,ignoreLook);
        if (abilityUi.ActivatedAbilityId is { } movingAbilityId)
        {
            feedback=simulation.RequestAbility(movingAbilityId);
            feedbackSeconds=1.5f;
        }
        targeting.Update(input,seconds);
        bool combatBeforeFrame=simulation.CombatStarted;
        activeControls.Act(ignoreLook);
        simulation.UpdateCombat(seconds,controllerScheme:!input.UsesMouse);
        if (simulation.ChestOpened is { } chest)
        {
            activeControls.Exit();
            inventoryPanel.OpenChest(chest);
            ignoreLook=true;
        }
        if (!combatBeforeFrame && simulation.CombatStarted)
            targeting.OnCombatStarted(input.UsesMouse);
        if (simulation.InteractionMessage is { } interaction)
            { feedback=interaction; feedbackSeconds=1.5f; }
        if (simulation.CombatMessage is { } combat)
            { feedback=combat; feedbackSeconds=1.5f; }
        ignoreLook=false;
    }

    private void CloseInventoryPanel()
    {
        inventoryPanel.Close();
        abilityUi.Reset();
        activeControls.Enter();
        ignoreLook=true;
    }

    private void OpenPauseMenu()
    {
        activeControls.Exit();
        abilityUi.Reset();
        pauseMenu.Open();
        ignoreLook=true;
    }

    private void ClosePauseMenu()
    {
        pauseMenu.Close();
        activeControls.Enter();
        ignoreLook=true;
    }

    public override void Exit()
    {
        pauseMenu.Close();
        inventoryPanel.Close();
        abilityUi.Reset();
        activeControls.Exit();
    }

    public override void Render()
    {
        renderer.Render();
        hud.Render(feedback,feedbackSeconds,
            Math.Max(0,skillPointLimit-currentBuild.SkillIds.Count),
            Math.Max(0,abilityPointLimit-currentBuild.AbilityIds.Count));
        if (!inventoryPanel.IsOpen && !pauseMenu.IsOpen)
            abilityUi.Render(simulation.Player,simulation);
        inventoryPanel.Render(simulation.Player,input.UsesMouse);
        pauseMenu.Render();
    }
}

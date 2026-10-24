# CCM

C# / .NET 9 / Raylib-cs 8.0.0 game foundation. One window, one game loop:
Initial Loading → Splash → Title → Loading → MainMenu or LevelUp → Game → Shutdown.
New Game and debug test-level startup both open character setup before the same block-built dungeon. Escape / controller Start opens the pause menu.

## Build and launch

Install a .NET 9 SDK and a graphics driver supporting Raylib/OpenGL.

```text
dotnet restore CCM.sln --locked-mode
dotnet build CCM.sln -c Release --no-restore
dotnet test CCM.sln -c Release --no-build
dotnet run --project CCM.csproj -c Release
dotnet publish CCM.csproj -c Release -o artifacts/publish
```

Launch the published CCM executable from any working directory. Config and
asset paths resolve from the executable directory. The publish is framework
dependent and needs the .NET 9 runtime.

## Startup modes

Edit Config/debug.json beside the executable, or select a bundled profile.
regular.json explicitly disables debug, independent of your default debug.json:

```text
dotnet run --project CCM.csproj -- --debug-config regular.json
dotnet run --project CCM.csproj -- --debug-config regular-debug.json
dotnet run --project CCM.csproj -- --debug-config test-level-debug.json
```

| Enabled | StartupScene | Result after both loads, splash and title |
| --- | --- | --- |
| false | Menu or TestLevel | Main menu, no overlay |
| true | Menu | Main menu with diagnostics |
| true | TestLevel | Level-up screen, then dungeon test level with diagnostics |

ShowFPS controls the FPS portion of the debug overlay. Debug-off suppresses
verbose application and native diagnostics while retaining event/error logging.
Config/application.json controls splash duration, skipping, title, frame cap and
initial window settings. Enter, Space, mouse click or controller A skips the
splash when enabled. Even zero duration renders a splash frame.

The title then requires a fresh key, mouse click or controller button and its
release. Held splash-skip input cannot confirm it. Mouse motion and stick drift
do not select controls. All three modes wait at the title, including Test Level
Debug. Keyboard/mouse wins simultaneous presses, then the lowest controller slot.

The selected device stays fixed for this session and is shown in the debug
overlay. Menus continue accepting all devices. The game's movement and camera actions
use the selected keyboard/mouse or specific controller slot; if that controller
disconnects, Escape still opens the pause menu. Restart to select another
device. Gameplay movement and camera actions use that selection.

DeviceSelection is a small headless CCM component, with native polling isolated
in TitleInputReader and gameplay action mapping in SessionInput. The external
Input repository is unchanged by this migration. CCM now includes the 0.2.0
source snapshot and uses InputControllerOptions, GetRawValue and
InputMath.ApplyRadialDeadzone. Title arbitration remains CCM policy.

## Menus and settings

Arrow keys navigate; Enter activates buttons/toggles; Left/Right changes the
resolution selector. Mouse clicks and controller D-pad/left stick/A/B are supported through
the framework. MenuBack is Escape/B. All bindings live in Config/input.json.

Settings edits are pending until Apply. Returning to the main menu discards them.
Apply changes the live window, reads the actual resulting dimensions and saves
display settings through JsonSettingsStore to %LOCALAPPDATA%/CCM/display.json.
The Auto attack selected enemy toggle is saved separately to
%LOCALAPPDATA%/CCM/gameplay.json and starts repeating attacks when an enemy is
selected. With it off, press A on a selected enemy (or use the mouse attack
command) to begin attacking. Saved settings take precedence over defaults at
next launch. Resizing updates the display selection; use Apply to persist it.

Windows fullscreen choices come from the current monitor's enumerated display
modes. Windowed presets are bounded by desktop dimensions and include the current
custom size. Unsupported saved modes fall back to an available mode. Other
platforms conservatively offer desktop resolution for fullscreen; Windows is the
validated platform. Refresh rate remains Raylib/driver controlled.

## Ownership and configuration

- Application/GameApplication owns window lifetime, the loop and centralized
  transitions. Lifecycle validates allowed transitions without native calls.
- Scenes implement Enter, Update, Render and Exit. LevelUp is an application
  state before Game; Settings is a framework submenu of MainMenu.
- Separate AssetSessions own the presentation and destination AssetManagers.
  InitialLoading loads Config/presentation-assets.json (the title's OBJ mark).
  After title confirmation, Loading requires only the foundation verification
  model from Config/assets.json. Other catalogue entries can remain unloaded until
  a scene requires them through AssetManager.RequireAsset; ProcessNext loads one
  required native asset per frame. Dungeon blocks are drawn procedurally and need
  no external models.
- The splash and loading text use Raylib's built-in font. Each catalogue is
  validated when its loading stage begins. Failures remain on a responsive
  error screen; Escape/B or window close exits. Details go to logs.
- All assets unload before CloseWindow, including after loading failure.
- Configuration uses framework JsonSettingsStore for typed application/debug/
  logging files and persisted display/gameplay settings, plus framework catalogue,
  menu and input loaders. Required malformed config fails with a useful log.
  Invalid saved settings fall back without overwriting the bad file.
- Logs rotate under %LOCALAPPDATA%/CCM/Logs. Config/logging.json controls size and
  retention. CrashLogging handlers are detached at exit; the framework logger
  closes its file handle after each write.

Framework dependencies and source provenance are in [vendor/PROVENANCE.md](vendor/PROVENANCE.md).
The dungeon uses flat-floor collision, block characters and one melee enemy.
Jumping, animation and procedural generation are not included.

## Inventory and equipment

Config/items.json defines item IDs, display names, type, description, weight,
equipment slot, weapon damage/range/attack rate, protection, value and block visual types.
Config/containers.json gives the starter chest item IDs and quantities. Both catalogues load through the Configuration library at startup;
unknown references and duplicate definitions stop startup with an error.
The Raylib AssetManager and Config/assets.json still own graphical resources.
These prototype items use procedural block visuals, so they add no model or
texture assets yet. An item is an ID and quantity in an Inventory, never a
separately loaded asset instance.

Open the chest through the normal interaction controls to display its contents.
Up/Down, controller D-pad or left stick selects a stack; Enter/A or a mouse click transfers
it to the player's bag. Press I or controller Y to open the inventory. The bag is
on the left, with All, Weapons and Apparel filters changed by A/D, Left/Right,
controller left stick or a mouse click. Equipped Weapon, Body and Quiver slots are on the right, including
empty slots. Tab or controller X switches between bag and equipment; Up/Down
selects a row, and Enter/A equips or unequips. On mouse and keyboard, left click
selects a bag item, right click equips it, and the wheel or scrollbar scrolls the
bag. The selected item's catalogue details appear in the bottom-left detail panel;
character attributes, selected skills and combat values are on the Character menu. Equipped items
leave the bag, and replaced or unequipped items return to it. Arrows remain a bag stack.
On keyboard and mouse, I opens Inventory and C opens Character. Q/E switches
between their title tabs, which can also be clicked; A/D changes Inventory
filters. Character displays innate attributes, current health, derived combat
values and equipped gear. The top-right X closes any of the
Inventory, Character or chest panels. Movement does not close a panel, and I
does not close Inventory. On controller, LT switches to Inventory and RT switches
to Character while either screen is open. Y or B closes Inventory, Character or
the chest; the X close button is hidden. Escape/Start opens the pause menu. Open panels
suspend dungeon updates; the chest and player inventory reset when entering a
new game. Equipping changes the player's block appearance and live
combat values. Value is display-only. The bow currently uses a short melee range;
projectiles and arrow consumption are not implemented. Arrows cannot be equipped.

## Character setup and skills

Config/player.json supplies the starting attributes, currently 4 in each of Strength,
Endurance, Agility, Intelligence, Willpower, Speed, Charisma and Luck. Level Up has
separate Attributes and Skills tabs. Each tab puts the selection list on the left
and details on the right. Config/attributes.json owns the attribute descriptions;
see docs/character-system-copy.md for the TODO to review them whenever the
underlying systems change. Attribute values have no point budget and can be set
from 1 to 10. Config/skills.json defines skill names, descriptions,
up to three attribute prerequisites and two skill points. A skill cannot be selected
until every requirement is met. The catalogue includes two- and three-attribute
examples. Available skills appear above locked skills, with catalogue order retained within
each group. Lowering an attribute below a selected skill's requirement refunds its point.
Both points must be spent before Enter Dungeon is available. Skills are recorded on
the player and shown on the Character panel; they have no gameplay effects yet.

Use Up/Down to select a list entry. On Attributes, Left/Right or A/D changes
the selected value; Enter/controller A increases it. The last list entry opens
Skills. On Skills, Enter/controller A toggles a skill or activates Enter Dungeon.
Tab/controller X swaps tabs; Q/E or controller LT/RT selects a tab directly.
The tabs, plus/minus buttons and bottom action button are clickable. On the
Skills tab, left click inspects a skill and right click selects or deselects it.
The mouse wheel scrolls the skill list if it grows beyond eight entries.
Escape/controller B returns to the main menu without creating a player.

## Dungeon test level

New Game and Test Level Debug first open the same level-up screen, then enter the same two-room dungeon. The second room
is wider and deeper, with one block-built melee enemy. Opening the corridor door
reveals the enemy; combat begins when it sees the player within its configured
vision range. A chest beside the first room's north wall remains a separate
interaction target. Entering the scene resets the player, enemy, door and chest.

- Follow camera keyboard: W/S move forward/back, A/D turn, Q/E strafe.
  Hold right mouse to turn the camera and character; while held, A/D strafe.
  Releasing right mouse restores the pointer. F is an alternate interact key.
- Follow camera Xbox controller: left stick moves, right stick turns the camera.
  LB/RB cycles reachable doors and boxes, plus the enemy once combat starts.
  A opens/closes the chosen object or starts attacking the chosen enemy.
  Enemy selection has no distance limit; objects must be in reach. The chosen
  object stays highlighted while reachable. Available actions and the selected
  option appear in the upper HUD. Once the enemy detects the player it is
  selected automatically. No mouse pointer is needed in this view.
- Top-down camera: WASD or left stick pans without following the player.
  The mouse or right stick moves the cursor. Left click uses nearby objects,
  selects or attacks an enemy, or routes the player to a floor point. Right click
  on a door or chest routes the player into interaction range and activates it on
  arrival; right click on an enemy chases and attacks it. Controller A does the
  same from the cursor: approach and use an object, chase and attack an enemy, or
  move to a floor point. LB/RB cycles reachable objects and the enemy; A acts on
  that choice. Moving the right stick returns to cursor control. A new floor move
  command cancels a pending object action or enemy chase.
- In follow view, left click selects an enemy. A short right click interacts
  with a reachable door or chest, or selects an enemy and starts auto attack.
  A right drag turns the camera without issuing an action. In top-down view,
  left click or A interacts with objects or selects enemies. Object interaction
  has a distance limit; enemy selection does not.
- The mouse wheel zooms continuously between follow and free top-down camera
  at the configured threshold. On Xbox, press the right stick (R3) to cycle
  close follow, wide follow and free top-down views. LT and RT switch between
  Inventory and Character while a panel is open; they do not zoom the camera.
  During combat, Space/Back toggles combat pause; Escape/Start opens the separate pause menu. Top-down
  uses the pointer or controller cursor; third-person controller interaction
  uses the highlighted reachable object. That object turns gold, and doors
  cannot close on the player.
- Opening the door does not pause or start combat by itself. The enemy spots the
  player when within VisionRange and line of sight; its default 28-unit range
  covers the player at the doorway after it opens. Combat then starts and, with
  AutoPauseOnCombatStart enabled, pauses until Space or Back resumes it.
  The enemy advances and attacks automatically
  in melee range. In follow view, left click selects the enemy and right click
  starts repeating melee attacks; with an Xbox controller, LB/RB chooses the
  enemy and A attacks. The Auto attack selected enemy menu option starts attacks
  as soon as an enemy is selected. In follow view, move into range to land hits;
  top-down attack moves the player into range automatically. Both combatants use
  a cast time, cooldown, health and line-of-sight checks. Defeat ends combat;
  use Return to Main Menu in the pause menu to leave. Resuming the pause menu preserves any combat pause.
- Title selection determines the gameplay device. Other controllers cannot move
  the player. If the selected controller disconnects, movement/look stop and
  keyboard Escape opens the pause menu.
- Mouse capture is active only while right mouse is held in focused follow-camera
  gameplay. Releasing it restores the pointer to its original position; focus
  loss and scene exit restore it as well. The first look sample after capture is
  discarded. Top-down mouse and controller behavior is unchanged.
- Debug mode draws collision bounds, facing direction, player position and
  camera distance.

Config/dungeon.json controls movement and turn speed, sensitivities, deadzones, zoom threshold,
top-down pan/cursor speeds, starting pitch, interaction range, the active enemy ID and
AutoPauseOnCombatStart. Config/input.json contains the action bindings.
Config/player.json defines starting player attributes; Config/attributes.json holds their
Level Up descriptions; Config/skills.json defines selectable skills and prerequisites;
Config/enemies.json catalogues enemy types,
attributes, movement speed and vision range. The game instantiates the selected enemy
only when entering the dungeon. CombatEntity shares health, mitigation, range, cast and
cooldown rules for PlayerEntity and EnemyEntity. Strength raises base damage; Endurance
raises health and armour; Agility raises attack rate; Intelligence and Willpower raise
resistance. Speed raises movement speed for players and enemies. Charisma and Luck
can unlock skills but have no gameplay effects yet. Equipped weapon damage adds to base damage, while its melee range and rate
set the attack profile. Cast and cooldown divide the attack interval at a fixed ratio.
Armour subtracts damage before percentage resistance, with a minimum of one damage
for a successful hit. Enemy types can reference catalogue weapon and body-armour IDs. PlayerEntity composes an Inventory and Equipment; EnemyEntity
does not carry player equipment. DoorInteractable and ChestInteractable share
the Interactable targeting contract, while static walls and floors remain Block
geometry. ThreeD Transform3D supplies position, rotation and facing vectors.
CCM owns the room, collision and movement. DungeonCamera owns follow/top-down
position, rotation, zoom, panning and camera collision. GameplayTargeting owns
pointer rays, the controller cursor and contextual action selection. Four
IGameplayControls implementations map keyboard/mouse and controller input in
follow and top-down views to the same DungeonSimulation commands. GameScene
coordinates updates and transitions; DungeonRenderer and GameplayHud draw the
world and overlay separately. Collision uses a conservative square footprint
and small axis-separated steps; the character slides along walls. The camera
tests its path against expanded obstacle bounds. Simulation limits long frame
intervals to 0.1 seconds rather than moving through walls.

The completed Input 0.2.0 migration is recorded in
[docs/input-library-follow-up.md](docs/input-library-follow-up.md).
The external libraries remain untouched. The AssetManager owns native models and
textures, while item/enemy catalogues own gameplay definitions. No dedicated weapon
or enemy model files exist yet, so no such native assets are loaded; the dungeon
continues drawing blocks. Their definitions are validated at startup, and the
enemy instance is created only when the game scene is first entered.

## Validation

Executed successfully on Windows:
- Release build, zero warnings/errors.
- 98 headless tests across the foundation/dungeon and input integration suites: all startup combinations, required lifecycle ordering,
  return/new-game/shutdown transitions, invalid configs, persisted settings,
  bundled menu/input/catalogue validation, separate loading catalogues, title
  press/release selection and missing-asset diagnostics; dungeon movement,
  collision, camera obstruction/pitch, input timing, deadzones, controller filtering,
  door/chest targeting, reach, occlusion, collision, melee timing, pause, combat
  start, unlimited enemy selection, limited object interaction, zoom mode,
  routed click movement and object activation, keyboard turning and facing, contextual Xbox interaction,
  reachable-action cycling, controller view presets and target selection,
  long-range follow-camera aiming, top-down combat chase, selected-device
  action edges, item catalogue validation, chest transfer, equipment slots and
  inventory reset, item metadata, bag filters, ownership transfers and input for pane switching.
  Input integration links the production library and CCM session sources against
  deterministic Raylib polling, covering multiple pads, title release gating,
  raw mouse/stick values and disconnect/reconnect fallback edges.
- Six native tests: all three startup profiles with both loading stages,
  title and splash frames, actual Model loading, both menu/game transitions
  and clean window teardown; window resolution changes, fullscreen round trip,
  persistence and screen-to-world pointer rays in both camera modes.
- Release publish, including Config and Assets.

```text
dotnet test Tests/Native/CCM.NativeTests.csproj -c Release
```

The native suite opens windows and changes display modes temporarily. It is kept
outside the default solution test suite so normal CI remains headless. Native
tests use temporary settings/log directories and do not change your saved settings.
The optional --smoke launch flag feeds idle/press/release samples through the
title selection gate, traverses scene states and exits. It fails immediately on
asset-loading errors. Native tests use this path; it does not exercise physical
devices. Normal launches always wait for real title input.

Still check interactively:
1. Visually inspect splash centring, loading progress and the debug overlay in
   each mode. Each tiny asset may finish in just two loading frames.
2. Use keyboard, mouse and a physical controller for every menu action; verify
   New Game opens the dungeon and the selected device moves, looks and returns.
   Hold splash skip, release, and verify the title needs a second press. Confirm
   with a controller and check another controller cannot control the player. Walk against walls, through
   the doorway and around obstacles; aim at the door and chest to verify the gold
   highlight follows the pointer on mouse and nearby object in view on Xbox. Open the door, enter the second
   room, return, and check that the door cannot close on the player. Open the
   door with auto-pause on and off; confirm opening alone does not pause, then
   confirm enemy detection starts combat and auto-pause. Select and attack the
   enemy, watch both health
   bars and cast/cooldown indicators, and verify Space/Back freezes the fight.
   Open the separate pause menu with Escape/Start, navigate it with the left stick,
   and verify Resume preserves combat pause while Return to Main Menu leaves the scene.
   Confirm B alone does not leave gameplay.
   Zoom out to free top-down view, pan away from the player, click around obstacles
   and through the open doorway, right-click the door and chest from beyond reach
   to confirm the player walks up and activates them, then use A and the
   controller cursor, and zoom in to
   reattach the camera. Confirm distant enemy selection works while a distant
   door/chest left click cannot instantly activate it. Open the chest in each
   camera view, take all six stacks, equip each weapon in turn plus armour and
   quiver, and verify the block visuals and arrow count. Reopen the emptied
   chest, then start a new game and verify its contents reset. In inventory, use
   Left/Right or the left stick to cycle filters, Tab/X to switch panes, and
   Enter/A to unequip a slot. In the bag, left click an item to check its
   bottom-left details, right click it to equip, then scroll with the wheel and
   scrollbar; ensure scrolling does not change the selected item's details.
   Check empty slot labels and that equipped items disappear from the bag, while
   replaced items return to it. On keyboard and mouse, press C to open Character,
   use Q/E or the title tabs to switch it with Inventory, A/D to change bag
   filters, and click X to close Character, Inventory and the chest. Check that movement does not
   close any panel. On controller, confirm LT/RT switches Inventory and Character,
   B closes either screen and the chest, and no X close button appears. In follow view,
   right-click an enemy,
   right-drag the camera and verify the pointer returns on release. Check A/D turning,
   Q/E strafing, A/D strafing during right drag, and short right-click interaction.
   On an Xbox controller, use LB/RB to cycle the door, chest and enemy, A to
   interact or attack, and R3 to cycle close, wide and top-down views. Toggle
   Auto attack selected enemy in Settings, Apply, and confirm that selection
   starts attacks automatically and persists after restart. Verify LT/RT do not
   zoom and no pointer is needed in follow view. Orbit near walls, then Alt-Tab
   and return.
3. Resize while Settings is open; apply several modes, restart, and confirm
   selection/persistence. Check monitor moves, DPI scaling and driver-specific
   fullscreen behaviour on the intended hardware.
4. Try window close in each state, splash skip, and a missing/corrupt bundled
   asset in a disposable publish copy. Confirm an error screen and useful log.

Automated native checks validate API state and lifecycle, not pixel appearance,
physical devices or every monitor/driver. Loading is incremental per asset;
one future large asset can still block its individual ProcessNext call.

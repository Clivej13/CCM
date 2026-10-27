# CCM

C# / .NET 9 / Raylib-cs 8.0.0 game foundation. One window, one game loop:
Initial Loading → Splash → Title → Loading → MainMenu or LevelUp → Game → Shutdown.
New Game and debug test-level startup both open character setup before the same modular dungeon. Escape / controller Start opens the pause menu.

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
  After title confirmation, Loading requires the foundation verification model,
  the two characters, fireplace and dungeon scenery from Config/assets.json.
  ProcessNext loads one required native asset per frame. Equipped items still
  use procedural visuals.
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
The dungeon uses flat-floor collision and one melee enemy. The player and enemy
use human and orc GLB models copied from the sibling CCM_assets character library.
The player starts the test fight with a bow, a quiver and loaded arrows.
Jumping and procedural generation are not included. Both characters play looping
idle, walk and run animations according to their movement speed. The player runs
at full movement speed; slower controller movement and the enemy chase use walk.

The player uses the Human/Male locomotion GLB, which contains its own Idle,
Walk and Run clips. The enemy uses the separate Orc/Male idle, walk and run
GLBs. The game samples the exported joint transforms and skin weights directly
and scales the complete character to 1.78 m for the human and 2.08 m for the
orc. To refresh these copies when `CCM_assets` is beside this repository, run
`dotnet build CCM.csproj -p:ImportCcmCharacters=true`.
Normal builds use the copies already in this repository.

Three fireplaces in the starting room, corridor and far room are the dungeon's
only origins of light. Each `FireplaceLightRig` groups the main firelight with two
weaker secondary lights placed around its room to suggest light bouncing off the
stone. All three follow the hearth's flicker and extinguish together. The lights
illuminate nearby characters, fireplace models, floor, walls and props with a
soft warm tint. Walls and closed doors block them; opening a door lets light
through. The shader lives in `Assets/Shaders`; `Scenes/DungeonLighting.cs`
uses it for both models and procedural cubes, so lighting varies across each
floor tile, wall and door surface. Animation updates mesh normals with the
vertices so the light follows each pose.

The fireplace GLB and dungeon scenery modules come from the sibling
`CCM_assets` library.
`dotnet build CCM.csproj -p:ImportCcmFireplace=true` refreshes the repository
copy; normal builds use the bundled copy. Game-driven flame particles rise from
each model's `FireEffectOrigin` at 0.35 m above the floor. Each fireplace can be
extinguished or relit independently, removing or restoring its light.
`FireplaceLightIntensity` in `Config/dungeon.json` sets their brightness from 0 to 3.
The scenery kit supplies flagstone and cracked floor tiles, ashlar and barred
window walls, pillars, the arched oak doorway, chest, crates, barrel, rubble and
an unlit torch sconce. The existing collision and interaction rules remain in
the game. `dotnet build CCM.csproj -p:ImportCcmDungeonKit=true` refreshes the
module GLBs copied into this repository.

## Inventory and equipment

Config/items.json defines item IDs, display names, type, description, weight,
equipment slot, weapon damage/range/attack rate, protection, value and block visual types.
Config/containers.json gives the starter chest item IDs and quantities. Both catalogues load through the Configuration library at startup;
unknown references and duplicate definitions stop startup with an error.
The Raylib AssetManager and Config/assets.json still own graphical resources.
Equipped prototype items still use procedural block visuals, while the dungeon
chest and scenery use models. An item is an ID and quantity in an Inventory, never a
separately loaded asset instance.

Open the chest through the normal interaction controls to display its contents.
Up/Down, controller D-pad or left stick selects a stack; Enter/A or a mouse click transfers
it to the player's bag. Press I or controller Y to open the inventory. The bag is
on the left, with All, Weapons, Apparel, Ammunition and Tomes filters changed by A/D, Left/Right,
controller left stick or a mouse click. Equipped Weapon, Body, Quiver and Ammo slots are on the right, including
empty slots. Tab or controller X switches between bag and equipment; Up/Down
selects a row, and Enter/A equips or unequips. On mouse and keyboard, left click
selects a bag item, right click equips it, and the wheel or scrollbar scrolls the
bag. The selected item's catalogue details appear in the bottom-left detail panel;
character attributes, selected skills and combat values are on the Character menu. Equipped items
leave the bag, and replaced or unequipped items return to it. Equipping arrows loads their stack
into an equipped quiver; shots consume them, and unloading returns the remainder to the bag.
On keyboard and mouse, I opens Inventory, C opens Character and K opens
Abilities. Q/E cycles their title tabs, which can also be clicked; A/D changes
Inventory filters. Character displays innate attributes, current health,
derived combat values and equipped gear. Abilities lists catalogue abilities
with their path, learning requirements and learned status. The top-right X
closes any panel. Movement does not close a panel, and I does not close
Inventory. On controller, LT/RT cycles backward/forward across the three tabs.
Y or B closes the panel or chest; the X close button is hidden. Escape/Start opens the pause menu. Open panels
suspend dungeon updates; the chest and player inventory reset when entering a
new game. Equipping changes the procedural weapon and quiver visuals and live
combat values; the character outfit is currently fixed. Value is display-only. The bow uses ranged line of sight and consumes
one loaded arrow per released shot.

Config/abilities.json defines two starting ability points, ability IDs, paths,
attribute and skill prerequisites, equipment requirements, stamina costs, cast
and recovery times, and optional status effects. Paths describe a direction of
training; they do not lock the character to a class. The catalogue contains
four iron sword attacks and two archer attacks. Sword Novice unlocks Opening Break and
Quick Cut; Sword Adept requires Novice and unlocks Driving Cut and Finishing Cut.
Bow Novice unlocks Quick Shot and Aimed Shot.
Adept also requires Strength 5 and Agility 4. Each attack needs an equipped sword
when used. Bow abilities require a bow with loaded arrows. Six single-use tomes are in the starter chest and reference catalogue abilities through Config/items.json. Move
a tome to the bag, then press Enter/controller A or right click it to learn its
ability once the requirements are met. An unavailable or already learned tome
is retained. Learning does not require the eventual equipment; a learned ability
remains known if equipment changes, but its use requirements may no longer be
met. Starting ability picks and tome-learned abilities share the same collection.
The Abilities tab lists learned and unlearned entries for inspection.
The Abilities tab assigns learned abilities to a player-owned loadout. On keyboard,
select an ability and press 1-9, 0, - or = to assign that slot, or click a slot at
the bottom of the tab. Pressing the same slot again with the same ability clears
it; right-clicking a slot also clears it. In the dungeon the 12-slot bar stays
at the bottom of the screen; its keys or a mouse click activate an assigned
combat ability. On controller, the Abilities tab shows the first eight slots.
Left/right selects a wheel slot, A assigns or clears the highlighted learned
ability, and X clears the slot. Hold LT in the dungeon to open the eight-way
wheel, aim with the right stick, and release LT to activate. LT continues to
switch tabs while a panel is open. The bar and wheel share the same first eight
assignments. Unlearned abilities cannot be assigned; learned abilities with
unmet current requirements appear disabled. Assignments belong to the current
player and reset with a new dungeon. During a cast, the purple line under its
ability fills from left to right. During that ability's cooldown, its tile has
a dark fill that drains away; cooldown does not use the purple line.

Config/statuses.json owns status names, descriptions, duration, display tone and
action blocking. Combatants own their active timers; reapplying a status refreshes
it, and combat pause freezes it. Opening Break applies Shaken. Quick Cut applies
Pressured, or consumes Shaken to apply Exposed. Driving Cut consumes Exposed to
apply Staggered, and Finishing Cut consumes Staggered for a larger hit. Quick Cut
followed by Opening Break consumes Pressured for a cheaper, moderate bonus.
These interactions use ability and status IDs rather than bar numbers. The guard
applies Shaken on a landed attack, so the left-side player portrait displays a
real incoming status next to health and regenerating stamina. Staggered and
Stunned block movement and attacks briefly. Melee attacks and sword abilities
land immediately; bow attacks and abilities have a visible cast that takes longer
each time the player is hit. Moving interrupts attack windups and stops auto-attacking;
it also cancels an ability cast without spending an arrow. Quick Shot marks a target and Aimed Shot consumes
Marked for extra damage and a brief pin. Configured casts must be shorter than
the statuses they apply or consume. Cast-time abilities hit a target in range
with line of sight; they miss if sight is blocked before release. Instant attacks
and ordinary bow shots can miss or be dodged. Auto-attacks
cost no stamina and resume after an ability's cast. Ability requests need a selected
enemy, line of sight, weapon range and sufficient stamina.

## Character setup and skills

Config/player.json supplies the starting attributes, currently 4 in each of Strength,
Endurance, Agility, Intelligence, Willpower, Speed, Charisma and Luck. Level Up has
Attributes, Skills, Abilities and Finish tabs. The first three tabs put the
selection list on the left and details on the right; Finish reviews attribute
values, spent skill and ability points, and learned picks before confirmation.
Config/attributes.json owns the attribute descriptions;
see docs/character-system-copy.md for the TODO to review them whenever the
underlying systems change. Attribute values have no point budget and can be set
from 1 to 10. Config/skills.json defines skill names, descriptions,
up to three attribute prerequisites, skill prerequisites and two skill points.
The bundled catalogue contains Sword Novice, Sword Adept and Bow Novice. Adept requires
Novice, Strength 5 and Agility 4; selecting Sword Novice and Sword Adept
spends both starting skill points. Available skills appear above locked skills, with catalogue order
retained within each group. Lowering an attribute or removing Novice refunds
Adept and any starting abilities that no longer meet their requirements.
Finish Level Up is available even with unspent points. The remaining skill and
ability points are tracked with the current build and shown in the dungeon HUD.
Open the pause menu with Escape/Start and choose Level Up to spend them later;
returning to the dungeon keeps inventory, equipment, camera, enemy state and
combat pause. Tome-learned abilities remain known and do not consume starting
ability points. Starting a New Game begins a fresh build. Skills are recorded
on the player and shown on the Character panel; they unlock matching sword
and bow abilities but have no direct combat effect. Selected abilities are shown as
learned in the dungeon's Abilities tab. Lowering an attribute or removing a
prerequisite skill during setup refunds any invalid starting ability pick.

Use Up/Down to select a list entry. On Attributes, Left/Right or A/D changes
the selected value; Enter/controller A increases it. The last list entry opens
Skills. On Skills, Enter/controller A toggles a skill or opens Abilities. On
Abilities, Enter/controller A toggles a starting ability or opens Finish. On
Finish, Enter/controller A confirms the build even if points remain.
Tab/controller X cycles tabs; Q/E or controller LT/RT moves backward/forward
through them.
The tabs, plus/minus buttons and bottom action button are clickable. On the
Skills and Abilities tabs, left click inspects an entry and right click selects
or deselects it. The mouse wheel scrolls either list beyond eight entries.
Controller B removes the highlighted skill or ability pick, or lowers the highlighted
attribute; on Finish it returns to Abilities. It does nothing on an unselected row. Escape/Start returns to the
previous setup tab. From Attributes, Escape/Start returns to the main menu
before the first dungeon entry, or to the same game when revisiting Level Up.

## Dungeon test level

New Game and Test Level Debug first open the same level-up screen, then enter the same two-room dungeon. The second room
is wider and deeper, with one orc melee enemy. Opening the corridor door
reveals the enemy; combat begins when it sees the player within its configured
vision range. A chest beside the first room's north wall remains a separate
interaction target. Entering the scene resets the player, enemy, door, chest
and fireplaces.

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
  starts repeating attacks with the equipped weapon; with an Xbox controller, LB/RB chooses the
  enemy and A attacks. The Auto attack selected enemy menu option starts attacks
  as soon as an enemy is selected. In follow view, move into range to land hits;
  top-down attack moves the player into range automatically. Both combatants use
  a cooldown, health and line-of-sight checks; bow attacks also have a cast time.
  Defeat ends combat;
  use Return to Main Menu in the pause menu to leave. Resuming the pause menu preserves any combat pause.
- Title selection determines the gameplay device. Other controllers cannot move
  the player. If the selected controller disconnects, movement/look stop and
  keyboard Escape opens the pause menu.
- Mouse capture is active only while right mouse is held in focused follow-camera
  gameplay. Releasing it restores the pointer to its original position; focus
  loss and scene exit restore it as well. The first look sample after capture is
  discarded. Top-down mouse and controller behavior is unchanged.
- Debug mode draws collision bounds and facing direction, with diagnostics,
  controls, player position and camera distance centred at the top. The player
  panel stays on the left and the enemy panel on the right.

Config/dungeon.json controls movement and turn speed, sensitivities, deadzones, zoom threshold,
top-down pan/cursor speeds, starting pitch, interaction range, the active enemy ID and
AutoPauseOnCombatStart. Config/input.json contains the action bindings.
Config/player.json defines starting attributes and archer abilities; Config/attributes.json holds their
Level Up descriptions; Config/skills.json defines selectable skills and prerequisites;
Config/enemies.json catalogues enemy types,
attributes, movement speed and vision range. The game instantiates the selected enemy
only when entering the dungeon. Config/dungeon.json also supplies the starting bow,
quiver and arrows. CombatEntity shares health, mitigation, range, cast and
cooldown rules for PlayerEntity and EnemyEntity. Strength raises base damage; Endurance
raises health and armour; Agility raises attack rate; Intelligence and Willpower raise
resistance. Speed raises movement speed for players and enemies. Charisma and Luck
can unlock skills; Luck also affects dodge chance. Equipped weapon damage adds to base
damage, while bow damage also uses loaded ammunition. Weapon range and rate set
the attack profile. Melee attacks land immediately; bow attacks have a windup
that takes longer when the attacker is hit.
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
The external libraries remain untouched. The AssetManager owns native models
and textures, while item/enemy catalogues own gameplay definitions. Equipped
weapons and armour still use procedural visuals. Their definitions are validated
at startup, and the enemy instance is created when the game scene is first entered.

## Validation

Executed successfully on Windows:
- Release build, zero warnings/errors.
- 121 headless tests across the foundation/dungeon and input integration suites: all startup combinations, required lifecycle ordering,
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
   New Game opens the four setup tabs, the Finish review and button work, available abilities move above locked
   ones as attributes and skills change. Finish with points unspent, check the
   dungeon HUD count, reopen Level Up from the pause menu, spend a point and
   confirm the count changes without resetting the dungeon. Check that learned
   abilities remain available and the selected device moves, looks and returns.
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
   camera view, take all ten stacks, equip each weapon in turn plus armour and
   quiver, and verify the block visuals and arrow count. Reopen the emptied
   chest, then start a new game and verify its contents reset. In inventory, use
   Left/Right or the left stick to cycle filters, Tab/X to switch panes, and
   Enter/A to unequip a slot. In the bag, left click an item to check its
   bottom-left details, right click it to equip, then scroll with the wheel and
   scrollbar; ensure scrolling does not change the selected item's details.
   Check empty slot labels and that equipped items disappear from the bag, while
   replaced items return to it. In the Tomes filter, check that Quick Cut's
   tome stays in the bag without Sword Novice or enough Agility. Restore both,
   read the tome, and confirm it moves to learned Abilities. Check that Quick
   Cut cannot be used until the iron sword is equipped. Assign learned abilities to
   the 1 through = bar slots, use those keys and click the bar, then check that
   controller A/X assigns and clears the first eight slots and LT plus right
   stick activates from the wheel without issuing a dungeon action. Confirm
   unlearned abilities cannot be assigned. Equip the iron sword, learn its four
   tomes, and try Opening Break, Quick Cut, Driving Cut, Finishing Cut in that
   order; compare with Quick Cut then Opening Break. Watch the enemy statuses
   change and the stamina cost. Let the guard land a hit and check Shaken beside
   the left-side player portrait, health and stamina. Pause combat and confirm
   status timers stop. On keyboard and mouse, press C to open Character and K to open Abilities;
   use Q/E or the title tabs to cycle all three, A/D to change bag
   filters, and click X to close Character, Inventory, Abilities and the chest. Check that movement does not
   close any panel. On controller, confirm LT/RT cycles Inventory, Character and Abilities,
   B closes any panel and the chest, and no X close button appears. In follow view,
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

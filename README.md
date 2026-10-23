# CCM

C# / .NET 9 / Raylib-cs 8.0.0 game foundation. One window, one game loop:
Initial Loading → Splash → Title → Loading → MainMenu or Game → Shutdown. New Game and debug test-level
startup share the same block-built dungeon test level. Escape / controller B returns to the menu.

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
| true | TestLevel | Dungeon test level with diagnostics |

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
overlay. Menus continue accepting all devices. The game's movement, camera and return actions
use the selected keyboard/mouse or specific controller slot; if that controller
disconnects, Escape provides a route back to the menu. Restart to select another
device. Gameplay movement and camera actions use that selection.

DeviceSelection is a small headless CCM component, with native polling isolated
in TitleInputReader and gameplay action mapping in SessionInput. The external
Input repository is unchanged by this migration. CCM now includes the 0.2.0
source snapshot and uses InputControllerOptions, GetRawValue and
InputMath.ApplyRadialDeadzone. Title arbitration remains CCM policy.

## Menus and settings

Arrow keys navigate; Enter activates buttons/toggles; Left/Right changes the
resolution selector. Mouse clicks and controller D-pad/A/B are supported through
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
- Scenes implement Enter, Update, Render and Exit. Settings is a framework
  submenu of MainMenu, not an extra application state.
- Separate AssetSessions own the presentation and destination AssetManagers.
  InitialLoading loads Config/presentation-assets.json (the title's OBJ mark).
  After title confirmation, Loading loads Config/assets.json (the foundation
  verification model). Both call ProcessNext once per frame and display actual
  progress. Dungeon blocks are drawn procedurally and need no external models.
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

## Dungeon test level

New Game and Test Level Debug enter the same two-room dungeon. The second room
is wider and deeper, with one block-built melee enemy. Opening the corridor door
reveals the enemy; combat begins when it sees the player within its configured
vision range. A box beside the first room's north wall remains a separate
interaction target. Entering the scene resets the player, enemy, door and box.

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
  on a door or box routes the player into interaction range and activates it on
  arrival; right click on an enemy chases and attacks it. Controller A does the
  same from the cursor: approach and use an object, chase and attack an enemy, or
  move to a floor point. LB/RB cycles reachable objects and the enemy; A acts on
  that choice. Moving the right stick returns to cursor control. A new floor move
  command cancels a pending object action or enemy chase.
- In follow view, left click selects an enemy. A short right click interacts
  with a reachable door or box, or selects an enemy and starts auto attack.
  A right drag turns the camera without issuing an action. In top-down view,
  left click or A interacts with objects or selects enemies. Object interaction
  has a distance limit; enemy selection does not.
- The mouse wheel zooms continuously between follow and free top-down camera
  at the configured threshold. On Xbox, press the right stick (R3) to cycle
  close follow, wide follow and free top-down views. LT and RT are unbound for
  future actions. Space/Start pauses; Escape/B returns to the menu. Top-down
  uses the pointer or controller cursor; third-person controller interaction
  uses the highlighted reachable object. That object turns gold, and doors
  cannot close on the player.
- Opening the door does not pause or start combat by itself. The enemy spots the
  player when within VisionRange and line of sight; its default 28-unit range
  covers the player at the doorway after it opens. Combat then starts and, with
  AutoPauseOnCombatStart enabled, pauses until Space or Start/Options resumes it.
  The enemy advances and attacks automatically
  in melee range. In follow view, left click selects the enemy and right click
  starts repeating melee attacks; with an Xbox controller, LB/RB chooses the
  enemy and A attacks. The Auto attack selected enemy menu option starts attacks
  as soon as an enemy is selected. In follow view, move into range to land hits;
  top-down attack moves the player into range automatically. Both combatants use
  a cast time, cooldown, health and line-of-sight checks. Defeat ends combat;
  Escape/B returns to the menu.
- Title selection determines the gameplay device. Other controllers cannot move
  the player. If the selected controller disconnects, movement/look stop and
  keyboard Escape returns to the menu.
- Mouse capture is active only while right mouse is held in focused follow-camera
  gameplay. Releasing it restores the pointer to its original position; focus
  loss and scene exit restore it as well. The first look sample after capture is
  discarded. Top-down mouse and controller behavior is unchanged.
- Debug mode draws collision bounds, facing direction, player position and
  camera distance.

Config/dungeon.json controls movement and turn speed, sensitivities, deadzones, zoom threshold,
top-down pan/cursor speeds, starting pitch, interaction range and Combat values (health, damage, cast time,
cooldown, melee reach, enemy speed, vision range and AutoPauseOnCombatStart). Config/input.json
contains the action bindings.
CombatEntity provides shared health, cast and cooldown behaviour for PlayerEntity
and EnemyEntity. Static walls/floors and interactable door/box use shared Block
geometry; only interactables carry open/close state, so there is no world-object
base class yet. ThreeD Transform3D supplies position, rotation and facing vectors.
CCM owns the room, collision, movement and camera. Collision uses a conservative
square footprint and small axis-separated steps; the character slides along
walls. The camera tests its path against expanded obstacle bounds. Simulation
limits long frame intervals to 0.1 seconds rather than moving through walls.

The completed Input 0.2.0 migration is recorded in
[docs/input-library-follow-up.md](docs/input-library-follow-up.md).
The external libraries remain untouched.

## Validation

Executed successfully on Windows:
- Release build, zero warnings/errors.
- 69 headless tests across the foundation/dungeon and input integration suites: all startup combinations, required lifecycle ordering,
  return/new-game/shutdown transitions, invalid configs, persisted settings,
  bundled menu/input/catalogue validation, separate loading catalogues, title
  press/release selection and missing-asset diagnostics; dungeon movement,
  collision, camera obstruction/pitch, input timing, deadzones, controller filtering,
  door/box targeting, reach, occlusion, collision, melee timing, pause, combat
  start, unlimited enemy selection, limited object interaction, zoom mode,
  routed click movement and object activation, keyboard turning and facing, contextual Xbox interaction,
  reachable-action cycling, controller view presets and target selection,
  long-range follow-camera aiming, top-down combat chase and selected-device
  action edges.
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
   the doorway and around obstacles; aim at the door and box to verify the gold
   highlight follows the pointer on mouse and nearby object in view on Xbox. Open the door, enter the second
   room, return, and check that the door cannot close on the player. Open the
   door with auto-pause on and off; confirm opening alone does not pause, then
   confirm enemy detection starts combat and auto-pause. Select and attack the
   enemy, watch both health
   bars and cast/cooldown indicators, and verify Space/Start freezes the fight.
   Zoom out to free top-down view, pan away from the player, click around obstacles
   and through the open doorway, right-click the door and box from beyond reach
   to confirm the player walks up and activates them, then use A and the
   controller cursor, and zoom in to
   reattach the camera. Confirm distant enemy selection works while a distant
   door/box click cannot activate it. In follow view, right-click an enemy,
   right-drag the camera and verify the pointer returns on release. Check A/D turning,
   Q/E strafing, A/D strafing during right drag, and short right-click interaction.
   On an Xbox controller, use LB/RB to cycle the door, box and enemy, A to
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

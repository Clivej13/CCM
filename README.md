# CCM

C# / .NET 9 / Raylib-cs 8.0.0 game foundation. One window, one game loop:
Initial Loading → Splash → Title → Loading → MainMenu or Game → Shutdown. New Game and debug test-level
startup share the same black, empty scene. Escape / controller B returns to the menu.

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

Edit Config/debug.json beside the executable, or select a bundled profile:

```text
dotnet run --project CCM.csproj -- --debug-config regular-debug.json
dotnet run --project CCM.csproj -- --debug-config test-level-debug.json
```

| Enabled | StartupScene | Result after both loads, splash and title |
| --- | --- | --- |
| false | Menu or TestLevel | Main menu, no overlay |
| true | Menu | Main menu with diagnostics |
| true | TestLevel | Blank game scene with diagnostics |

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
overlay. Menus continue accepting all devices. The blank game's return action
uses the selected keyboard/mouse or specific controller slot; if that controller
disconnects, Escape provides a route back to the menu. Restart to select another
device. Current gameplay only has the return action; future gameplay bindings
should use the same session selection.

DeviceSelection is a small headless CCM component, with native polling isolated
in TitleInputReader and the current return action in SessionInput. The external
Input library and its vendored snapshot are unchanged. Generic session/device
selection can be contributed to the library separately.

## Menus and display settings

Arrow keys navigate; Enter activates buttons/toggles; Left/Right changes the
resolution selector. Mouse clicks and controller D-pad/A/B are supported through
the framework. MenuBack is Escape/B. All bindings live in Config/input.json.

Settings edits are pending until Apply. Returning to the main menu discards them.
Apply changes the live window, reads the actual resulting dimensions and saves
through JsonSettingsStore to %LOCALAPPDATA%/CCM/display.json. Saved settings take
precedence over application defaults at next launch. Resizing updates the
settings selection; use Apply to persist that size.

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
  progress. The title mark is drawn only on the title; the game remains black.
- The splash and loading text use Raylib's built-in font. Each catalogue is
  validated when its loading stage begins. Failures remain on a responsive
  error screen; Escape/B or window close exits. Details go to logs.
- All assets unload before CloseWindow, including after loading failure.
- Configuration uses framework JsonSettingsStore for typed application/debug/
  logging files and persisted display settings, plus framework catalogue,
  menu and input loaders. Required malformed config fails with a useful log.
  Invalid saved settings fall back without overwriting the bad file.
- Logs rotate under %LOCALAPPDATA%/CCM/Logs. Config/logging.json controls size and
  retention. CrashLogging handlers are detached at exit; the framework logger
  closes its file handle after each write.

Framework dependencies and source provenance are in [vendor/PROVENANCE.md](vendor/PROVENANCE.md).
No gameplay, character, physics or animation systems are introduced.

## Validation

Executed successfully on Windows:
- Release build, zero warnings/errors.
- 21 headless tests: all startup combinations, required lifecycle ordering,
  return/new-game/shutdown transitions, invalid configs, persisted settings,
  bundled menu/input/catalogue validation, separate loading catalogues, title
  press/release selection and missing-asset diagnostics.
- Four native tests: all three startup profiles with both loading stages, title and splash frames,
  actual Model loading, both menu/game transitions and clean window teardown;
  window resolution changes, fullscreen round trip and persistence.
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
   New Game is completely black in Regular mode and the selected device returns.
   Hold splash skip, release, and verify the title needs a second press. Confirm
   with a controller and check another controller cannot control the blank scene.
3. Resize while Settings is open; apply several modes, restart, and confirm
   selection/persistence. Check monitor moves, DPI scaling and driver-specific
   fullscreen behaviour on the intended hardware.
4. Try window close in each state, splash skip, and a missing/corrupt bundled
   asset in a disposable publish copy. Confirm an error screen and useful log.

Automated native checks validate API state and lifecycle, not pixel appearance,
physical devices or every monitor/driver. Loading is incremental per asset;
one future large asset can still block its individual ProcessNext call.

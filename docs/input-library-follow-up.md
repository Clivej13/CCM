# Input 0.2.0 migration — completed

CCM now consumes the Input 0.2.0 source snapshot in vendor.
NuGet.org did not contain this package during migration; no machine-local feed
or sibling project path is required. See vendor/PROVENANCE.md for source details.

Replaced:
- GamepadActions and IGamepadSource were removed.
- SessionInput creates an InputController with InputControllerOptions after
  title selection, filtering DeviceFamily and GamepadIndex.
- Movement/look composition uses Math.Abs(GetRawValue(action)) for directional
  magnitudes. InputMath.ApplyRadialDeadzone handles each controller stick.
- The unrestricted menu controller keeps the framework's default thresholds.

Preserved in CCM:
- DeviceSelection's fresh press/release title gate and fixed session owner.
- Keyboard diagonal normalization and action-to-gameplay mapping.
- Mouse delta sensitivity without elapsed-time scaling; controller rotation
  rate multiplied by elapsed time in DungeonSimulation.
- Selected-controller connectivity checks and keyboard Escape fallback.
- Combined backDown/backPressed tracking prevents duplicate actions when
  switching between a held controller back button and held fallback Escape.
- Config/input.json and Config/dungeon.json values remain unchanged.

Validation:
- 29 foundation/dungeon tests and 5 session-integration tests.
- Integration tests link unchanged production Input sources and CCM session
  sources against deterministic native polling, without a graphics context.
- Four native smoke tests exercise the actual compiled Input 0.2.0 library with
  menus, both startup routes, dungeon rendering, display settings and teardown.
- Physical dual-controller operation, unplug/replug and mouse capture still
  benefit from interactive checks.

The external Input repository was only read. No upstream API changes are needed
for this migration. When 0.2.0 is published to an accessible feed, switch both CCM
and its vendored Menus project together to one exact package version, remove the
Input project/snapshot, and update the linked-source test strategy and lock files.
Do not leave old source and package definitions active simultaneously.

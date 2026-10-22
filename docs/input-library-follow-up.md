# Input library follow-up

The dungeon implementation intentionally keeps these changes inside CCM.
Neither the external Input repository nor CCM's Input snapshot was modified.

Local implementation:
- Application/GamepadActions.cs reads configured actions from one selected slot.
  IGamepadSource permits headless tests with two controllers and disconnection.
- Raw directional stick components are combined into a two-dimensional vector,
  then a radial deadzone is applied and the remaining magnitude is rescaled.
  This avoids the library's current fixed 0.5 per-axis activation threshold.
- Application/SessionInput.cs keeps keyboard/mouse on the framework controller,
  composes movement/look vectors and keeps gamepad return-button edges locally.
- Config/dungeon.json supplies movement/look deadzones and sensitivities.
- DeviceSelection continues owning title press/release gating in CCM.

Proposed upstream API work:
1. Optional selected gamepad index and device-family filter on InputController;
   preserve unrestricted input as the default for existing menus.
2. Raw selected-controller axis sampling, plus configurable radial stick
   deadzones distinct from digital navigation/rebind thresholds.
3. Preserve action press/release semantics when filtering or disconnecting.
4. Keep mouse delta units separate from stick deflection. Mouse look is a
   displacement; stick look is a rate multiplied by elapsed time.
5. Validate bindings against supported devices/axes, and define trigger mapping
   separately from signed stick directions.
6. Tests for multiple pads, inactive devices, disconnect/reconnect, thresholds,
   diagonal vectors, low analog values and title-input carry-through.

When upstream supports these, replace GamepadActions with the framework API,
retaining CCM's action-to-gameplay mapping, camera sensitivity and movement rules.
Keep CCM's keyboard escape route when the selected controller disconnects.
No automatic device takeover is implemented; title selection lasts the session.

Current scope is signed stick axes and buttons used by this dungeon.
This local adapter is not a complete replacement input framework.

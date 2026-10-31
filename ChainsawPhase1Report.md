# Chainsaw Phase 1

## Implemented

1. Movement follows `input.wish` directly, acceleration 90, braking 120, turn scale 2.5; raised rest position and upright locomotion pitch.
2. Three ground hits: upright windup into hopping diagonal spin (body yaw 360 plus jump flip), left spin (-300), existing downward slam retained as the third hit. The old fourth-hit finisher is now hit 3; the old standalone rising hit was removed. Ground-combo hops can chain without being mistaken for jump attacks.
3. Right mouse: 0.18-second motor roll at 26 m/s, dash invulnerability, swept path contact damage once per enemy. Uses common directional assist.
4. Air left click: one 360-degree attack per jump, with common facing/distance assist and attack buffering.
5. E: 10-second overclock, windup/strike/recovery multiplied by 0.65, cooldown 16 seconds. Played swings are cloned; WeaponData is never mutated. Newly started swings revert after expiry; an already playing accelerated swing finishes at its chosen timing. Switching away ends the buff, as approved by coordinator.
6. Swap special: assisted forward 200-degree cone, range 4, short forward lunge, one immediate slice per enemy during 0.38 seconds.
7. Q: 2.4-second forward grinding roll at 13 m/s, repeated small hops, 0.1-second damage ticks, steering limited to 80 degrees/second; automatic target steering when move input is absent. Uses existing ultimate gauge and damage recording.
8. Shift: MoveSpeedScale is applied to direct locomotion; sprint multiplier is 1.6 (11 -> 17.6 m/s before swing scaling). Existing hit feedback and ChainsawDriver contact/rev methods are reused.

## Presentation handoff

- `Epic_Action/Assets/Scripts/Avatar/ChainsawMoveset.cs`: all new pose and drive tuning is at the top (`UprightPitch`, `FirstHitFlipDegrees`, `DashSpinDegrees`, `UltimateSpinSpeed`, `DrivePoseSharpness`, plus skill/drive damage/speed/duration constants). Pose application: `TickDrive`, `KickMotion`, existing `TickBounce`, and `Locomote`.
- `Epic_Action/Assets/Scripts/Editor/WeaponAvatarSceneBuilder.Chainsaw.cs`: `Chainsaw(WeaponData d)` owns rest pose, swing poses, timings, curves, body yaw, leaps and lunges. No new particles, camera behavior or audio work.

## Validation and remaining work

- Unity CLI project target port 7800: `script recompile` -> `up_to_date`; `script recompile_status` -> `failed: false`, `errors: []`, `compilationFailed: false` after final changes.
- Unity edit-time eval built an in-memory WeaponData via the private Chainsaw builder and destroyed it: ground count 3, air count 1, first bodyYaw 360, second -300, air 360; compiled `TickDrive` method exists. No scene or asset regeneration was performed.
- Scoped `git diff --check` passed. The shared scene has unrelated pre-existing whitespace; it was not edited by this worker.
- Historical console entries include a temporary missing TickDrive error from the first partial save (resolved), older material texture errors, and a CLI command spelling error from validation. These are distinct from the final compilation status, which reports no compilation errors. Console was not cleared.
- Final `console get --level error --since 29`: no new entries, groundTruth `compilationFailed: false`, `compiling: false`, `consoleErrors: 0`.
- Coordinator must regenerate the avatar scene/assets once after all weapon workers finish. Existing saved assets still contain old combo data until that build.
- Play mode and screenshots intentionally not run. Actual movement feel, cone reach, hit balance and animation continuity need the later gameplay/presentation pass. Drive overlaps use a 64-collider buffer; very dense clusters beyond that may need a larger buffer.
- Only the two assigned chainsaw source files and this report were changed by this worker; shared hooks and SampleScene combat behavior were not modified.

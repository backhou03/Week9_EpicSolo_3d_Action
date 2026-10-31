# Phase 3 — Stage flow and weapon facilities

## Result
Implemented the coordinator's superseding C plan: continuous narrow roads with arena locks and physical branch selection, rather than an IMGUI route-selection screen. Compilation completed with zero errors, and `WeaponAvatarSceneBuilder.Build()` succeeded through eval after the final changes. No play mode, screenshots, Git operations, or administrator operations were performed; Orca runtime access used the sandbox-approved connection.

## Progression
- Start at (0, 0, -16), slot 1 = evolved shears, slot 2 = chainsaw. Rifle and hammer are reward references only, not initially owned. The legacy reserve array contains no weapons and F no longer cycles it.
- Approach encounter: one 5-enemy wave, no doors locked. Players can run through.
- Stage 1 arena: center (0, 0, 22), 28 m square; entry locks both doors. Twelve waves, nine seconds apart, maximum 16 alive. Completion requires all scheduled waves and all surviving wave enemies defeated. Target about two minutes requires player timing verification.
- On clear, doors and branch gates open. A 1.8-second camera lift shows the fork, with plain A: RIFLE / B: HAMMER signs. The player chooses by running into a branch; the opposite branch closes permanently.
- Left midboss arena: (-28, 0, 82), rifle reward. Right: (28, 0, 82), hammer reward. Each contains a 500 HP boss scaled 1.55 plus two 45 HP enemies. Defeating the group opens its doors and adds the reward to the owned list; HUD shows an acquisition notice.
- Paths merge, with another unlocked one-wave road encounter. Final arena at (0, 0, 144) contains a 1400 HP boss scaled 2.2 plus three 45 HP enemies. Defeating the group shows a basic clear panel.
- Roads are 12 m wide and bounded by 6 m walls. Arena entrances/exits lock only for the three important encounters (one of the two midboss rooms is used). Seven facilities sit along the road, including before/after the fork and before the boss.
- Checkpoints update at visited road/branch/arena-entry triggers. Death uses the existing two-second respawn, preserving progression, ownership, and surviving enemy state. Arena checkpoints lie inside the locked entrance.
- F near a facility opens an IMGUI owned-weapon list. Existing carried weapons are shown disabled; selecting an owned uncarried weapon replaces the active slot. The outgoing weapon remains owned. Existing `Equip` then `ActivateSpecial` behavior is preserved. F closes the menu.

## Hooks for presentation worker
- `Assets/Scripts/Stage/StageManager.cs`: events `ArenaEntered(int)`, `ArenaCleared(int)`, `MapRevealStarted`, `MapRevealEnded`, `RouteSelected(int)`, `RewardAcquired(WeaponData)`, `BossEntered`, `RunCompleted`; methods `RevealMap`, `SelectRoute`, `EnterArena`; properties `SelectedRoute`, `MapRevealing`, `RunCleared`, `Status`.
- Arena indices: 0 = Stage 1, 1 = rifle midboss, 2 = hammer midboss, 3 = temporary boss. Route indices: 0 = rifle, 1 = hammer.
- StageManager constants: `StageOneWaves=12`, `StageOneWaveInterval=9`, `MapRevealDuration=1.8`, `MapRevealHeight=48`, `MidBossHp=500`, `TemporaryBossHp=1400`. Camera reveal runs in LateUpdate after the existing camera using execution order 1000. Presentation can replace this basic interpolation.
- `StageDoor`: `SetLocked(bool)`, `IsLocked`, `LockChanged(bool)`. Currently switches renderer/collider immediately; preserve collider gating when adding animation.
- `WeaponSwapFacility`: `InteractionRadius=3`, `Opened(WeaponAvatar)`, `WeaponReplaced(WeaponData)`. Existing display is a cyan cube and basic menu. Cursor and avatar input are released/restored while open.
- `WeaponAvatar`: `OwnedWeapons`, `OwnsWeapon`, `AcquireWeapon`, `WeaponAcquired`, `ActiveSlot`, `ReplaceCurrentWeapon`, `SetCheckpoint`, `InteractionOpen`.
- `EnemyWaveSpawner`: `BeginWaves(count, delay)`, `WavesSpawned`, `AliveCount`, `IsComplete`, `WaveStarted(int)`. Spawns are constrained to serialized arena bounds.
- `Enemy/MidBoss.cs`: alternates existing melee/ranged attacks after each fired attack. `DamageMultiplier=1.8`; EnemyAttack now has serialized `damageMultiplier` (default 1) and `SetKind`, approved by coordinator message msg_600ee9355c56. Ordinary enemies retain original damage and attack behavior.
- `Editor/WeaponAvatarSceneBuilder.Stage.cs`: `RoadWidth=12`, `ArenaWidth=28`, all layout positions and plain signs/facility construction. `Stage` object has road/wall tiles, doors, triggers, MapRevealFocus, enemy templates/groups, and facilities. Common builder calls `BuildStage` instead of the previous test arena and endless spawner.

## Validation evidence
- Final `script recompile_status`: completed, failed=false, compilationFailed=false, errors=[].
- Final eval Build: success=true, no diagnostics. Active scene `Assets/Scenes/WeaponAvatarScene.unity`; editor isPlaying=false.
- Scene readback: StageManager=1, StageDoor=10 (8 arena doors + 2 branch gates), WeaponSwapFacility=7, StageTrigger=9, MidBoss=3 (including temporary final boss).
- Slot readback: Avatar_EvolvedShears / Avatar_ChainsawRuler; non-null reserves=0; rewards Avatar_Rifle / Avatar_Hammer.
- Serialized boss HP=500,500,1400; damage multipliers all 1.8; default mobs 1. Wave bounds center (0,0,22), half-size (12,12).
- Saved scene timestamp 2026-10-31 17:00:17 KST. Four generated Avatar weapon assets saved at 17:00:16. Representative values: shears first bodyYaw=-14/leap=0; chainsaw first bodyYaw=360/leap=1. No moveset or Feedback source files were edited.
- Builder additionally creates normal generated materials StageDoor.mat and Facility.mat under the existing material directory, plus Unity .meta files for new scripts/folder.

## Player verification and limits
Play was explicitly excluded. The user should verify road feel, camera framing at the fork, jumping/dashing against doors, opposite-path closure, wave duration, melee/ranged midboss cadence, both possible reward/facility swaps and swap special, death/checkpoint persistence, and final clear panel. Bosses reuse Enemy's existing slice/crush/piercing instant-kill rules; high HP does not override those mechanics, so timing/boss survivability requires later tuning. The final clear currently requires boss and its small escort group, not just the boss. Doors use a 28 m width even on 12 m road sections, with the excess outside the walkable road.

## Differences from original default plan
The coordinator explicitly superseded the IMGUI map screen and detached room transitions with the C plan. The implementation therefore uses a continuous route, camera map reveal, physical branch triggers, and last-trigger checkpoints. EnemyAttack was minimally extended with coordinator approval. Reward equip is manual at the next facility, ownership is granted immediately. There are no further unapproved scope changes.

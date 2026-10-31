# Phase 2 — Enemy attacks, waves, avatar health

Implemented in the existing WeaponAvatarScene builder; SampleScene builder and weapon-family source files were not edited.

## Validation

- Final Unity recompile: `completed`, `failed: false`, `errors: []`, `compilationFailed: false`.
- `eval` called `WeaponAvatarSceneBuilder.Build()` successfully with Play mode false.
- Scene saved at `2026-10-31T07:40:35.1888319Z`; one active EnemyWaveSpawner, two inactive Melee/Ranged templates, no old fixed active enemies.
- Serialized template `respawn` is false for both; ordinary Enemy defaults to true and retains its original chase/respawn behavior.
- Avatar HP 100; generated Avatar_Hammer heldScale 0.4, combo bodyYaw/leap all 0, and four current Avatar weapon assets refreshed by the builder.
- No Play mode, screenshot, Git operation, administrator operation, or weapon-family source edit was performed. Runtime feel and collisions remain for manual Play verification.

## Events and methods for the effects worker

`EnemyAttack` exposes `event Action<EnemyAttack>`: `TelegraphStarted`, `TelegraphEnded`, `AttackFired`, `AttackCancelled`.

- Begin: `BeginTelegraph(Vector3 direction)` freezes `AimDirection`, stops desired movement, enables the simple ground marker.
- Fire: `FireAttack()` ends the telegraph, applies a melee cone hit or calls `EnemyProjectile.Fire`, then emits `AttackFired`.
- Cancel: `CancelAttack()` ends the telegraph and emits `AttackCancelled`. `TelegraphEnded` fires on both normal release and cancellation before the specific event.
- Read-only `Kind`, `IsTelegraphing`, `AimDirection`, `AttackOrigin`, `TelegraphProgress` support visuals.
- `Enemy.TelegraphColor` overlays the normal body color while preserving the existing hit-flash precedence.
- Heavy/crush cancels windup. Ordinary hits pause windup while stagger/knockback resolves; setting `CancelTelegraphOnLightHit = true` cancels on ordinary hits too. Dead/pinned/staggered/knocked targets cannot release an attack; death or a dead player cancels the windup.

## Tuning locations

| File | Constants / configuration |
|---|---|
| EnemyAttack.cs | MeleeTelegraph 0.35s, RangedTelegraph 0.8s, MeleeRange 2.3m, MeleeHalfAngle 55°, RangedDistance 9m, RangedRange 17m, AttackCooldown 1.6s, damage 15/10, CancelTelegraphOnLightHit false |
| EnemyProjectile.cs | Speed 9m/s, Radius 0.16m, Lifetime 4s; swept sphere collision, ignores Enemy colliders and stops at walls/player |
| EnemyWaveSpawner.cs | MaxAlive 16, WaveSize 5, WaveInterval 7s, SpawnRadius 15m; mixed batches, position checks and arena bounds |
| WeaponAvatar.cs | MaxHp 100, HurtInvulnerability 0.65s, RespawnDelay 2s, HurtKnockback 7, HurtDrag 12; TakeDamage(int, Vector3 from), Hp, IsDead, Center |
| WeaponAvatarSceneBuilder.Enemies.cs | Melee/Ranged template materials, type assignment, no template self-respawn; reuses GameSceneBuilder.BuildEnemy |

## Manual Play checks

1. Five enemies arrive immediately; subsequent mixed batches arrive every seven seconds up to sixteen living enemies. Red enemies approach; purple enemies maintain distance.
2. Melee short warning cone versus longer fixed ranged aim line; walk, jump or dash away from attacks/projectiles.
3. Heavy attacks interrupt windups; ordinary attacks pause them until impairment resolves; no attacks while dead or pinned.
4. Upper-left HP decreases by 15/10, turns yellow during hurt/dash immunity, ignores repeated hits during immunity, and pushes the avatar away from the source.
5. HP zero hides the weapon and stops control; two seconds later the same equipped weapon returns at the original spawn with full HP and brief immunity.
6. SampleScene retains its existing dummy enemy chase/respawn behavior (source compatibility reviewed; no Play run).

Generated output includes WeaponAvatarScene.unity, current Avatar weapon assets, EnemyRanged.mat and new Unity script/material meta files.

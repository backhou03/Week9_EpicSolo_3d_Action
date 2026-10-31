# Phase 5 — Bottom-center combat HUD

## Applied
- Replaced AvatarHud's lower-right text panel with a centered 652-unit, five-card combat bar: RMB dash, E skill, Q ultimate, and weapon slots 1/2. Uses existing Malgun Gothic/Arial fonts, dark translucent panels, gold/cyan accents, and proportional scale constrained by screen width and height.
- Dash and E show vertical readiness fill plus remaining seconds; available skills highlight, and E names vary by weapon (shears: 연속 절단, chainsaw: 오버클럭, hammer: 돌진 강화, rifle: 폭발탄 장전).
- Q fills from UltimateGauge 0..1 and pulses when full. While an ultimate is active it shows the real remaining duration read from its existing timer.
- Slots show abbreviated weapon names, active-slot gold emphasis, and a 1.6-second 특수스킬! notice after switching weapons or replacing the active slot. Initial equip and same-weapon respawn do not trigger this notice.
- HP now spans immediately above the bar. Ammo/reload/moveset Status and charge messages sit above HP. Acquisition notices and the F facility prompt appear near the bar; stage status remains top-center, and the existing clear message and crosshair remain.
- Removed the long duplicate general controls/owned-weapon text from the old lower-right panel.
- Facility selection now uses horizontally arranged cards above the combat bar. Header explicitly identifies the active replacement slot and the other retained slot, with instructions to close F then press 1/2 to change the target. Carried weapons stay disabled, ownership and replacement/special behavior are unchanged. Horizontal scrolling supports lists wider than the panel.

## Scope and approval
Changed AvatarHud.cs, AvatarMoveset.cs, ShearsMoveset.cs, HammerMoveset.cs, ChainsawMoveset.cs, RifleMoveset.cs, and only display-related fields/OnGUI in Stage/WeaponSwapFacility.cs. WeaponAvatar.cs, builders, WeaponData, scenes, and all other Stage files were untouched. Movesets received only SkillName and UltimateTimeRemaining read-only property overrides; coordinator explicitly approved the extra timer override through Orca ask. No new gameplay state or behavior was added.

## Validation
Unity script recompile returned status=completed, failed=false, errors=[], compilationFailed=false. Follow-up recompile_status independently reported the same zero-error result. Read-only eval returned isPlaying=false, isCompiling=false, compilationFailed=false, active scene Assets/Scenes/WeaponAvatarScene.unity. No builder run was needed because builder/data were unchanged. No play testing, screenshots, Git operations, or administrator operations were performed; sandbox-external execution was used only to reach the Orca runtime (and initial file reads after a sandbox backend startup error).

## User verification still needed
Check actual screen layout at target resolutions, cooldown fills and E names for all four weapons, full Q pulse and each ultimate countdown, 1/2 and facility-swap special notices, ammo/charge readability, and the facility replacement labels/card selection. Runtime layout and interaction were not exercised because play/screenshot checks were excluded from the task.

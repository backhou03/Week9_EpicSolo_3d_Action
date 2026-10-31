using UnityEngine;
using UnityEngine.InputSystem;

// Stock locomotion, magazine fire and recoil skills share the same avatar.
public class RifleMoveset : AvatarMoveset
{
    const float HopInterval = 0.3f;
    const float HopHeight = 0.4f;
    const float BounceSpeed = 5f;
    const float GroundDrag = 16f;
    const float AirAcceleration = 4f;
    const float AirSpeed = 4f;
    const float FireMoveScale = 0.6f;
    const float FireGlideAcceleration = 30f;
    const float JumpFlipTime = 0.06f;
    const float JumpShotInterval = 0.05f;
    const float MuzzleDownPitch = 90f;
    const float LandSquash = 0.8f;
    const float LandSquashTime = 0.1f;
    const float ShotPitch = 7f;
    const float KickDecay = 70f;
    const float AimTurnSpeed = 1800f;
    const float ShotRecoil = 0.08f;
    const float ShotRecoilTime = 0.06f;
    const float ShotStagger = 0.18f;
    const float ShotKnockbackScale = 0.3f;
    const int MagazineSize = 40;
    const float ReloadTime = 1.6f;
    const float ExplosiveCooldown = 8f;
    const float ExplosionRadius = 2f;
    const float ExplosionDamageScale = 0.7f;
    const float DashDistance = 3.5f;
    const float DashTime = 0.18f;
    const int DashShots = 3;
    const float DashShotGap = 0.07f;
    const int SpecialShots = 8;
    const float SpecialInterval = 0.065f;
    const float SpecialRange = 8f;
    const float SpecialHalfAngle = 100f;
    const float SpecialKnockback = 12f;
    const float UltimateDuration = 60f;
    const int MachineMagazineSize = 120;
    const float MachineFireScale = 0.45f;
    const float MachineDamageScale = 1.6f;
    const float MachineReloadScale = 0.5f;
    const float MachineSkillCooldown = 5f;
    const float MachineExplosionScale = 1.5f;
    const float MachineDashScale = 1.4f;
    const float SpecialStagger = 0.45f;
    const float ShotShake = 0.08f;
    const float ShotShakeTime = 0.06f;
    const float PoseSharpness = 35f;
    const float RisingPitchOffset = 14f;
    const float KickBackDistance = 0.07f;
    const float KickRecoverRate = 14f;
    const float BarrelSquash = 0.08f;
    const float MachineKickScale = 0.7f;
    const float MachineRollKick = 3f;
    const float RollKick = 1.5f;
    const float HoverKickUp = 0.04f;
    const float HoverBobHeight = 0.03f;
    const float HoverBobRate = 20f;
    const float HoverSwayRoll = 3f;
    const float HoverSwayRate = 9f;
    const float DashStretch = 0.25f;
    const float DashPitch = 12f;
    const float DashKickBack = 0.2f;
    const float ReloadRoll = 40f;
    const float ReloadDip = 0.12f;
    const float ReloadJoltDrop = 0.09f;
    const float ReloadJoltBack = 0.05f;
    const float ReloadJoltDecay = 8f;
    const float SpecialSweep = 80f;
    const float SpecialShake = 0.07f;
    static readonly float[] ReloadBeatAt = { 0.3f, 0.65f, 0.88f };
    static readonly float[] ReloadBeatPitch = { 0.8f, 1f, 1.35f };
    static readonly float[] ReloadBeatJolt = { 0.5f, 1f, 0.8f };
    static readonly Color SkillColor = new Color(1f, 0.55f, 0.15f, 0.9f);
    static readonly Color SpecialColor = new Color(1f, 0.9f, 0.55f, 0.8f);
    static readonly SwingData HeavyShot = new SwingData { heavy = true };

    Camera aimCamera;
    RifleFx fx;
    Transform machineVisual, rifleSight, machineSight;
    Renderer[] rifleRenderers;
    bool[] rifleRendererEnabled;
    float hopTimer, fireTimer, jumpShotTimer, jumpFlipTimer, squashTimer, pitchKick, rollKick, recoilKick;
    int jumpShotsLeft;
    bool firing, jumpQueued, wasGrounded;
    Vector3 aimPoint;
    int rounds = MagazineSize, explosiveRounds, specialShotsLeft;
    int dashShotsLeft;
    float dashShotTimer;
    Vector3 dashShotDirection;
    float reloadLeft, reloadTotal, reloadJolt, ultimateLeft, specialTimer, recoilDashLeft;
    int reloadBeat;
    Vector3 specialForward;

    // The trigger must not block E or Q while it is held.
    public override bool Busy => base.Busy || specialShotsLeft > 0;
    protected override float SkillCooldown => MachineGun ? MachineSkillCooldown : ExplosiveCooldown;
    public override string SkillName => "폭발탄 장전";
    public override float UltimateTimeRemaining => Mathf.Max(0f, ultimateLeft);
    bool MachineGun => ultimateLeft > 0f;
    bool MachineVisual => machineVisual != null && machineVisual.gameObject.activeSelf;
    Transform ActiveVisual => MachineVisual ? machineVisual : visual;
    Vector3 MuzzleLocal => MachineVisual ? weapon.rifleMachineMuzzle : weapon.rifleMuzzle;
    int Capacity => MachineGun ? MachineMagazineSize : MagazineSize;
    public override float ChargeProgress => 0f;
    public override string Status => $"{(MachineGun ? $"기관총 {ultimateLeft:F0}s · " : "")}탄 {rounds}/{Capacity}"
        + (reloadLeft > 0f ? $" · 재장전 {reloadLeft:F1}s" : " · R 재장전")
        + (explosiveRounds > 0 ? $" · 폭발탄 {explosiveRounds}" : "") + " · V 근접공격";

    public override void Configure(WeaponAvatar owner, WeaponData data, Transform weaponVisual)
    {
        base.Configure(owner, data, weaponVisual);
        aimCamera = owner.CameraRig != null ? owner.CameraRig.GetComponent<Camera>() : Camera.main;
        var go = new GameObject("RifleFx");
        go.transform.SetParent(transform, false);
        fx = go.AddComponent<RifleFx>();
        fx.Configure(owner.CameraRig);
        rifleRenderers = visual.GetComponentsInChildren<Renderer>(true);
        rifleRendererEnabled = System.Array.ConvertAll(rifleRenderers, r => r.enabled);
        rifleSight = MakeSight(visual, data.rifleSightRear, data.rifleSightFront);
        if (data.rifleMachinePrefab != null)
        {
            machineVisual = Instantiate(data.rifleMachinePrefab, visual.parent).transform;
            machineVisual.localPosition = data.rifleMachineGrip;
            machineVisual.localRotation = Quaternion.identity;
            machineVisual.localScale = Vector3.one * data.rifleMachineScale;
            foreach (var collider in machineVisual.GetComponentsInChildren<Collider>()) Destroy(collider);
            machineSight = MakeSight(machineVisual, data.rifleMachineSightRear, data.rifleMachineSightFront);
            machineVisual.gameObject.SetActive(false);
        }
    }

    public override void Teardown()
    {
        firing = false;
        ultimateLeft = 0f;
        SetMachineVisual(false);
        if (machineVisual != null) Destroy(machineVisual.gameObject);
        if (avatar.CameraRig != null) avatar.CameraRig.Sight = null;
        ReleaseFx();
        base.Teardown();
    }

    void OnDestroy()
    {
        if (machineVisual != null) Destroy(machineVisual.gameObject);
        ReleaseFx();
    }

    static Transform MakeSight(Transform model, Vector3 rear, Vector3 front)
    {
        var sight = new GameObject("Sight").transform;
        sight.SetParent(model, false);
        sight.localPosition = rear;
        Vector3 forward = front - rear;
        sight.localRotation = forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward, Vector3.up) : Quaternion.identity;
        return sight;
    }

    void SetMachineVisual(bool active)
    {
        if (machineVisual == null) return;
        machineVisual.gameObject.SetActive(active);
        for (int i = 0; i < rifleRenderers.Length; i++)
            if (rifleRenderers[i] != null) rifleRenderers[i].enabled = !active && rifleRendererEnabled[i];
        var cameraRig = avatar.CameraRig;
        if (cameraRig != null && (cameraRig.Sight == rifleSight || cameraRig.Sight == machineSight))
            cameraRig.Sight = active ? machineSight : rifleSight;
    }

    void ReleaseFx()
    {
        if (fx != null) Destroy(fx.gameObject);
        fx = null;
    }

    protected override void CancelAttack()
    {
        base.CancelAttack();
        firing = jumpQueued = false;
        jumpShotsLeft = 0;
        specialShotsLeft = 0;
        if (avatar.CameraRig != null) avatar.CameraRig.Sight = null;
    }

    protected override void StartSwing(bool alt)
    {
        firing = false;
        jumpShotsLeft = 0;
        jumpQueued = false;
        jumpFlipTimer = squashTimer = pitchKick = 0f;
        base.StartSwing(alt);
    }

    protected override void TickAttack(AvatarInput input, float dt)
    {
        if (Keyboard.current != null && Keyboard.current[Key.R].wasPressedThisFrame) BeginReload();
        if (specialShotsLeft > 0)
        {
            firing = false;
            return;
        }
        // Only the V key reaches the base attack buffer, as the stock combo.
        TickCombo(input.melee, true);
        firing = input.attackHeld && !swinging && reloadLeft <= 0f && rounds > 0;
        if (!firing || dt <= 0f) return;
        aimPoint = FindAimPoint(weapon.rifleAimAssist);
        FaceYaw(YawTo(aimPoint), AimTurnSpeed, dt);
        if (fireTimer > 0f) return;
        fireTimer = Mathf.Max(0.02f, weapon.fireInterval * (MachineGun ? MachineFireScale : 1f));
        Fire();
    }

    void BeginReload()
    {
        if (reloadLeft > 0f || rounds >= Capacity) return;
        reloadLeft = reloadTotal = ReloadTime * (MachineGun ? MachineReloadScale : 1f);
        reloadBeat = 0;
        reloadJolt = 0f;
        firing = false;
    }

    protected override void OnSkill()
    {
        rounds = Capacity;
        explosiveRounds = Capacity;
        reloadLeft = fireTimer = 0f;
        firing = false;
        fx.Cast(transform.position, SkillColor, 2.2f);
    }

    protected override void OnUltimate()
    {
        ultimateLeft = UltimateDuration;
        rounds = Capacity;
        reloadLeft = fireTimer = 0f;
        firing = false;
        fx.Ultimate(transform.position);
        SetMachineVisual(true);
    }

    protected override void OnDash(Vector3 dir)
    {
        // Preserve the requested movement direction; only the backward burst is assisted.
        transform.rotation = Quaternion.LookRotation(-dir, Vector3.up);
        dashShotDirection = -dir;
        FireDashShot();
        dashShotsLeft = DashShots - 1;
        dashShotTimer = DashShotGap;
        recoilDashLeft = DashTime;
        avatar.GrantInvulnerability(DashTime);
        avatar.Lunge(dir, DashDistance * (MachineGun ? MachineDashScale : 1f), DashTime);
        fx.DashStart(transform.position, dir, transform, DashTime, MachineGun);
    }

    // One shot of the backward dash burst; an empty magazine only propels.
    void FireDashShot()
    {
        var target = FindAssistTarget(weapon.fireRange, 45f, dashShotDirection);
        aimPoint = target != null ? target.Center : Muzzle + dashShotDirection * weapon.fireRange;
        if (rounds > 0 && reloadLeft <= 0f) Fire(false);
        else PropulsionShot(dashShotDirection);
    }

    protected override void OnSpecial()
    {
        CancelAttack();
        var target = FindAssistTarget(SpecialRange, SpecialHalfAngle);
        specialForward = target != null
            ? Vector3.ProjectOnPlane(target.Center - transform.position, Vector3.up).normalized : CameraForward;
        transform.rotation = Quaternion.LookRotation(specialForward, Vector3.up);
        specialShotsLeft = SpecialShots * (MachineGun ? 2 : 1);
        specialTimer = 0f;
        fx.Ring(transform.position + Vector3.up * 0.05f, SpecialRange, 0.35f, SpecialColor, 0.2f);
        fx.Punch(0.2f, 0.2f, -5f, 0.3f);
    }

    void TickState(float dt)
    {
        fireTimer = Mathf.Max(0f, fireTimer - dt);
        recoilDashLeft = Mathf.Max(0f, recoilDashLeft - dt);
        if (dashShotsLeft > 0 && (dashShotTimer -= dt) <= 0f)
        {
            dashShotsLeft--;
            dashShotTimer = DashShotGap;
            FireDashShot();
        }
        if (ultimateLeft > 0f)
        {
            ultimateLeft = Mathf.Max(0f, ultimateLeft - dt);
            if (!MachineGun)
            {
                SetMachineVisual(false);
                rounds = Mathf.Min(rounds, MagazineSize);
                explosiveRounds = Mathf.Min(explosiveRounds, MagazineSize);
            }
        }
        fx.SetMachineGun(transform, ultimateLeft);
        if (reloadLeft > 0f)
        {
            reloadLeft = Mathf.Max(0f, reloadLeft - dt);
            TickReloadBeats();
            if (reloadLeft <= 0f)
            {
                rounds = Capacity;
                fx.Ready(Muzzle);
            }
        }
        if (specialShotsLeft <= 0 || dt <= 0f) return;
        specialTimer -= dt;
        if (specialTimer > 0f) return;
        specialTimer = SpecialInterval * (MachineGun ? MachineFireScale : 1f);
        specialShotsLeft--;
        // Sweep the body and the tracers across the cone; damage below still covers the whole cone.
        int total = SpecialShots * (MachineGun ? 2 : 1);
        int index = total - specialShotsLeft - 1;
        Vector3 fan = Quaternion.Euler(0f, Mathf.Lerp(-SpecialSweep, SpecialSweep, index / (total - 1f)), 0f) * specialForward;
        transform.rotation = Quaternion.LookRotation(fan, Vector3.up);
        // The entrance burst is a skill payload, separate from the magazine.
        foreach (var enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (enemy.IsDead) continue;
            Vector3 to = enemy.Center - transform.position;
            Vector3 planar = Vector3.ProjectOnPlane(to, Vector3.up);
            if (to.magnitude > SpecialRange || Vector3.Angle(specialForward, planar) > SpecialHalfAngle) continue;
            ApplyShotHit(enemy, enemy.Center, planar.normalized, ShotDamage,
                SpecialKnockback * (MachineGun ? MachineDamageScale : 1f), SpecialStagger);
        }
        PropulsionShot(fan, SpecialRange, 1.2f, SpecialShake);
        if (specialShotsLeft > 0) return;
        fx.Ring(transform.position + Vector3.up * 0.05f, SpecialRange * 0.6f, 0.25f, SpecialColor, 0.14f);
        fx.Punch(0.25f, 0.2f, 3f, 0.2f);
    }

    void TickReloadBeats()
    {
        reloadJolt = Mathf.MoveTowards(reloadJolt, 0f, ReloadJoltDecay * Time.deltaTime);
        float progress = 1f - reloadLeft / reloadTotal;
        while (reloadBeat < ReloadBeatAt.Length && progress >= ReloadBeatAt[reloadBeat])
        {
            fx.Click(ReloadBeatPitch[reloadBeat], reloadBeat == 1 ? 0.9f : 0.6f);
            reloadJolt = ReloadBeatJolt[reloadBeat];
            reloadBeat++;
        }
    }

    protected override void Locomote(AvatarInput input, float dt)
    {
        TickState(dt);
        hopTimer = Mathf.Max(0f, hopTimer - dt);
        squashTimer = Mathf.Max(0f, squashTimer - dt);
        pitchKick = Mathf.MoveTowards(pitchKick, 0f, KickDecay * dt);
        rollKick = Mathf.MoveTowards(rollKick, 0f, KickDecay * dt);
        recoilKick = Mathf.MoveTowards(recoilKick, 0f, KickRecoverRate * dt);
        bool grounded = avatar.IsGrounded;
        if (grounded && !wasGrounded) squashTimer = LandSquashTime;
        wasGrounded = grounded;

        if (recoilDashLeft > 0f)
        {
            avatar.PlanarVelocity = Vector3.zero;
            // Thrust pose: the rifle stretches and pitches back hardest at the start of the dash.
            float thrust = recoilDashLeft / DashTime;
            float thin = 1f - 0.1f * thrust;
            EaseMotion(new Vector3(0f, 0f, -DashKickBack * thrust), Quaternion.Euler(pitchKick - DashPitch * thrust, 0f, 0f),
                new Vector3(thin, thin, 1f + DashStretch * thrust), PoseSharpness, dt);
            return;
        }

        if (swinging)
        {
            UpdateFacing(input.wish, dt);
            Accelerate(input.wish * (AirSpeed * MoveSpeedScale * weapon.swingMoveScale), GroundDrag, dt);
            RelaxMotion(dt);
            return;
        }

        if (!firing && specialShotsLeft <= 0) UpdateFacing(input.wish, dt);
        bool moving = input.wish.sqrMagnitude > 0.01f;

        // Jump: flip muzzle down first, then each downward shot pushes the rifle higher.
        if (input.jump && !jumpQueued && jumpShotsLeft == 0)
        {
            jumpQueued = true;
            jumpFlipTimer = JumpFlipTime;
        }
        if (jumpQueued)
        {
            jumpFlipTimer -= dt;
            if (jumpFlipTimer <= 0f)
            {
                jumpQueued = false;
                jumpShotsLeft = Mathf.Max(1, weapon.rifleJumpShots);
                jumpShotTimer = 0f;
                hopTimer = HopInterval;
            }
        }
        jumpShotTimer -= dt;
        if (jumpShotsLeft > 0 && jumpShotTimer <= 0f && dt > 0f)
        {
            jumpShotsLeft--;
            jumpShotTimer = JumpShotInterval;
            int shots = Mathf.Max(1, weapon.rifleJumpShots);
            // Each shot raises the launch speed until the last one reaches the jump height.
            avatar.Hop(weapon.rifleJumpHeight * (shots - jumpShotsLeft) / shots, true);
            PropulsionShot(Vector3.down);
        }
        bool jumpShooting = jumpQueued || jumpShotsLeft > 0;

        if (firing && !grounded)
        {
            // WeaponAvatar adds gravity after Tick: cancel it so cc.Move receives zero vertical speed.
            avatar.VerticalVelocity = -avatar.Gravity * dt;
        }

        if (firing)
            Accelerate(input.wish * (BounceSpeed * MoveSpeedScale * FireMoveScale), FireGlideAcceleration, dt);
        else if (!grounded)
            Accelerate(input.wish * (BounceSpeed * MoveSpeedScale), AirAcceleration, dt);
        else if (moving && !jumpShooting && avatar.VerticalVelocity <= 0f && hopTimer <= 0f && squashTimer <= 0f)
        {
            // Stock bounce: spring off the ground once the landing squash has played.
            avatar.Hop(HopHeight);
            avatar.PlanarVelocity = input.wish * (BounceSpeed * MoveSpeedScale);
            hopTimer = HopInterval;
        }
        else
            Accelerate(Vector3.zero, GroundDrag, dt);

        if (firing || specialShotsLeft > 0)
        {
            // Each shot kicks the rifle back and squashes it along the barrel; hovering adds bob and sway.
            bool hovering = firing && !grounded;
            var kickPosition = new Vector3(0f, hovering ? HoverKickUp * recoilKick : 0f, -KickBackDistance * recoilKick);
            float roll = rollKick;
            if (hovering)
            {
                kickPosition.y += Mathf.Sin(Time.time * HoverBobRate) * HoverBobHeight;
                roll += Mathf.Sin(Time.time * HoverSwayRate) * HoverSwayRoll;
            }
            EaseMotion(kickPosition, Quaternion.Euler(pitchKick, 0f, roll), new Vector3(1f, 1f, 1f - BarrelSquash * recoilKick), PoseSharpness, dt);
        }
        else if (jumpShooting)
            PivotPose(MuzzleDownPitch, ModelPoint(MuzzleLocal), new Vector3(1f, 1f, 1f - BarrelSquash * recoilKick), dt);
        else
        {
            float pitch = !grounded && avatar.VerticalVelocity > 0f ? weapon.rifleStandPitch + RisingPitchOffset : weapon.rifleStandPitch;
            Vector3 scale = grounded && squashTimer > 0f
                ? new Vector3(1f / Mathf.Sqrt(LandSquash), 1f / Mathf.Sqrt(LandSquash), LandSquash)
                : Vector3.one;
            Vector3 standPivot = weapon.rifleStandPivot != Vector3.zero ? weapon.rifleStandPivot : weapon.bladeTip;
            float reloadRoll = 0f;
            Vector3 reloadOffset = Vector3.zero;
            if (reloadLeft > 0f)
            {
                // Tilt the rifle sideways and drop it while the magazine swaps; each beat jolts it.
                float progress = 1f - reloadLeft / reloadTotal;
                float tilt = Mathf.Clamp01(Mathf.Min(progress / 0.2f, (1f - progress) / 0.15f));
                reloadRoll = ReloadRoll * tilt;
                reloadOffset = new Vector3(0f, -ReloadDip * tilt - ReloadJoltDrop * reloadJolt, -ReloadJoltBack * reloadJolt);
            }
            Vector3 pivot = MachineVisual ? weapon.rifleMachineStandPivot : standPivot;
            PivotPose(pitch, ModelPoint(pivot), scale, dt, roll: reloadRoll, offset: reloadOffset);
        }
    }

    // Tilts the rifle by pitch with the given point (Motion space) held `height` above the avatar's origin
    // (0 = resting on the ground). Motion's z axis is the rifle's length, so scale.z squashes it along the barrel.
    void PivotPose(float pitch, Vector3 pivot, Vector3 scale, float dt, float height = 0f, float sharpness = PoseSharpness,
        float roll = 0f, Vector3 offset = default)
    {
        var rotation = Quaternion.Euler(pitch, 0f, roll);
        var anchor = new Vector3(0f, height - weapon.restPosition.y, 0f) + offset;
        EaseMotion(anchor - rotation * Vector3.Scale(scale, pivot), rotation, scale, sharpness, dt);
    }

    Vector3 ModelPoint(Vector3 point) => ActiveVisual.localPosition
        + ActiveVisual.localRotation * Vector3.Scale(ActiveVisual.localScale, point);

    Vector3 Muzzle => ActiveVisual.TransformPoint(MuzzleLocal);

    Vector3 FindAimPoint(bool assist)
    {
        var target = assist ? FindAssistTarget(weapon.fireRange, 45f) : null;
        if (target != null) return target.Center;
        Ray ray = aimCamera != null
            ? aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
            : new Ray(Muzzle, transform.forward);
        return FirstHit(ray.origin, ray.direction, weapon.fireRange, out var hit)
            ? hit.point : ray.GetPoint(weapon.fireRange);
    }

    bool FirstHit(Vector3 origin, Vector3 direction, float range, out RaycastHit hit)
    {
        var hits = Physics.RaycastAll(origin, direction, Mathf.Max(0f, range), ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var candidate in hits)
        {
            if (candidate.transform.IsChildOf(avatar.transform)) continue;
            hit = candidate;
            return true;
        }
        hit = default;
        return false;
    }

    int ShotDamage => Mathf.Max(1, Mathf.RoundToInt(weapon.damage * (MachineGun ? MachineDamageScale : 1f)));

    void ApplyShotHit(Enemy enemy, Vector3 point, Vector3 direction, int damage, float knockback, float stagger)
    {
        if (enemy == null || enemy.IsDead) return;
        var info = new HitInfo
        {
            damage = damage,
            direction = direction,
            point = point,
            knockback = knockback,
            stagger = stagger,
            heavy = weapon.rifleHeavyHit,
        };
        enemy.TakeHit(info);
        avatar.RecordHit(info.damage, enemy.IsDead);
        if (HitFeedback.Instance == null) return;
        if (weapon.rifleHeavyHit) HitFeedback.Instance.PlayHit(info, weapon, HeavyShot, enemy.IsDead);
        else HitFeedback.Instance.PlayTick(info);
    }

    void Explode(Vector3 point, Enemy directTarget)
    {
        float radius = ExplosionRadius * (MachineGun ? MachineExplosionScale : 1f);
        fx.Explosion(point, radius, MachineGun);
        foreach (var enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (enemy.IsDead || enemy == directTarget || Vector3.Distance(point, enemy.Center) > radius + enemy.Radius) continue;
            Vector3 direction = Vector3.ProjectOnPlane(enemy.Center - point, Vector3.up).normalized;
            if (direction.sqrMagnitude < 0.01f) direction = transform.forward;
            ApplyShotHit(enemy, enemy.Center, direction, Mathf.Max(1, Mathf.RoundToInt(ShotDamage * ExplosionDamageScale)),
                weapon.knockback, ShotStagger * 2f);
        }
    }

    void Fire(bool recoil = true)
    {
        if (rounds <= 0 || reloadLeft > 0f) return;
        rounds--;
        bool explosive = explosiveRounds > 0;
        if (explosive) explosiveRounds--;
        Vector3 muzzle = Muzzle;
        Vector3 direction = aimPoint - muzzle;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        Vector3 end = muzzle + direction * weapon.fireRange;
        bool struck = FirstHit(muzzle, direction, weapon.fireRange, out var hit);
        Enemy target = null;
        if (struck)
        {
            end = hit.point;
            target = hit.collider.GetComponentInParent<Enemy>();
            ApplyShotHit(target, hit.point, direction, ShotDamage, weapon.knockback * ShotKnockbackScale, ShotStagger);
            if (explosive) Explode(hit.point, target);
        }
        // The dash shot is part of the dash: its own camera kick is skipped and the blast is bigger.
        float shake = !recoil ? 0f : weapon.rifleHeavyHit ? weapon.shake : ShotShake;
        fx.Shot(muzzle, direction, end, explosive, MachineGun, shake, recoil ? 1f : 1.8f);
        if (struck && target == null && !explosive) fx.Impact(end, direction);
        if (recoil) avatar.Lunge(Vector3.ProjectOnPlane(-direction, Vector3.up), ShotRecoil, ShotRecoilTime);
        pitchKick = -ShotPitch * (MachineGun ? MachineKickScale : 1f);
        rollKick = Random.Range(-1f, 1f) * (MachineGun ? MachineRollKick : RollKick);
        recoilKick = 1f;
        if (rounds == 0) BeginReload();
    }

    // Visual-only shot that pushes the avatar (jump, special, empty dash): no damage is dealt here.
    void PropulsionShot(Vector3 direction, float range = 3f, float boost = 1.6f, float shake = 0f)
    {
        Vector3 muzzle = Muzzle;
        bool struck = FirstHit(muzzle, direction, range, out var hit);
        Vector3 end = struck ? hit.point : muzzle + direction * range;
        fx.Shot(muzzle, direction, end, false, MachineGun, shake, boost);
        if (struck) fx.Impact(end, direction, direction == Vector3.down);
        recoilKick = 1f;
    }
}

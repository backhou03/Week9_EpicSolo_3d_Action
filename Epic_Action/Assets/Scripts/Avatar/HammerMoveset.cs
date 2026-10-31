using UnityEngine;

// Hammer timing is local: the E buff never changes the shared WeaponData.
public class HammerMoveset : AvatarMoveset
{
    const float MoveSpeed = 7f;
    const float Acceleration = 60f;
    const float JumpHeight = 2.5f;
    const float UppercutHeight = 1.4f;
    const float AttackRange = 3.6f;
    const float AttackHalfAngle = 70f;
    const float LaunchKnockback = 14f;
    const float LaunchUp = 0.95f;
    const float SlamDownSpeed = 20f;
    const float SpinDegreesPerSecond = 720f;
    const int MaxDamageSpins = 7;
    const float DamagePerSpin = 0.4f;
    const float RangePerSpin = 0.35f;
    const float ChargeHopHeight = 1.8f;
    const float AttackBuffer = 0.4f;
    const float SkillDuration = 15f;
    const float SkillAttackSpeed = 1.65f;
    const float SkillRecharge = 20f;
    const float RushRange = 12f;
    const float RushDistance = 8f;
    const float RushDuration = 0.3f;
    const float RushHitRange = 2.4f;
    const float SpecialRange = 5f;
    const float SpecialPoseDuration = 0.35f;
    const float UltimateDuration = 15f;
    const float UltimateScale = 2.3f;
    const float UltimateRange = 5.5f;
    const float UltimateTickInterval = 0.2f;
    const float UltimateTickDamage = 0.45f;
    const float UltimateSpinSpeed = 540f;
    // Bounce locomotion: one full head-over-handle flip per hop, head striking the floor on every landing.
    const float HopDistance = 2f;
    const float HopHeight = 0.35f;
    const float LandSquash = 0.14f;
    const float SquashDecay = 16f;
    const float FloorClearance = 0.02f;
    const float PoseSharpness = 30f;
    const float AttackTurnSpeed = 2400f;
    const float SpinPitch = 90f;
    const float SlamPitch = 105f;
    const float SlamPoseHeight = 0.8f;
    const float LaunchForward = 0.25f;
    const float HitStagger = 0.6f;
    const float DashPoseDuration = 0.18f;
    const float SplashRange = 3f;
    const float SplashDamage = 0.35f;
    internal const float CrushDuration = 0.28f;
    internal const float CrushFlatness = 0.08f;
    internal const float CrushWidth = 1.7f;
    internal const float CrushBurstHeight = 0.2f;

    // Feel only: the cock-back takes the first part of windup and the fall lands exactly when the hit resolves.
    const float CockFraction = 0.4f;
    const float UppercutImpactU = 0.75f;
    const float FollowTiltTime = 0.12f;
    const float SwingSmearSize = 1f;
    const float WindupTilt = 10f;
    const float ImpactOvershoot = 6f;
    const float ImpactReach = 1.7f;
    const float ImpactRadius = 2.2f;
    const float ShakeBase = 0.28f;
    const float FovBase = 2.8f;
    const float HitstopBase = 0.07f;
    const float PunchDecay = 14f;
    const float SpinGrowth = 0.3f;
    const float DashFxDuration = 0.22f;
    const float LandMinSpeed = 7f;
    const float TrailBuffRed = 0.4f;

    int comboStep, activeStep;
    float attackTime, bufferedAt = -10f, lastAttackEnd = -10f;
    float spinDegrees, hopPhase, squash, buffUntil, ultimateUntil, nextUltimateHit;
    float rushUntil, specialUntil, dashUntil;
    bool charging, slamming, strikeApplied, poseHeld;
    float holdTime;
    float modelRadius;
    Bounds localBounds;
    Vector3 rushDirection;
    readonly System.Collections.Generic.HashSet<Enemy> rushHits = new System.Collections.Generic.HashSet<Enemy>();

    HammerFx fx;
    float punch, ultimateStart, dashFxUntil, fallSpeed, spinTier;
    float rushAcc, dashAcc, auraAcc, dustAcc, sparkAcc;
    int lastSpinCount, lastKills;
    bool wasGrounded = true, landSkip, wasRush, wasUltimate, wasBuffed;
    Vector3 dashDirection;
    Color trailNow;

    protected override float SkillCooldown => SkillRecharge;
    public override bool Busy => swinging || charging || slamming || Time.time < ultimateUntil || Time.time < rushUntil;
    public override float ChargeProgress => charging ? Mathf.Clamp01(spinDegrees / (360f * MaxDamageSpins)) : 0f;
    public override string Status => Time.time < ultimateUntil ? $"거대 회전 {ultimateUntil - Time.time:F1}초"
        : charging ? $"회전 {Mathf.FloorToInt(spinDegrees / 360f)}회 · 위력 {Mathf.Min(MaxDamageSpins, Mathf.FloorToInt(spinDegrees / 360f))}/{MaxDamageSpins} · 놓으면 내려찍기"
        : Time.time < buffUntil ? $"공격속도 증가 {buffUntil - Time.time:F1}초" : "";

    Vector3 Head => visual.TransformPoint((weapon.bladeBase + weapon.bladeTip) * 0.5f);
    Vector3 Facing => Quaternion.Euler(0f, swingFacing, 0f) * Vector3.forward;
    static Vector3 Ground(Vector3 p) => new Vector3(p.x, p.y + 0.03f, p.z);

    public override void Configure(WeaponAvatar owner, WeaponData data, Transform weaponVisual)
    {
        base.Configure(owner, data, weaponVisual);
        // Own the same Pose transform while using local attack timings.
        swinger.enabled = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
        {
            foreach (Vector3 corner in BoundsCorners(renderer.localBounds))
                bounds.Encapsulate(visual.InverseTransformPoint(renderer.transform.TransformPoint(corner)));
        }
        localBounds = bounds;
        modelRadius = Mathf.Max(bounds.min.magnitude, bounds.max.magnitude) * weapon.heldScale;
        fx = gameObject.AddComponent<HammerFx>();
        fx.Configure();
        trailNow = weapon.trailColor;
    }

    static Vector3[] BoundsCorners(Bounds b)
    {
        var corners = new Vector3[8];
        for (int i = 0; i < 8; i++) corners[i] = b.center + Vector3.Scale(b.extents,
            new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
        return corners;
    }

    public override void Teardown()
    {
        swinger.enabled = true;
        if (trail != null) trail.Emitting = false;
        if (fx != null) Destroy(fx);
        base.Teardown();
    }

    protected override void CancelAttack()
    {
        base.CancelAttack();
        charging = slamming = false;
        comboStep = 0;
        bufferedAt = -10f;
    }

    protected override void TickAttack(AvatarInput input, float dt)
    {
        if (Time.time < ultimateUntil || Time.time < rushUntil) return;
        if (input.attack) bufferedAt = Time.time;
        if (charging)
        {
            spinDegrees += SpinDegreesPerSecond * dt * AttackSpeed;
            if (!input.attackHeld)
            {
                charging = false;
                slamming = true;
                avatar.VerticalVelocity = -SlamDownSpeed;
                DiveFx();
            }
            return;
        }
        if (slamming)
        {
            avatar.VerticalVelocity = -SlamDownSpeed;
            if (avatar.IsGrounded)
            {
                int spins = Mathf.Min(MaxDamageSpins, Mathf.FloorToInt(spinDegrees / 360f));
                float range = AttackRange + spins * RangePerSpin;
                int hits = Pulse(transform.position, range, 180f,
                    weapon.combo[2].damageScale + spins * DamagePerSpin, false, true);
                slamming = false;
                FinishAttack();
                specialUntil = Time.time + SpecialPoseDuration;
                FinalSlamFx(range, spins, hits);
            }
            return;
        }
        if (swinging)
        {
            attackTime += dt * AttackSpeed;
            if (!strikeApplied && attackTime >= currentSwing.windup)
            {
                strikeApplied = true;
                if (activeStep == 0) avatar.Hop(UppercutHeight, true);
                else avatar.VerticalVelocity = -SlamDownSpeed;
                int hits = Pulse(transform.position, AttackRange, AttackHalfAngle, currentSwing.damageScale,
                    activeStep == 0, activeStep == 1);
                ComboImpactFx(hits);
            }
            if (attackTime >= currentSwing.windup + currentSwing.strike + currentSwing.recovery) FinishAttack();
        }
        if (!Busy && Time.time - bufferedAt <= AttackBuffer) BeginAttack();
    }

    float AttackSpeed => Time.time < buffUntil ? SkillAttackSpeed : 1f;

    void BeginAttack()
    {
        if (Time.time - lastAttackEnd > weapon.comboResetTime) comboStep = 0;
        activeStep = comboStep;
        comboStep = (comboStep + 1) % 3;
        bufferedAt = -10f;
        swingFacing = avatar.CameraRig.Yaw;
        assistTarget = FindAssistTarget();
        if (assistTarget != null) swingFacing = YawTo(assistTarget.transform.position);
        currentSwing = weapon.combo[activeStep];
        attackTime = 0f;
        strikeApplied = false;
        if (activeStep == 2)
        {
            spinDegrees = 0f;
            lastSpinCount = 0;
            charging = true;
            avatar.Hop(ChargeHopHeight, true);
            // Take-off thump: the hammer pushes off the floor before it starts spinning.
            fx.Impact(Ground(transform.position), 1.5f, 0.3f);
            punch += 0.1f;
        }
        else swinging = true;
        HitFeedback.Instance?.PlaySwing(true);
    }

    void FinishAttack()
    {
        swinging = false;
        lastAttackEnd = Time.time;
    }

    // ---- Attack feel --------------------------------------------------------

    // Shared ground hit: rings and dust, camera kick and a hit stop when something was struck.
    void GroundImpact(Vector3 ground, float radius, float power, int hits)
    {
        fx.Impact(ground, radius, power);
        var cam = avatar.CameraRig;
        cam.Shake(ShakeBase * power, 0.22f + 0.14f * power);
        cam.FovPunch(FovBase * power, 0.22f);
        if (hits > 0) fx.Hitstop(HitstopBase * (0.5f + power) + 0.025f * Mathf.Min(lastKills, 2));
        if (power >= 0.5f) HitFeedback.Instance?.PlaySonicImpact(ground + Vector3.up * 0.1f, Vector3.up, true);
        punch = Mathf.Max(punch, 0.07f + 0.08f * power);
    }

    void ComboImpactFx(int hits)
    {
        Vector3 forward = Facing;
        Vector3 ground = Ground(transform.position + forward * ImpactReach);
        if (activeStep == 0)
        {
            // Uppercut: a rising column instead of a wide splash.
            GroundImpact(ground, ImpactRadius * 0.8f, 0.55f, hits);
            fx.Sparks(ground, Vector3.up + forward * 0.3f, 26, 10f, 0.35f, 0.08f, 0.55f);
            fx.DustStream(ground, Vector3.up, 10, 4f, 0.8f);
            fx.Smear(ground + Vector3.up * 1.2f, 1.6f, 0.2f, new Color(1f, 0.85f, 0.5f, 0.4f));
        }
        else
        {
            GroundImpact(ground, ImpactRadius * 1.15f, 0.9f, hits);
            fx.DustStream(ground, forward, 8, 5f, 0.9f);
        }
    }

    void DiveFx()
    {
        Vector3 head = Head;
        fx.Sparks(head, Vector3.up, 16, 7f, 0.8f);
        fx.Smear(head, 2f, 0.2f, new Color(1f, 0.9f, 0.6f, 0.5f));
        avatar.CameraRig.FovPunch(-(3f + 4f * spinTier), 0.3f);
        avatar.CameraRig.Shake(0.1f, 0.12f);
        HitFeedback.Instance?.PlaySwing(true);
    }

    void FinalSlamFx(float range, int spins, int hits)
    {
        float power = 0.6f + 0.12f * spins;
        Vector3 ground = Ground(transform.position);
        GroundImpact(ground, range, power, hits);
        fx.Ring(ground, range * 0.35f, 0.2f, Color.white);
        fx.Sparks(ground + Vector3.up * 0.1f, Vector3.up, 20 + spins * 5, 8f + spins, 0.6f, 0.09f, 0.6f);
        landSkip = true;
        if (spins >= MaxDamageSpins) HitFeedback.Instance?.PlayBreak(ground + Vector3.up * 0.4f);
    }

    // Maps the swing onto: quick cock-back, one accelerating fall that lands as the hit resolves, follow-through, recovery.
    // The pose freezes while the contact hold runs; the swing clock keeps going.
    void SwingPose(out Vector3 pos, out Vector3 euler, out float tilt)
    {
        float w = currentSwing.windup;
        float cock = w * CockFraction;
        float strike = currentSwing.strike;
        float side = activeStep == 0 ? -1f : 1f;
        float impactU = activeStep == 0 ? UppercutImpactU : 1f;
        if (fx.Holding)
        {
            if (!poseHeld) { poseHeld = true; holdTime = attackTime; }
        }
        else poseHeld = false;
        float t = poseHeld ? holdTime : attackTime;
        if (t < cock)
        {
            float u = Mathf.Clamp01(t / cock);
            currentSwing.Evaluate(SwingPhase.Windup, u, weapon.restPosition, weapon.restEuler, weapon, out pos, out euler);
            tilt = -WindupTilt * side * Mathf.SmoothStep(0f, 1f, u);
        }
        else if (t < w)
        {
            float u = Mathf.Clamp01((t - cock) / (w - cock));
            currentSwing.Evaluate(SwingPhase.Strike, u * impactU, weapon.restPosition, weapon.restEuler, weapon, out pos, out euler);
            tilt = Mathf.Lerp(-WindupTilt, ImpactOvershoot, u * u) * side;
        }
        else if (t < w + strike)
        {
            float u = Mathf.Clamp01((t - w) / strike);
            float eased = 1f - (1f - u) * (1f - u);
            currentSwing.Evaluate(SwingPhase.Strike, Mathf.Lerp(impactU, 1f, eased), weapon.restPosition, weapon.restEuler, weapon, out pos, out euler);
            tilt = ImpactOvershoot * side * (1f - Mathf.Clamp01((t - w) / FollowTiltTime));
        }
        else
        {
            float u = Mathf.Clamp01((t - w - strike) / Mathf.Max(0.01f, currentSwing.recovery));
            currentSwing.Evaluate(SwingPhase.Recovery, u, weapon.restPosition, weapon.restEuler, weapon, out pos, out euler);
            tilt = 0f;
        }
    }

    Color TrailColor()
    {
        if (Time.time < ultimateUntil) return new Color(1f, 0.95f, 0.7f, 0.9f);
        if (charging || slamming)
        {
            Color red = new Color(1f, 0.15f, 0.08f, 0.95f);
            return spinTier < 0.5f ? Color.Lerp(weapon.trailColor, red, spinTier * 2f)
                : Color.Lerp(red, new Color(1f, 0.85f, 0.9f, 1f), (spinTier - 0.5f) * 2f);
        }
        if (Time.time < buffUntil) return new Color(1f, TrailBuffRed, 0.12f, 0.85f);
        return weapon.trailColor;
    }

    // ---- Locomotion ---------------------------------------------------------

    protected override void Locomote(AvatarInput input, float dt)
    {
        bool ultimate = Time.time < ultimateUntil;
        if (ultimate)
        {
            transform.Rotate(0f, UltimateSpinSpeed * dt, 0f);
            if (Time.time >= nextUltimateHit)
            {
                nextUltimateHit = Time.time + UltimateTickInterval;
                Pulse(transform.position, UltimateRange, 180f, UltimateTickDamage, false, false, true);
                UltimateTickFx();
            }
        }
        else if (Time.time < rushUntil)
        {
            FaceYaw(Mathf.Atan2(rushDirection.x, rushDirection.z) * Mathf.Rad2Deg, AttackTurnSpeed, dt);
            foreach (var enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
                if (!enemy.IsDead && (enemy.Center - (transform.position + Vector3.up)).sqrMagnitude < RushHitRange * RushHitRange && rushHits.Add(enemy))
                {
                    DealHit(enemy, weapon.damage, rushDirection, weapon.knockback, false);
                    RushHitFx(enemy);
                }
        }
        else if (charging || slamming || swinging) FaceYaw(swingFacing, AttackTurnSpeed, dt);
        else UpdateFacing(input.wish, dt);

        Accelerate(input.wish * (MoveSpeed * MoveSpeedScale * (Busy && !ultimate ? weapon.swingMoveScale : 1f)), Acceleration, dt);
        if (input.jump && avatar.IsGrounded && !Busy)
        {
            avatar.Hop(JumpHeight);
            dashUntil = Time.time + DashPoseDuration;
            // Smash the floor, then spring off it.
            Vector3 ground = Ground(transform.position);
            GroundImpact(ground, 1.6f, 0.25f, 0);
            fx.DustStream(ground, Vector3.up, 6, 3f, 0.7f);
        }

        Transform pose = avatar.Motion.parent;
        Vector3 posePosition = weapon.restPosition;
        Vector3 poseEuler = weapon.restEuler;
        float tilt = 0f;
        if (swinging) SwingPose(out posePosition, out poseEuler, out tilt);
        pose.localPosition = posePosition;
        pose.localRotation = Quaternion.Euler(poseEuler);

        Quaternion rotation = Quaternion.identity;
        float scale = ultimate ? UltimateScale * UltimatePop() : 1f;
        Vector3 position = Vector3.zero;
        bool floorAlign = false;
        float lift = 0f;
        Vector3 squashScale = Vector3.one;
        if (charging || slamming) spinTier = Mathf.Clamp01(spinDegrees / (360f * MaxDamageSpins));
        if (charging) scale *= 1f + SpinGrowth * spinTier;
        if (ultimate || charging)
        {
            // Lie flat and spin: the lowest corner rests on the floor so the head sweeps at body height.
            rotation = Quaternion.Euler(SpinPitch, charging ? spinDegrees : 0f, 0f);
            floorAlign = true;
        }
        else if (slamming || Time.time < specialUntil || Time.time < dashUntil || Time.time < rushUntil)
        {
            rotation = Quaternion.Euler(SlamPitch, 0f, 0f);
            position = new Vector3(0f, SlamPoseHeight, 0f);
        }
        else if (!swinging && input.wish.sqrMagnitude > 0.01f)
        {
            hopPhase += input.wish.magnitude * MoveSpeed * MoveSpeedScale * dt / HopDistance;
            if (hopPhase >= 1f)
            {
                hopPhase -= Mathf.Floor(hopPhase);
                squash = 1f;
                if (avatar.IsGrounded) ReboundFx();
            }
            // Head down at landing, then flips forward over the hop arc.
            rotation = Quaternion.Euler(180f + 360f * Mathf.SmoothStep(0f, 1f, hopPhase), 0f, 0f);
            lift = HopHeight * 4f * hopPhase * (1f - hopPhase);
            floorAlign = true;
        }
        else if (swinging) rotation = Quaternion.Euler(tilt, 0f, 0f);
        squash *= Mathf.Exp(-SquashDecay * dt);
        if (floorAlign && squash > 0.01f) squashScale = new Vector3(1f + squash * LandSquash * 0.5f, 1f - squash * LandSquash, 1f + squash * LandSquash * 0.5f);
        punch *= Mathf.Exp(-PunchDecay * dt);
        Vector3 fullScale = squashScale * (scale * (1f + punch));
        if (floorAlign)
            position = pose.InverseTransformVector(Vector3.up) * (transform.position.y + FloorClearance + lift - LowestY(pose, position, rotation, fullScale));
        EaseMotion(position, rotation, fullScale, PoseSharpness, dt);
        TickFx(dt);
    }

    // World height of the lowest model corner for a given Motion pose.
    float LowestY(Transform pose, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        Matrix4x4 toWorld = pose.localToWorldMatrix * Matrix4x4.TRS(position, rotation, scale)
            * Matrix4x4.TRS(visual.localPosition, visual.localRotation, visual.localScale);
        float low = float.MaxValue;
        foreach (Vector3 corner in BoundsCorners(localBounds)) low = Mathf.Min(low, toWorld.MultiplyPoint3x4(corner).y);
        return low;
    }

    // Grow past the final size, then settle.
    float UltimatePop()
    {
        float t = Time.time - ultimateStart;
        return 1f + 0.2f * Mathf.Exp(-4f * t) * Mathf.Sin(14f * t);
    }

    void ReboundFx()
    {
        Vector3 ground = Ground(transform.position + transform.forward * (modelRadius * 0.4f));
        fx.Dust(ground, 3, 1.8f, 0.5f, 0.45f, 0.5f);
        fx.Ring(ground, 0.9f, 0.22f, new Color(1f, 0.8f, 0.5f, 0.35f));
        avatar.CameraRig.Shake(0.03f, 0.08f);
        punch += 0.03f;
    }

    // Everything that runs every frame: trail, spin build-up, dive streak, dash/rush wake, buff aura, landing.
    void TickFx(float dt)
    {
        Vector3 feet = Ground(transform.position);
        Vector3 head = Head;
        bool ultimate = Time.time < ultimateUntil;
        bool rush = Time.time < rushUntil;
        bool buffed = Time.time < buffUntil;
        bool grounded = avatar.IsGrounded;

        if (trail != null)
        {
            // The ribbon follows the whole arc: cock-back, fall and follow-through.
            bool swingTrail = swinging && attackTime < currentSwing.windup + currentSwing.strike;
            trail.Emitting = swingTrail || charging || slamming || ultimate || rush || Time.time < dashFxUntil;
            Color color = TrailColor();
            if (color != trailNow)
            {
                trailNow = color;
                trail.Configure(weapon.bladeBase, weapon.bladeTip, color);
            }
        }

        if (swinging && attackTime < currentSwing.windup + currentSwing.strike)
            fx.Smear(head, SwingSmearSize, 0.14f, new Color(1f, 0.85f, 0.5f, 0.3f));

        if (charging) ChargeFx(dt, feet, head);
        else lastSpinCount = 0;

        if (slamming)
        {
            fx.Smear(head, 1.3f + spinTier, 0.18f, new Color(1f, 0.8f, 0.5f, 0.4f));
            int n = HammerFx.Steps(ref sparkAcc, 80f, dt);
            if (n > 0) fx.Sparks(head, Vector3.up, n, 3f, 0.4f);
        }

        if (Time.time < dashFxUntil && !rush)
        {
            fx.Smear(transform.position + Vector3.up * 0.9f, 1.2f, 0.22f, new Color(1f, 0.75f, 0.4f, 0.35f));
            fx.Smear(head, 1.2f, 0.18f, new Color(1f, 0.85f, 0.5f, 0.4f));
            int n = HammerFx.Steps(ref dashAcc, 90f, dt);
            if (n > 0)
            {
                fx.DustStream(feet, -dashDirection, n, 4f, 0.7f);
                fx.Sparks(feet + Vector3.up * 0.1f, -dashDirection + Vector3.up * 0.3f, n, 6f, 0.5f);
            }
        }

        if (rush)
        {
            fx.Smear(transform.position + Vector3.up * 1f, 1.8f, 0.3f, new Color(1f, 0.55f, 0.18f, 0.4f));
            fx.Smear(head, 2f, 0.25f, new Color(1f, 0.9f, 0.6f, 0.5f));
            int n = HammerFx.Steps(ref rushAcc, 120f, dt);
            if (n > 0)
            {
                fx.DustStream(feet, -rushDirection, n, 6f, 1f);
                fx.Sparks(feet + Vector3.up * 0.1f, -rushDirection + Vector3.up * 0.3f, n, 9f, 0.6f);
            }
            avatar.CameraRig.Shake(0.08f, 0.1f);
        }
        else if (wasRush)
        {
            // The charge ends with a skid and a thump; the ember aura takes over.
            GroundImpact(feet, 2.6f, 0.7f, 0);
            fx.DustStream(feet, rushDirection, 14, 7f, 1f);
        }
        wasRush = rush;

        if (buffed)
        {
            int n = HammerFx.Steps(ref auraAcc, 16f, dt);
            if (n > 0) fx.Sparks(head, Vector3.up, n, 1.6f, 1.2f, 0.05f, 0.6f);
            if (Time.frameCount % 4 == 0) fx.Smear(head, 1.1f + 0.2f * Mathf.Sin(Time.time * 8f), 0.35f, new Color(1f, 0.45f, 0.12f, 0.25f));
        }
        else if (wasBuffed) fx.Sparks(head, Vector3.up, 26, 3f, 1f, 0.05f, 0.5f);
        wasBuffed = buffed;

        if (ultimate)
        {
            fx.Smear(head, 2.4f, 0.25f, new Color(1f, 0.85f, 0.45f, 0.45f));
            int n = HammerFx.Steps(ref sparkAcc, 50f, dt);
            if (n > 0) fx.Sparks(head, (head - transform.position).normalized + Vector3.up * 0.3f, n, 9f, 0.5f, 0.09f, 0.5f);
            int d = HammerFx.Steps(ref dustAcc, 22f, dt);
            if (d > 0) fx.Dust(feet, d, 4f, UltimateRange * 0.7f, 1.2f, 0.9f);
            avatar.CameraRig.Shake(0.05f, 0.1f);
        }
        else if (wasUltimate)
        {
            // Shrinking back down hits the floor once more.
            GroundImpact(feet, UltimateRange * 0.8f, 1f, 0);
            fx.Sparks(feet + Vector3.up * 0.2f, Vector3.up, 40, 9f, 0.8f, 0.09f, 0.6f);
        }
        wasUltimate = ultimate;

        if (!grounded) fallSpeed = -avatar.VerticalVelocity;
        else if (!wasGrounded)
        {
            if (!landSkip && fallSpeed > LandMinSpeed)
            {
                float power = Mathf.Clamp(fallSpeed / 30f, 0.15f, 0.45f);
                fx.Impact(feet, 1.2f + power, power);
                avatar.CameraRig.Shake(0.2f * power, 0.15f);
                punch = Mathf.Max(punch, 0.08f);
            }
            landSkip = false;
        }
        wasGrounded = grounded;
    }

    // The faster it spins, the more it glows, shakes and throws: a ring at the future slam radius marks each rotation.
    void ChargeFx(float dt, Vector3 feet, Vector3 head)
    {
        int spins = Mathf.FloorToInt(spinDegrees / 360f);
        Color tint = Color.Lerp(HammerFx.Ember, new Color(1f, 0.3f, 0.2f), Mathf.Clamp01(spinTier * 1.4f));
        if (spins > lastSpinCount && lastSpinCount < MaxDamageSpins)
        {
            lastSpinCount = spins;
            int shown = Mathf.Min(spins, MaxDamageSpins);
            float range = AttackRange + shown * RangePerSpin;
            fx.Ring(feet, range, 0.35f, new Color(tint.r, tint.g, tint.b, 0.55f));
            fx.SparkRing(feet, 10 + shown * 4, 5f + shown);
            fx.Dust(feet, 4 + shown * 2, 3f, 1.2f, 0.6f, 0.7f);
            avatar.CameraRig.Shake(0.06f + 0.025f * shown, 0.2f);
            punch += 0.06f;
            HitFeedback.Instance?.PlaySwing(true);
            if (shown >= MaxDamageSpins)
            {
                // Fully charged.
                HitFeedback.Instance?.PlayUpgradeFlash(head);
                fx.Ring(feet, range * 0.6f, 0.3f, Color.white);
                fx.Crack(feet, range * 0.8f, 2f);
            }
        }
        fx.Smear(head, 0.9f + 0.7f * spinTier, 0.18f, new Color(tint.r, tint.g, tint.b, 0.35f + 0.25f * spinTier));
        int n = HammerFx.Steps(ref sparkAcc, 20f + 100f * spinTier, dt);
        if (n > 0) fx.Sparks(head, head - transform.position + Vector3.up * 0.3f, n, 4f + 6f * spinTier, 0.5f);
        int d = HammerFx.Steps(ref dustAcc, 6f + 24f * spinTier, dt);
        if (d > 0) fx.Dust(feet, d, 2.5f + 2f * spinTier, 0.8f + 0.8f * spinTier, 0.5f, 0.7f);
        avatar.CameraRig.Shake(0.015f + 0.05f * spinTier, 0.1f);
    }

    void UltimateTickFx()
    {
        Vector3 feet = Ground(transform.position);
        bool ending = ultimateUntil - Time.time < 2f;
        Color color = ending ? new Color(1f, 0.25f, 0.15f, 0.6f) : new Color(1f, 0.75f, 0.3f, 0.5f);
        fx.Ring(feet, UltimateRange, 0.35f, color);
        fx.SparkRing(feet, 14, 9f);
        punch = Mathf.Max(punch, 0.04f);
    }

    void RushHitFx(Enemy enemy)
    {
        Vector3 ground = Ground(enemy.transform.position);
        fx.Ring(ground, 1.6f, 0.3f, HammerFx.Ember);
        fx.Sparks(enemy.Center, rushDirection + Vector3.up * 0.3f, 20, 11f, 0.6f);
        fx.Hitstop(HitstopBase);
        avatar.CameraRig.Shake(0.3f, 0.2f);
        avatar.CameraRig.FovPunch(4f, 0.2f);
        punch = Mathf.Max(punch, 0.1f);
    }

    protected override void OnDash(Vector3 direction)
    {
        base.OnDash(direction);
        dashUntil = Time.time + DashPoseDuration;
        dashFxUntil = Time.time + DashFxDuration;
        dashDirection = direction;
        // The hammer strikes the floor and the swing carries you forward.
        Vector3 ground = Ground(transform.position);
        fx.Ring(ground, 1.7f, 0.28f, HammerFx.Ember);
        fx.Crack(ground, 1.1f, 1f);
        fx.DustStream(ground, -direction, 14, 6f, 0.9f);
        fx.SparkRing(ground, 14, 6f);
        avatar.CameraRig.Shake(0.15f, 0.15f);
        avatar.CameraRig.FovPunch(-3f, 0.25f);
        punch = Mathf.Max(punch, 0.1f);
        HitFeedback.Instance?.PlaySwing(true);
    }

    protected override void OnSkill()
    {
        buffUntil = Time.time + SkillDuration;
        Enemy target = FindAssistTarget(RushRange, 180f);
        rushDirection = target != null ? target.transform.position - transform.position : CameraForward;
        rushDirection.y = 0f;
        rushDirection.Normalize();
        rushHits.Clear();
        rushUntil = Time.time + RushDuration;
        avatar.Lunge(rushDirection, target != null ? Mathf.Min(RushDistance, Vector3.Distance(target.transform.position, transform.position)) : RushDistance, RushDuration);

        // Ignition: a blast behind the hammer head, then the speed lines of the rush take over.
        Vector3 ground = Ground(transform.position);
        fx.Ring(ground, 2.4f, 0.35f, HammerFx.Ember);
        fx.Ring(ground, 1.2f, 0.25f, Color.white);
        fx.DustStream(ground, -rushDirection, 20, 8f, 1.1f);
        fx.Sparks(ground + Vector3.up * 0.3f, -rushDirection + Vector3.up * 0.2f, 30, 12f, 0.5f, 0.08f, 0.5f);
        avatar.CameraRig.Shake(0.3f, 0.25f);
        avatar.CameraRig.FovPunch(-6f, 0.35f);
        punch = Mathf.Max(punch, 0.15f);
        HitFeedback.Instance?.PlaySwing(true);
    }

    protected override void OnSpecial()
    {
        specialUntil = Time.time + SpecialPoseDuration;
        int hits = Pulse(transform.position, SpecialRange, 180f, 1f, true, false);
        // Entrance slam: the biggest ground hit short of a full charge.
        Vector3 ground = Ground(transform.position);
        GroundImpact(ground, SpecialRange, 1f, hits);
        fx.Ring(ground, SpecialRange * 0.5f, 0.25f, Color.white);
        fx.Sparks(ground + Vector3.up * 0.2f, Vector3.up, 30, 10f, 0.5f, 0.09f, 0.6f);
        fx.DustStream(ground, Vector3.up, 12, 5f, 1f);
    }

    protected override void OnUltimate()
    {
        ultimateUntil = Time.time + UltimateDuration;
        nextUltimateHit = Time.time;
        ultimateStart = Time.time;
        // Growing hammer pounds the floor as it swells.
        Vector3 ground = Ground(transform.position);
        GroundImpact(ground, UltimateRange, 1.3f, 0);
        fx.Ring(ground, UltimateRange * 1.3f, 0.6f, new Color(1f, 0.85f, 0.4f, 0.6f));
        fx.Crack(ground, UltimateRange * 0.9f, 3f);
        fx.Sparks(ground + Vector3.up * 0.3f, Vector3.up, 50, 12f, 0.9f, 0.1f, 0.7f);
        avatar.CameraRig.FovPunch(-8f, 0.5f);
        HitFeedback.Instance?.PlayUpgradeFlash(transform.position + Vector3.up);
    }

    int Pulse(Vector3 centre, float range, float angle, float damageScale, bool launch, bool crush, bool tick = false, Enemy excluded = null)
    {
        int hits = 0;
        lastKills = 0;
        Vector3 forward = Facing;
        foreach (var enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (enemy == excluded || enemy.IsDead) continue;
            Vector3 to = enemy.transform.position - centre;
            Vector3 planar = new Vector3(to.x, 0f, to.z);
            if (planar.magnitude > range + enemy.Radius || Mathf.Abs(to.y) > range || Vector3.Angle(forward, planar) > angle) continue;
            Vector3 direction = planar.sqrMagnitude > 0.01f ? planar.normalized : forward;
            if (launch) direction = (direction * LaunchForward + Vector3.up * LaunchUp).normalized;
            else if (crush) direction = (direction + Vector3.down).normalized;
            bool killed = DealHit(enemy, Mathf.Max(1, Mathf.RoundToInt(weapon.damage * damageScale)), direction, launch ? LaunchKnockback : weapon.knockback, tick);
            hits++;
            if (killed) lastKills++;
            if (launch) LaunchFx(enemy);
            if (crush && killed)
            {
                Vector3 impact = enemy.transform.position;
                HammerCrushEffect.Begin(enemy, fx, () =>
                {
                    if (this != null) Pulse(impact, SplashRange, 180f, SplashDamage, false, false, false, enemy);
                });
            }
        }
        return hits;
    }

    // Launched enemies leave a ring on the floor and a streak going up.
    void LaunchFx(Enemy enemy)
    {
        fx.Ring(Ground(enemy.transform.position), 1.2f, 0.3f, HammerFx.Hot);
        fx.Sparks(enemy.Center, Vector3.up, 16, 9f, 0.4f);
        fx.Smear(enemy.Center + Vector3.up * 0.5f, 1.3f, 0.25f, new Color(1f, 0.95f, 0.8f, 0.5f));
    }

    bool DealHit(Enemy enemy, int damage, Vector3 direction, float knockback, bool tick)
    {
        var info = new HitInfo { damage = damage, point = enemy.Center, direction = direction,
            knockback = knockback, stagger = tick ? UltimateTickInterval : HitStagger, heavy = !tick, tick = tick };
        enemy.TakeHit(info);
        avatar.RecordHit(damage, enemy.IsDead);
        if (tick) HitFeedback.Instance?.PlayTick(info);
        else HitFeedback.Instance?.PlayHit(info, weapon, currentSwing ?? weapon.combo[0], enemy.IsDead);
        return enemy.IsDead;
    }
}

// A detached visual survives weapon swaps and restores renderer visibility on respawn.
// Rhythm: slam flat, tremble and swell, then pop.
public class HammerCrushEffect : MonoBehaviour
{
    const float SquashPhase = 0.55f;
    const float SwellHeight = 0.3f;
    const float SwellWidth = 0.35f;

    Enemy enemy;
    HammerFx fx;
    Renderer[] originals;
    MeshRenderer[] copies;
    bool[] visibility;
    Transform snapshot;
    float elapsed;
    bool exploded;
    System.Action burst;
    MaterialPropertyBlock flash;

    public static void Begin(Enemy target, HammerFx effects, System.Action onBurst)
    {
        var effect = new GameObject("HammerCrush").AddComponent<HammerCrushEffect>();
        effect.enemy = target;
        effect.fx = effects;
        effect.burst = onBurst;
        effect.transform.position = target.transform.position;
        effect.snapshot = new GameObject("CrushedBody").transform;
        effect.snapshot.SetParent(effect.transform, false);
        effect.originals = target.GetComponentsInChildren<Renderer>();
        effect.visibility = new bool[effect.originals.Length];
        var meshes = new System.Collections.Generic.List<MeshRenderer>();
        for (int i = 0; i < effect.originals.Length; i++)
        {
            var renderer = effect.originals[i];
            effect.visibility[i] = renderer.forceRenderingOff;
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null) continue;
            var copy = new GameObject(renderer.name);
            copy.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
            copy.transform.localScale = renderer.transform.lossyScale;
            copy.transform.SetParent(effect.snapshot, true);
            copy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var meshRenderer = copy.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = renderer.sharedMaterials;
            meshes.Add(meshRenderer);
            renderer.forceRenderingOff = true;
        }
        effect.copies = meshes.ToArray();
        effect.flash = new MaterialPropertyBlock();

        // Impact: a flat ring of dust squeezes out from under the hammer.
        if (effects != null)
        {
            Vector3 ground = target.transform.position + Vector3.up * 0.03f;
            effects.Ring(ground, 1.3f, 0.25f, new Color(1f, 1f, 1f, 0.7f));
            effects.Dust(ground, 10, 3.5f, 0.5f, 0.6f, 0.6f);
        }
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        if (!exploded)
        {
            float u = Mathf.Clamp01(elapsed / HammerMoveset.CrushDuration);
            float width, height;
            Vector3 jitter = Vector3.zero;
            if (u < SquashPhase)
            {
                float s = u / SquashPhase;
                float ease = 1f - (1f - s) * (1f - s) * (1f - s);
                width = Mathf.Lerp(1f, HammerMoveset.CrushWidth, ease);
                height = Mathf.Lerp(1f, HammerMoveset.CrushFlatness, ease);
            }
            else
            {
                float p = (u - SquashPhase) / (1f - SquashPhase);
                width = HammerMoveset.CrushWidth * (1f + SwellWidth * p * p);
                height = HammerMoveset.CrushFlatness + SwellHeight * p * p;
                jitter = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * (0.05f * p);
                SetFlash(p > 0.5f && ((int)(elapsed * 60f) & 1) == 0);
            }
            snapshot.localScale = new Vector3(width, height, width);
            snapshot.localPosition = jitter;
            if (u >= 1f)
            {
                exploded = true;
                Vector3 ground = transform.position + Vector3.up * 0.03f;
                HitFeedback.Instance?.PlaySonicExplosion(transform.position + Vector3.up * HammerMoveset.CrushBurstHeight, Vector3.up);
                if (fx != null)
                {
                    fx.Impact(ground, 2.4f, 0.8f);
                    fx.Sparks(ground + Vector3.up * 0.3f, Vector3.up, 30, 11f, 0.7f, 0.09f, 0.6f);
                    fx.Hitstop(0.06f);
                }
                snapshot.gameObject.SetActive(false);
                burst?.Invoke();
                burst = null;
            }
        }
        if (enemy == null || !enemy.IsDead) Destroy(gameObject);
    }

    void SetFlash(bool on)
    {
        if (on)
        {
            flash.SetColor("_BaseColor", new Color(1f, 0.85f, 0.6f));
            flash.SetColor("_Color", new Color(1f, 0.85f, 0.6f));
        }
        foreach (var renderer in copies) if (renderer != null) renderer.SetPropertyBlock(on ? flash : null);
    }

    void OnDestroy()
    {
        if (originals == null) return;
        for (int i = 0; i < originals.Length; i++) if (originals[i] != null) originals[i].forceRenderingOff = visibility[i];
    }
}

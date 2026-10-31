using System.Collections.Generic;
using UnityEngine;

// The shears walk on their blades, slash in a combo and snap three times with E.
[DefaultExecutionOrder(-100)]
public class ShearsMoveset : AvatarMoveset
{
    const float MoveSpeed = 5.6f;
    const float Acceleration = 62f;
    const float SprintBoost = 1.7f;
    const float TurnBoost = 1.6f;
    const float JumpHeight = 2f;
    const float CrouchTime = 0.08f;
    const float CrouchDip = 0.1f;
    const float StrideAngle = 30f;
    const float AirJaw = 55f;
    const float JawSpeed = 900f;
    const float AttackBuffer = 0.24f;
    const float ComboReset = 0.65f;
    const float SkillCooldownSeconds = 7f;
    const float SkillRadius = 3.6f;
    const float AirRadius = 3.2f;
    const float SpecialRadius = 4f;
    const float SpecialHalfAngle = 100f;
    const float RollDuration = 0.32f;
    const float RollDistance = 3.8f;
    const float RollDegrees = 360f;
    const float UltimateDuration = 60f;
    const float UltimateSize = 1.6f;
    const float UltimateDamage = 1.8f;
    const float AirSpinDegrees = 360f;
    const float AirBladePitch = 0f;
    const float AirWindup = 0.08f;
    const float AirStrike = 0.32f;
    const float AirRecovery = 0.16f;
    const float SpecialWindup = 0.08f;
    const float SpecialStrike = 0.14f;
    const float SpecialRecovery = 0.2f;
    const float SpecialBladePitch = 90f;
    const float SpecialSweepYaw = 100f;
    const float AssistStandOff = 1.2f;
    const float AssistAdvance = 1.2f;
    // Presentation only.
    const float SnipFlashSize = 1.1f;
    static readonly float[] StepPunch = { 1.8f, 2.6f, 4f };
    static readonly float[] StepShake = { 0.08f, 0.14f, 0.3f };
    const float AirWindupSpin = 35f;
    const float AirLean = 14f;
    const float AirJawPeak = 85f;
    const float RollTuck = 0.15f;
    const float LandingSpeed = 5f;
    const float PopTime = 0.4f;
    const float FlareTime = 0.5f;
    const float FlareJaw = 85f;
    const float PulseInterval = 1.6f;
    const float WarningTime = 5f;
    static readonly Color UltimateColor = new Color(1f, 0.82f, 0.3f);
    static readonly Color WarningColor = new Color(1f, 0.4f, 0.15f);
    static readonly Color DustColor = new Color(0.8f, 0.78f, 0.72f);

    readonly HashSet<Enemy> areaHits = new HashSet<Enemy>();
    WeaponData runtimeWeapon;
    SwingData airSwing;
    int comboStep, skillStep;
    bool skillSequence, airAttack, specialAttack;
    float bufferedAt = -10f, comboEndedAt = -10f;
    float rollLeft, rollFacing, ultimateUntil, hitPauseLeft;
    int baseDamage;
    float baseScale, baseRadius;
    Vector3 visualScale;
    Vector3 visualPosition;
    float presentationScale = 1f;
    protected override float SkillCooldown => SkillCooldownSeconds;
    public override string SkillName => "연속 절단";
    public override float UltimateTimeRemaining => Mathf.Max(0f, ultimateUntil - Time.time);
    public override bool Busy => base.Busy || skillSequence || rollLeft > 0f;
    public override string Status => ultimateUntil > Time.time ? $"Giant shears {Mathf.CeilToInt(ultimateUntil - Time.time)}s" : "";
    ShearsDriver shears;
    float legLength;
    // Each step travels one blade length; two steps complete a cycle.
    float stridePhase;
    float jawFrom;
    float crouchTimer;
    bool jumpQueued;
    ShearsFx fx;
    bool wasGrounded = true;
    float peakFall, rollDustAt, flareLeft, nextPulseAt;
    float scaleFrom = 1f, scaleTo = 1f, scaleStart, scaleTime;
    bool scaling;

    public override void Configure(WeaponAvatar owner, WeaponData data, Transform weaponVisual)
    {
        legLength = Mathf.Max(0.3f, (data.bladeTip - data.bladeBase).magnitude * data.heldScale);
        SprintMultiplier = SprintBoost;
        shears = weaponVisual.gameObject.AddComponent<ShearsDriver>();
        shears.Configure(data.jawClosed, data.restJaw);
        fx = gameObject.AddComponent<ShearsFx>();
        fx.ConfigureSweep(shears.HalfA, data.bladeBase, shears.HalfA, data.bladeTip);
        // Keep the asset and the other equipped weapons untouched by temporary buffs.
        runtimeWeapon = Instantiate(data);
        baseDamage = data.damage;
        baseScale = data.heldScale;
        baseRadius = data.hitRadius;
        visualScale = weaponVisual.localScale;
        visualPosition = weaponVisual.localPosition;
        base.Configure(owner, runtimeWeapon, weaponVisual);
        airSwing = new SwingData
        {
            windup = AirWindup, strike = AirStrike, recovery = AirRecovery,
            windupPosition = data.restPosition, strikePosition = data.restPosition,
            windupEuler = data.restEuler, strikeEuler = data.restEuler,
            damageScale = 1.3f, knockbackScale = 0.65f, stagger = 0.4f,
            jawWindup = AirJaw, jawStrike = AirJaw, lunge = AssistAdvance,
            strikeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f),
        };
    }

    public override void Teardown()
    {
        CancelAttack();
        scaling = false;
        RestoreUltimate();
        base.Teardown();
        if (fx != null) Destroy(fx);
        Destroy(runtimeWeapon);
    }

    // Each blade rides on its own half; the cut plane is the plane the blades lie in.
    protected override void BuildHitbox()
    {
        hitbox = visual.gameObject.AddComponent<WeaponHitbox>();
        hitbox.Hit += OnBladeHit;
        ConfigureBlades();
        trail = visual.gameObject.AddComponent<BladeTrail>();
        trail.Configure(shears.HalfA, weapon.bladeTip, shears.HalfB, MirrorX(weapon.bladeTip), weapon.trailColor);
    }

    void ConfigureBlades()
    {
        weapon.hitRadius = baseRadius * AreaScale;
        hitbox.Configure(weapon.bladeBase, weapon.bladeTip, weapon.hitRadius, weapon.tickInterval);
        hitbox.ClearBlades();
        // TransformPoint scales both blade endpoints with the visual; do not scale them twice.
        hitbox.AddBlade(shears.HalfA, weapon.bladeBase, weapon.bladeTip);
        if (weapon.mirrorBlade) hitbox.AddBlade(shears.HalfB, MirrorX(weapon.bladeBase), MirrorX(weapon.bladeTip));
        hitbox.CutPlane = visual;
    }

    static Vector3 MirrorX(Vector3 v) => new Vector3(-v.x, v.y, v.z);

    protected override void TickAttack(AvatarInput input, float dt)
    {
        if (skillSequence || rollLeft > 0f) return;
        if (input.attack) bufferedAt = Time.unscaledTime;
        if (Time.unscaledTime - bufferedAt > AttackBuffer) return;
        bool canChain = swinging && !airAttack && swinger.Phase == SwingPhase.Recovery
            && swinger.PhaseProgress >= currentSwing.chainAt;
        if (swinging && !canChain) return;
        bufferedAt = -10f;
        if (!avatar.IsGrounded)
        {
            PlaySwing(airSwing, false, true);
            return;
        }
        if (weapon.combo.Count == 0) return;
        if (Time.time - comboEndedAt > ComboReset && !swinging) comboStep = 0;
        PlaySwing(weapon.combo[comboStep % weapon.combo.Count], false, false);
        comboStep++;
    }

    void PlaySwing(SwingData swing, bool alt, bool air)
    {
        jumpQueued = false;
        crouchTimer = 0f;
        hitbox.SetActive(false);
        trail.Emitting = false;
        areaHits.Clear();
        jawFrom = CurrentJaw();
        fx.EndSweep();
        currentSwing = swing;
        currentAlt = alt;
        airAttack = air;
        specialAttack = false;
        hitPauseLeft = 0f;
        swinger.Holding = false;
        swinging = true;
        assistTarget = FindAssistTarget();
        swingFacing = assistTarget != null ? YawTo(assistTarget.transform.position) : avatar.CameraRig.Yaw;
        swinger.Play(swing);
        HitFeedback.Instance.PlaySwing(swing.heavy);
        if (alt) TelegraphArea(SkillRadius, 180f, swing.windup);
    }

    // The halves are driven directly while walking, so read the pose the player actually sees.
    float CurrentJaw() => shears.HalfB != null ? Mathf.DeltaAngle(0f, shears.HalfB.localEulerAngles.z) : shears.Jaw;

    Color SweepColor => ultimateUntil > 0f ? UltimateColor : weapon.trailColor;
    float AreaScale => weapon.heldScale / baseScale;
    Vector3 Hinge => swinger.transform.position;
    Vector3 Feet => transform.position + Vector3.up * 0.05f;

    // A faint fan grows during the windup so the E range is readable before the cut lands.
    void TelegraphArea(float radius, float halfAngle, float time)
    {
        fx.Arc(AreaHeight(), swingFacing, radius * AreaScale, halfAngle, new Color(1f, 1f, 1f, 0.35f), time, 0.35f);
    }

    // E steps climb: low sweep, mid sweep, high finishing sweep.
    Vector3 AreaHeight()
    {
        if (!currentAlt) return Hinge;
        return Vector3.Lerp(Feet + Vector3.up * 0.4f, Hinge, Mathf.Clamp01((skillStep - 1) / 2f));
    }

    protected override void OnStrikeBegan()
    {
        if (specialAttack)
        {
            ResolveArea(SpecialRadius, SpecialHalfAngle);
            PlayStrikeFx();
            return;
        }
        base.OnStrikeBegan();
        if (currentAlt || airAttack)
        {
            hitbox.SetActive(false);
            if (currentAlt) ResolveArea(SkillRadius, 180f);
        }
        PlayStrikeFx();
    }

    void PlayStrikeFx()
    {
        Color glint = Color.Lerp(SweepColor, Color.white, 0.6f);
        Vector3 forward = Quaternion.Euler(0f, swingFacing, 0f) * Vector3.forward;
        var cam = avatar.CameraRig;
        if (specialAttack)
        {
            fx.BeginSweep(glint, 0.4f);
            fx.Arc(Hinge, swingFacing, SpecialRadius * AreaScale, SpecialHalfAngle, new Color(1f, 0.95f, 0.7f, 0.9f), 0.32f, 0.45f);
            fx.Ring(Feet, SpecialRadius * AreaScale, new Color(1f, 0.9f, 0.5f, 0.6f), 0.4f, 0.2f);
            fx.Puff(Feet, Vector3.up, DustColor, 14, 3.5f, 0.35f, 0.5f, 80f);
            cam.Shake(0.22f, 0.2f);
            cam.FovPunch(3.5f, 0.22f);
            return;
        }
        if (airAttack)
        {
            fx.BeginSweep(glint, 0.45f);
            fx.Ring(Hinge, AirRadius * AreaScale, new Color(glint.r, glint.g, glint.b, 0.75f), 0.3f, 0.35f);
            cam.FovPunch(2.5f, 0.2f);
            return;
        }
        if (currentAlt)
        {
            float power = skillStep / 3f;
            HitFeedback.Instance.PlaySnip(Hinge + forward * SkillRadius * AreaScale * 0.4f);
            fx.Arc(AreaHeight(), swingFacing, SkillRadius * AreaScale, 90f, new Color(1f, 1f, 1f, 0.55f + 0.35f * power), 0.22f + 0.1f * power, 0.6f);
            if (skillStep == 3)
            {
                fx.Ring(Feet, SkillRadius * AreaScale, new Color(1f, 0.9f, 0.55f, 0.7f), 0.4f, 0.25f);
                fx.Puff(Feet, forward + Vector3.up * 0.5f, DustColor, 12, 4f, 0.3f, 0.45f, 60f);
                cam.Shake(0.3f, 0.2f);
                cam.FovPunch(3f, 0.2f);
            }
            return;
        }
        // Combo cuts: the fan lingers longer and brighter on the finisher.
        // Each cut hits a little harder than the last; the finisher lands with the most weight.
        int step = Mathf.Clamp(comboStep - 1, 0, StepPunch.Length - 1);
        fx.BeginSweep(currentSwing.heavy ? glint : SweepColor, currentSwing.heavy ? 0.5f : 0.3f + 0.04f * step);
        cam.Shake(StepShake[step], 0.15f);
        cam.FovPunch(StepPunch[step], 0.18f);
        if (currentSwing.heavy)
        {
            fx.Puff(Feet + forward * 0.8f, Vector3.up, DustColor, 14, 3.5f, 0.3f, 0.45f, 70f);
            fx.Arc(Hinge, swingFacing, 2.6f * AreaScale, 80f, new Color(1f, 0.95f, 0.75f, 0.6f), 0.28f, 0.5f);
        }
    }

    protected override void OnStrikeEnded()
    {
        if (airAttack) ResolveArea(AirRadius, 180f);
        if (currentAlt) ResolveArea(SkillRadius, 180f);
        base.OnStrikeEnded();
        fx.EndSweep();
        if (!currentAlt && !airAttack && !specialAttack) SnipFlash();
    }

    // The blades meet at the end of a cut: a bright clack at the tip.
    void SnipFlash()
    {
        Vector3 tip = shears.HalfA.TransformPoint(weapon.bladeTip);
        HitFeedback.Instance.PlaySnip(tip);
        fx.Ring(tip, SnipFlashSize * (currentSwing.heavy ? 1.5f : 1f), new Color(1f, 1f, 1f, 0.8f), 0.18f, 0.2f);
    }

    protected override void OnBladeHit(HitContact contact)
    {
        if (!currentAlt && !airAttack && !specialAttack && areaHits.Add(contact.enemy)) HandleHit(contact);
    }

    // Damage and stagger matter on living enemies; killing blows still slice.
    protected override bool SlicesNow(HitContact contact) => false;

    protected override void OnEnemyHit(Enemy enemy, bool tick, Vector3 direction)
    {
        if (tick) return;
        if (!currentAlt && !airAttack && !specialAttack)
        {
            // Cut flash on the victim; kept small so multi-hits do not pile up.
            fx.Ring(enemy.Center, 0.9f, new Color(1f, 0.95f, 0.8f, 0.7f), 0.16f, 0.25f);
            fx.Puff(enemy.Center, direction + Vector3.up * 0.3f, Color.white, 5, 6f, 0.07f, 0.2f, 45f, 0.6f);
        }
        if (currentSwing.heavy)
        {
            Vector3 ground = enemy.transform.position + Vector3.up * 0.05f;
            fx.Ring(ground, 2.2f * AreaScale, new Color(1f, 0.85f, 0.45f, 0.7f), 0.35f, 0.2f);
            fx.Puff(ground, Vector3.up, DustColor, 10, 3.5f, 0.3f, 0.45f, 70f);
            avatar.CameraRig.Shake(0.4f, 0.25f);
            avatar.CameraRig.FovPunch(2f, 0.2f);
        }
        if (swinger.Phase != SwingPhase.Strike) return;
        hitPauseLeft = Mathf.Max(hitPauseLeft, weapon.hitStop * currentSwing.hitStopScale);
        swinger.Holding = hitPauseLeft > 0f;
    }

    void ResolveArea(float radius, float halfAngle)
    {
        Vector3 forward = Quaternion.Euler(0f, swingFacing, 0f) * Vector3.forward;
        float scale = AreaScale;
        foreach (var enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (enemy.IsDead || areaHits.Contains(enemy)) continue;
            Vector3 to = enemy.Center - (transform.position + Vector3.up);
            if (Mathf.Abs(to.y) > radius * scale) continue;
            to.y = 0f;
            if (to.magnitude > radius * scale + enemy.Radius || Vector3.Angle(forward, to) > halfAngle) continue;
            areaHits.Add(enemy);
            HandleHit(new HitContact
            {
                enemy = enemy,
                point = enemy.Center,
                cutNormal = Vector3.up,
                firstContact = true,
                bladeSpeed = float.MaxValue,
            });
        }
    }

    protected override void OnSwingFinished()
    {
        base.OnSwingFinished();
        comboEndedAt = Time.time;
        airAttack = specialAttack = false;
        fx.EndSweep();
    }

    protected override void OnSkill()
    {
        if (weapon.altCombo.Count == 0) return;
        comboStep = 0;
        bufferedAt = -10f;
        skillSequence = true;
        skillStep = 1;
        PlaySwing(weapon.altCombo[0], true, false);
    }

    protected override void OnSpecial()
    {
        var special = new SwingData
        {
            windup = SpecialWindup, strike = SpecialStrike, recovery = SpecialRecovery,
            damageScale = 1.6f, knockbackScale = 1.1f, stagger = 0.65f, heavy = true,
            hitStopScale = 1.3f,
            windupPosition = weapon.restPosition, strikePosition = weapon.restPosition,
            windupEuler = new Vector3(SpecialBladePitch, SpecialSweepYaw, 0f),
            strikeEuler = new Vector3(SpecialBladePitch, -SpecialSweepYaw, 0f),
            jawWindup = FlareJaw, jawStrike = 40f,
        };
        CancelAttack();
        PlaySwing(special, false, false);
        specialAttack = true;
        TelegraphArea(SpecialRadius, SpecialHalfAngle, special.windup);
        // Pop in: stretched tall, then settles.
        avatar.Motion.localScale = new Vector3(0.88f, 1.15f, 0.88f);
        Vector3 forward = Quaternion.Euler(0f, swingFacing, 0f) * Vector3.forward;
        FaceYaw(swingFacing, float.MaxValue, 1f);
        if (assistTarget != null)
            avatar.Lunge(forward, Mathf.Clamp(Vector3.Distance(transform.position, assistTarget.transform.position) - AssistStandOff, 0f, AssistAdvance), special.windup);
    }

    protected override void OnDash(Vector3 dir)
    {
        rollLeft = RollDuration;
        rollFacing = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        avatar.PlanarVelocity = Vector3.zero;
        avatar.GrantInvulnerability(RollDuration);
        avatar.Lunge(dir, RollDistance, RollDuration);
        rollDustAt = 0f;
        fx.BeginSweep(new Color(0.75f, 0.95f, 1f, 0.9f), 0.35f);
        fx.Puff(Feet, -dir + Vector3.up * 0.3f, DustColor, 10, 3f, 0.3f, 0.4f, 50f);
        avatar.CameraRig.FovPunch(2.5f, 0.2f);
    }

    protected override void CancelAttack()
    {
        base.CancelAttack();
        skillSequence = airAttack = specialAttack = jumpQueued = false;
        skillStep = comboStep = 0;
        rollLeft = 0f;
        hitPauseLeft = 0f;
        swinger.Holding = false;
        bufferedAt = -10f;
        if (fx != null) fx.EndSweep();
        areaHits.Clear();
    }

    protected override void OnUltimate()
    {
        ultimateUntil = Time.time + UltimateDuration;
        weapon.damage = Mathf.RoundToInt(baseDamage * UltimateDamage);
        weapon.heldScale = baseScale * UltimateSize;
        ConfigureBlades();
        StartScale(1f, UltimateSize);
        ApplyTrailColor(UltimateColor);
        flareLeft = FlareTime;
        nextPulseAt = Time.time + PulseInterval;

        // Transformation burst: shock rings, gold sparks and a camera snap.
        HitFeedback.Instance.PlayUpgrade();
        HitFeedback.Instance.PlayUpgradeFlash(Hinge);
        fx.Ring(Feet, 6f, new Color(1f, 0.85f, 0.35f, 0.9f), 0.5f, 0.15f);
        fx.Ring(Hinge, 4f, new Color(1f, 1f, 0.8f, 0.7f), 0.35f, 0.1f);
        fx.Puff(Feet, Vector3.up, DustColor, 24, 5f, 0.4f, 0.7f, 90f);
        avatar.CameraRig.Shake(0.5f, 0.4f);
    }

    void ApplyTrailColor(Color color)
    {
        trail.Configure(shears.HalfA, weapon.bladeTip, shears.HalfB, MirrorX(weapon.bladeTip), color);
    }

    void StartScale(float from, float to)
    {
        scaleFrom = from;
        scaleTo = to;
        scaleStart = Time.time;
        scaleTime = PopTime;
        scaling = true;
    }

    // Overshoots the target size a little, then settles.
    void TickScale()
    {
        if (!scaling || visual == null) return;
        float u = Mathf.Clamp01((Time.time - scaleStart) / scaleTime);
        float back = 1f + 2.70158f * Mathf.Pow(u - 1f, 3f) + 1.70158f * Mathf.Pow(u - 1f, 2f);
        presentationScale = Mathf.LerpUnclamped(scaleFrom, scaleTo, back);
        visual.localScale = visualScale * presentationScale;
        if (u >= 1f) scaling = false;
    }

    // Apply after the attack pose, before blade sweeps and the camera.
    void LateUpdate()
    {
        if (visual == null) return;
        // Lift only the downward blade extent, so horizontal cuts stay at enemy height.
        float down = Mathf.Max(0f, -shears.HalfA.TransformVector(weapon.bladeTip).y,
            -shears.HalfB.TransformVector(MirrorX(weapon.bladeTip)).y);
        Vector3 lift = Vector3.up * down * (1f - 1f / presentationScale);
        visual.localPosition = visualPosition + visual.parent.InverseTransformVector(lift);
        avatar.CameraRig.SetFramingScale(Mathf.Max(AreaScale, presentationScale), visual);
    }

    void RestoreUltimate(bool animate = false)
    {
        ultimateUntil = 0f;
        flareLeft = 0f;
        if (trail != null) ApplyTrailColor(weapon.trailColor);
        weapon.damage = baseDamage;
        weapon.heldScale = baseScale;
        weapon.hitRadius = baseRadius;
        if (visual != null && animate) StartScale(UltimateSize, 1f);
        else if (visual != null)
        {
            presentationScale = 1f;
            visual.localScale = visualScale;
            visual.localPosition = visualPosition;
        }
        if (!animate) avatar.CameraRig.SetFramingScale(1f);
        if (hitbox != null)
        {
            hitbox.SetActive(false);
            ConfigureBlades();
            // Seed the restored blades at their current pose, never at the world origin.
            hitbox.SetActive(swinging && swinger.Phase == SwingPhase.Strike && !currentAlt && !airAttack && !specialAttack);
        }
    }

    protected override void Locomote(AvatarInput input, float dt)
    {
        hitPauseLeft = Mathf.Max(0f, hitPauseLeft - dt);
        swinger.Holding = hitPauseLeft > 0f;
        TickScale();
        TickUltimateFx();
        TickLanding();
        if (rollLeft > 0f)
        {
            rollLeft = Mathf.Max(0f, rollLeft - dt);
            FaceYaw(rollFacing, float.MaxValue, dt);
            avatar.PlanarVelocity = Vector3.zero;
            // Slow wind-in, fast middle, soft finish; the blades fold shut while tumbling.
            float u = 1f - rollLeft / RollDuration;
            float tuck = Mathf.Sin(u * Mathf.PI);
            avatar.Motion.localRotation = Quaternion.Euler(RollDegrees * Mathf.SmoothStep(0f, 1f, u), 0f, 0f);
            avatar.Motion.localScale = new Vector3(1f + RollTuck * 0.4f * tuck, 1f - RollTuck * tuck, 1f + RollTuck * 0.4f * tuck);
            shears.Jaw = Mathf.MoveTowards(shears.Jaw, weapon.jawClosed, JawSpeed * dt);
            rollDustAt -= dt;
            if (rollDustAt <= 0f)
            {
                rollDustAt = 0.07f;
                fx.Puff(Feet, Vector3.up, DustColor, 3, 1.5f, 0.25f, 0.35f, 60f);
            }
            if (rollLeft <= 0f) RollLanded();
            return;
        }
        if (skillSequence && !swinging)
        {
            if (skillStep < Mathf.Min(3, weapon.altCombo.Count)) PlaySwing(weapon.altCombo[skillStep++], true, false);
            else skillSequence = false;
        }
        if (airAttack && swinging)
        {
            FaceYaw(swingFacing, float.MaxValue, dt);
            // Coil back in the windup, whip through the circle, then glide to a stop.
            float spin = swinger.Phase == SwingPhase.Strike ? swinger.PhaseProgress : swinger.Phase == SwingPhase.Recovery ? 1f : 0f;
            float angle = swinger.Phase == SwingPhase.Windup
                ? -AirWindupSpin * swinger.PhaseProgress
                : Mathf.Lerp(-AirWindupSpin, AirSpinDegrees, Mathf.SmoothStep(0f, 1f, spin));
            float bank = Mathf.Sin(spin * Mathf.PI);
            shears.Jaw = Mathf.Lerp(AirJaw, AirJawPeak, bank);
            avatar.Motion.localRotation = Quaternion.Euler(0f, angle, -AirLean * bank) * Quaternion.Euler(AirBladePitch, 0f, 0f);
            float squash = swinger.Phase == SwingPhase.Windup ? swinger.PhaseProgress : 0f;
            float stretch = swinger.Phase == SwingPhase.Strike ? 1f - swinger.PhaseProgress : 0f;
            avatar.Motion.localScale = new Vector3(1f + 0.1f * squash - 0.06f * stretch, 1f - 0.12f * squash + 0.1f * stretch, 1f + 0.1f * squash - 0.06f * stretch);
            if (swinger.Phase == SwingPhase.Strike) ResolveArea(AirRadius, 180f);
            Accelerate(input.wish * MoveSpeed * MoveSpeedScale * weapon.swingMoveScale, Acceleration, dt);
            return;
        }
        UpdateFacing(input.wish, dt, TurnBoost);

        if (swinging)
        {
            UpdateJaw();
            Accelerate(input.wish * (MoveSpeed * MoveSpeedScale * weapon.swingMoveScale), Acceleration, dt);
            RelaxMotion(dt);
            return;
        }

        Accelerate(input.wish * (MoveSpeed * MoveSpeedScale), Acceleration, dt);
        if (input.jump && avatar.IsGrounded && !jumpQueued)
        {
            jumpQueued = true;
            crouchTimer = CrouchTime;
        }
        if (jumpQueued)
        {
            UpdateJump(dt);
            return;
        }

        float speed = avatar.PlanarVelocity.magnitude;
        bool striding = avatar.IsGrounded && speed > 0.3f;
        if (striding) stridePhase = Mathf.Repeat(stridePhase + speed * dt / legLength * Mathf.PI, Mathf.PI * 2f);

        if (avatar.IsGrounded)
        {
            float angle = striding ? weapon.jawClosed + StrideAngle * Mathf.Cos(stridePhase) : weapon.restJaw;
            if (flareLeft > 0f)
            {
                // Giant shears yawn wide, then bite shut.
                bool bite = flareLeft <= FlareTime * 0.4f;
                if (bite && flareLeft + dt > FlareTime * 0.4f) HitFeedback.Instance.PlaySnip(Hinge);
                angle = bite ? weapon.jawClosed : FlareJaw;
                flareLeft = Mathf.Max(0f, flareLeft - dt);
            }
            float k = 1f - Mathf.Exp(-30f * dt);
            // Bypass the jaw clamp so the legs can cross without registering a snip.
            if (shears.HalfA != null)
                shears.HalfA.localRotation = Quaternion.Slerp(shears.HalfA.localRotation, Quaternion.Euler(0f, 0f, -angle), k);
            if (shears.HalfB != null)
                shears.HalfB.localRotation = Quaternion.Slerp(shears.HalfB.localRotation, Quaternion.Euler(0f, 0f, angle), k);
        }
        else shears.Jaw = Mathf.MoveTowards(shears.Jaw, AirJaw, JawSpeed * dt);

        RelaxMotion(dt);
    }

    // Gold pulses while the giant form lasts, red and faster in the last seconds; the form ends with a deflating pop.
    void TickUltimateFx()
    {
        if (ultimateUntil <= 0f) return;
        float left = ultimateUntil - Time.time;
        if (left <= 0f)
        {
            fx.Ring(Feet, 5f, new Color(WarningColor.r, WarningColor.g, WarningColor.b, 0.8f), 0.5f, 1.8f);
            fx.Puff(Hinge, Vector3.up, DustColor, 16, 4f, 0.35f, 0.6f, 180f);
            fx.Play(ProceduralSfx.Shatter, 0.5f, 1.25f);
            avatar.CameraRig.Shake(0.3f, 0.3f);
            avatar.CameraRig.FovPunch(-3f, 0.3f);
            RestoreUltimate(true);
            return;
        }
        if (Time.time < nextPulseAt) return;
        bool warning = left <= WarningTime;
        nextPulseAt = Time.time + (warning ? PulseInterval * 0.3f : PulseInterval);
        Color c = warning ? WarningColor : UltimateColor;
        fx.Ring(Feet, 2.4f * UltimateSize, new Color(c.r, c.g, c.b, warning ? 0.5f : 0.3f), 0.7f, 0.4f);
        ApplyTrailColor(warning ? WarningColor : UltimateColor);
    }

    // A hard landing kicks up dust and squashes the body.
    void TickLanding()
    {
        if (!avatar.IsGrounded)
        {
            peakFall = Mathf.Min(peakFall, avatar.VerticalVelocity);
            wasGrounded = false;
            return;
        }
        if (!wasGrounded && peakFall < -LandingSpeed)
        {
            float power = Mathf.Clamp01(-peakFall / 12f);
            float size = AreaScale;
            fx.Ring(Feet, (1.2f + 1.2f * power) * size, new Color(DustColor.r, DustColor.g, DustColor.b, 0.55f), 0.35f, 0.3f);
            fx.Puff(Feet, Vector3.up, DustColor, 6 + Mathf.RoundToInt(8f * power), 2.5f + 2f * power, 0.3f, 0.4f, 80f);
            avatar.Motion.localScale = new Vector3(1f + 0.12f * power, 1f - 0.2f * power, 1f + 0.12f * power);
            avatar.CameraRig.Shake(0.08f + 0.12f * power * size, 0.15f);
        }
        wasGrounded = true;
        peakFall = 0f;
    }

    // Roll ends in a low squash with a puff, then springs open.
    void RollLanded()
    {
        fx.EndSweep();
        shears.Jaw = weapon.restJaw;
        avatar.Motion.localRotation = Quaternion.identity;
        avatar.Motion.localScale = new Vector3(1.14f, 0.82f, 1.14f);
        fx.Ring(Feet, 1.5f * AreaScale, new Color(DustColor.r, DustColor.g, DustColor.b, 0.5f), 0.3f, 0.3f);
        fx.Puff(Feet, Vector3.up, DustColor, 8, 3f, 0.3f, 0.4f, 70f);
        avatar.CameraRig.Shake(0.1f, 0.12f);
    }

    void UpdateJump(float dt)
    {
        crouchTimer -= dt;
        if (crouchTimer > 0f)
        {
            shears.Jaw = Mathf.MoveTowards(shears.Jaw, weapon.jawClosed, JawSpeed * dt);
            Vector3 dip = avatar.Swinger.transform.InverseTransformVector(Vector3.down * CrouchDip);
            EaseMotion(dip, Quaternion.identity, Vector3.one, 30f, dt);
            return;
        }
        jumpQueued = false;
        avatar.Hop(JumpHeight);
        shears.Jaw = AirJaw;
    }

    // Cuts and snips open on the windup, squeeze shut through the strike, then relax.
    void UpdateJaw()
    {
        float jaw = weapon.restJaw;
        float u = swinger.PhaseProgress;
        switch (swinger.Phase)
        {
            case SwingPhase.Windup: jaw = Mathf.LerpUnclamped(jawFrom, currentSwing.jawWindup, currentSwing.windupCurve.Evaluate(u)); break;
            case SwingPhase.Strike: jaw = Mathf.LerpUnclamped(currentSwing.jawWindup, currentSwing.jawStrike, currentSwing.strikeCurve.Evaluate(u)); break;
            case SwingPhase.Recovery:
                // Cuts keep the blades half open so the next one chains without a pop.
                float settle = currentAlt ? weapon.restJaw : Mathf.Lerp(weapon.restJaw, currentSwing.jawWindup, 0.6f);
                jaw = Mathf.LerpUnclamped(currentSwing.jawStrike, settle, currentSwing.recoveryCurve.Evaluate(u));
                break;
        }
        shears.Jaw = jaw;
    }
}

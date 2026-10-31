using System.Collections.Generic;
using UnityEngine;

// Direct steering with a motor dash and a sustained forward grinding ultimate.
public class ChainsawMoveset : AvatarMoveset
{
    const float AttackBuffer = 0.4f;
    const float MoveSpeed = 11f;
    const float Acceleration = 90f;
    const float Braking = 120f;
    const float TurnScale = 2.5f;
    const float JumpHeight = 2f;
    const float CrouchDuration = 0.09f;
    const float CrouchDepth = 0.12f;
    const float CrouchPitch = 18f;
    const float KickReleaseSharpness = 40f;
    const float LandingBounceHeight = 0.2f;
    const float SlamHeight = 2.5f;
    const float SlamSpeed = 24f;
    const float ChainsawPush = 0.12f;
    const float JitterDistance = 0.012f;
    const float JitterDegrees = 2f;
    const float OverclockShake = 1.8f;
    const float LeanDegrees = 9f;
    const float BobHeight = 0.03f;
    const float BobHz = 2.4f;
    const float PitchKickDegrees = 5f;
    const float PitchSharpness = 10f;
    const float SkillDuration = 10f;
    const float SkillDurationScale = 0.65f;
    const float SkillCooldownSeconds = 16f;
    const float DashSeconds = 0.18f;
    const float DashSpeed = 26f;
    const float DashDamageScale = 0.65f;
    const float DashSpinDegrees = 720f;
    const float DriveRadius = 0.95f;
    const float DriveReach = 1.1f;
    const float DriveHeight = 0.8f;
    const float DriveAssistRange = 6f;
    const float DriveAssistAngle = 45f;
    const float DrivePoseSharpness = 60f;
    const float SpecialSeconds = 0.38f;
    const float SpecialRange = 4f;
    const float SpecialHalfAngle = 100f;
    const float SpecialDamageScale = 1.8f;
    const float SpecialLunge = 1.4f;
    const float UltimateSeconds = 2.4f;
    const float UltimateSpeed = 13f;
    const float UltimateSteering = 80f;
    const float UltimateTick = 0.1f;
    const float UltimateDamageScale = 0.5f;
    const float UltimateHop = 0.65f;
    const float UltimateSpinSpeed = 1080f;
    const float UprightPitch = -16f;
    // No somersault: it swung the flat of the blade into the target. The spin alone keeps the chain edge leading.
    const float FirstHitFlipDegrees = 0f;
    const float SlamAssistRange = 9f;
    const float SlamAssistAngle = 80f;
    const float SlamStandOff = 1.1f;
    const float SlamHomeSpeed = 18f;
    const float FirstHitFlipEnd = 0.65f;
    const float FirstHitWindupLean = -14f;
    // Presentation tuning.
    const float SpinWindupPull = 0.1f;
    const float SpinWindupRoll = 14f;
    const float SpinLeanDegrees = 22f;
    const float SpinStretch = 0.06f;
    const float AirBankDegrees = 24f;
    const float SlamStretch = 0.14f;
    const float SlamSquash = 0.2f;
    const float LandPulseSeconds = 0.16f;
    const float SlamLandShake = 2.6f;
    const float SlamLandFov = 5f;
    const float SwingSparkRate = 70f;
    const float GroundSparkRate = 160f;
    const float OverclockSparkRate = 14f;
    const float DashSparkRate = 320f;
    const float UltimateSparkRate = 150f;
    const float UltimateTipSparkRate = 110f;
    const float UltimateWobbleHz = 9f;
    const float UltimateWobbleDegrees = 7f;
    const int UltimateBigBeat = 3;
    // Hit feel tuning.
    const float BiteHold = 0.06f;
    const float FinisherHold = 0.14f;
    const float LandHold = 0.1f;
    const float GrindBeatSeconds = 0.07f;
    const float GrindShake = 0.05f;
    const float ShudderDistance = 0.018f;
    const float ShudderDegrees = 3f;
    const float TickPull = 0.08f;
    const float TickWobble = 0.05f;
    const float BitePull = 0.3f;
    const float DashCarry = 0.7f;
    const float SpecialCarry = 0.6f;
    const float UltimatePull = 0.14f;
    const float PullStandOff = 0.9f;
    const int BloodEveryTicks = 3;

    ChainsawDriver chainsaw;
    ChainsawFx fx;
    float pitchKick;
    enum BouncePhase { None, Crouch, Flip, Rebound }
    BouncePhase bounce;
    float bounceTime, flightDuration;
    bool bounceAirborne, slamAirborne, slamDriving;
    bool airAttackUsed, bufferedAirAttack;
    float bufferedAttackTime = -10f;
    enum DriveMode { None, Dash, Special, Ultimate }
    DriveMode drive;
    float driveTime, buffUntil;
    Vector3 driveDirection, previousDrivePosition;
    int groundHit = -1;
    bool comboAirborne;
    readonly Dictionary<Enemy, float> nextDriveHit = new Dictionary<Enemy, float>();
    readonly Collider[] contacts = new Collider[64];
    Vector3 lastTip, tipVelocity;
    float sparkAcc, driveAcc, landPulse, ultimateBeat;
    int ultimateHits;
    bool wasBuffed;
    float bitePause, grindBeat, lastDriveHitTime = -10f;
    float grindSide = 1f;
    int tickCount;

    ThirdPersonCamera Cam => avatar.CameraRig;
    Vector3 Feet => transform.position + Vector3.up * 0.05f;

    protected override float SkillCooldown => SkillCooldownSeconds;
    public override string SkillName => "오버클럭";
    public override float UltimateTimeRemaining => drive == DriveMode.Ultimate ? Mathf.Max(0f, UltimateSeconds - driveTime) : 0f;
    public override bool Busy => base.Busy || drive != DriveMode.None;
    public override string Status => Time.time < buffUntil ? "OVERCLOCK" : "";

    bool IsComboHit(int index) => !currentAlt && groundHit == index;

    public override void Configure(WeaponAvatar owner, WeaponData data, Transform weaponVisual)
    {
        base.Configure(owner, data, weaponVisual);
        chainsaw = weaponVisual.gameObject.AddComponent<ChainsawDriver>();
        fx = gameObject.AddComponent<ChainsawFx>();
        fx.Configure(weaponVisual, weapon.bladeTip);
        lastTip = fx.Tip;
        SprintMultiplier = 1.6f;
    }

    public override void Teardown()
    {
        CancelAttack();
        buffUntil = 0f;
        if (fx != null) Destroy(fx);
        base.Teardown();
    }

    protected override void CancelAttack()
    {
        base.CancelAttack();
        bufferedAttackTime = -10f;
        bufferedAirAttack = false;
        bounce = BouncePhase.None;
        slamAirborne = slamDriving = false;
        comboAirborne = false;
        drive = DriveMode.None;
        nextDriveHit.Clear();
        avatar.PlanarVelocity = Vector3.zero;
    }

    protected override void StartSwing(bool alt)
    {
        bounce = BouncePhase.None;
        slamAirborne = false;
        slamDriving = false;
        base.StartSwing(alt);
        groundHit = alt ? -1 : weapon.combo.IndexOf(currentSwing);
        comboAirborne = !alt;
        // Clone only the played swing; shared asset timings stay unchanged.
        if (Time.time < buffUntil)
        {
            currentSwing = JsonUtility.FromJson<SwingData>(JsonUtility.ToJson(currentSwing));
            currentSwing.windup *= SkillDurationScale;
            currentSwing.strike *= SkillDurationScale;
            currentSwing.recovery *= SkillDurationScale;
            swinger.Play(currentSwing);
        }
        if (alt)
        {
            airAttackUsed = true;
            avatar.Motion.localPosition = Vector3.zero;
            avatar.Motion.localRotation = Quaternion.identity;
            avatar.Motion.localScale = Vector3.one;
        }
        if (IsComboHit(2))
        {
            avatar.Hop(SlamHeight, true);
            // The head slam picks a wider target and homes on it while airborne.
            assistTarget = FindAssistTarget(SlamAssistRange, SlamAssistAngle);
            if (assistTarget != null) swingFacing = YawTo(assistTarget.transform.position);
        }
        // Every hit revs differently: a flick, a snap, then a full roar for the slam.
        fx.Surge(alt ? 0.5f : IsComboHit(2) ? 0.9f : IsComboHit(1) ? 0.35f : 0.55f);
        tickCount = 0;
    }

    protected override void OnStrikeBegan()
    {
        base.OnStrikeBegan();
        if (IsComboHit(2))
        {
            slamDriving = true;
            avatar.VerticalVelocity = -SlamSpeed;
        }
        StrikeFx();
    }

    // Each hit launches differently: kick-off sparks and a widening FOV for the spins, a plunge zoom for the slam.
    void StrikeFx()
    {
        if (IsComboHit(2))
        {
            Punch(0.1f, 0.15f, 2.5f, 0.2f);
            HitFeedback.Instance.PlaySwing(true);
            return;
        }
        HitFeedback.Instance.PlaySwing(currentAlt);
        if (currentAlt)
        {
            Punch(0.12f, 0.2f, -4f, 0.25f);
            fx.Ring(transform.position + Vector3.up * 0.9f, 26, 6f);
            return;
        }
        bool first = IsComboHit(0);
        Punch(first ? 0.1f : 0.08f, 0.15f, first ? -3f : -2f, 0.18f);
        fx.Sparks(Feet, -transform.forward + Vector3.up * 0.5f, first ? 20 : 14, 6f, 0.6f);
    }

    void Punch(float shake, float shakeTime, float fov, float fovTime)
    {
        if (Cam == null) return;
        if (shake > 0f) Cam.Shake(shake, shakeTime);
        if (fov != 0f) Cam.FovPunch(fov, fovTime);
    }

    protected override void TickAttack(AvatarInput input, float dt)
    {
        if (drive != DriveMode.None) return;
        if (avatar.IsGrounded)
        {
            airAttackUsed = false;
            if (!swinging) comboAirborne = false;
            if (bufferedAirAttack) bufferedAttackTime = -10f;
        }
        // Only left clicks queue attacks; retain whether the press happened in the air.
        if (input.attack)
        {
            bufferedAirAttack = !avatar.IsGrounded && !comboAirborne;
            bufferedAttackTime = bufferedAirAttack && airAttackUsed ? -10f : Time.unscaledTime;
        }
        bool wantAttack = Time.unscaledTime - bufferedAttackTime <= AttackBuffer;
        bool canStart = !swinging
            || (swinger.Phase == SwingPhase.Recovery && swinger.PhaseProgress >= currentSwing.chainAt);
        bool validAttack = bufferedAirAttack
            ? !avatar.IsGrounded && !airAttackUsed && weapon.altCombo.Count > 0
            : (avatar.IsGrounded || (swinging && comboAirborne)) && weapon.combo.Count > 0;
        if (wantAttack && canStart && validAttack)
        {
            bufferedAttackTime = -10f;
            StartSwing(bufferedAirAttack);
        }
        swinger.Holding = swinging && swinger.Phase == SwingPhase.Strike
            && (bitePause > 0f || (weapon.sliceDelay > 0f && hitbox.Touching));
    }

    // A click grinds like a hold: even a fast spin must saw for the full delay.
    protected override bool SlicesNow(HitContact contact) => false;

    protected override void OnEnemyHit(Enemy enemy, bool tick, Vector3 direction)
    {
        chainsaw.NotifyContact();
        swinger.Holding = swinging && weapon.sliceDelay > 0f && swinger.Phase == SwingPhase.Strike;
        if (tick && !enemy.IsDead) avatar.Lunge(direction, ChainsawPush, weapon.tickInterval);
        if (tick) GrindTickFx(enemy, direction);
        else BiteFx(enemy, direction);
    }

    // The teeth catch: a short pose hold, then each hit throws sparks and shoves the enemy its own way.
    void BiteFx(Enemy enemy, Vector3 direction)
    {
        tickCount = 0;
        grindBeat = 0f;
        Vector3 point = enemy.Center;
        Vector3 sweep = tipVelocity.sqrMagnitude > 1f ? tipVelocity.normalized : transform.right;
        bool finisher = IsComboHit(2);
        bitePause = Mathf.Max(bitePause, finisher ? FinisherHold : BiteHold);
        fx.Surge(finisher ? 1f : 0.6f);
        if (finisher)
        {
            // Last hit: a crushing shockwave and a roar.
            Punch(0.55f, 0.3f, 7f, 0.3f);
            fx.Ring(point, 44, 10f);
            fx.Sparks(point, direction + Vector3.up * 0.6f, 36, 12f, 0.7f, 0.07f, 0.45f);
            fx.Smear(point, 2.2f, 0.3f, new Color(1f, 0.8f, 0.45f, 0.6f));
            fx.Play(ProceduralSfx.HitHeavy, 0.9f, 0.7f);
            fx.Play(ProceduralSfx.Shatter, 0.5f, 1.1f);
            if (weapon.bloody) HitFeedback.Instance.PlayBlood(point, direction, 1.4f);
            return;
        }
        if (currentAlt)
        {
            // Air spin sucks the enemy into the blade and flings sparks out all round.
            Punch(0.16f, 0.2f, 3f, 0.2f);
            fx.Ring(point, 24, 7f);
            fx.Play(ProceduralSfx.Hit, 0.6f, 1.15f);
            Drag(enemy, transform.position - enemy.transform.position, BitePull);
            return;
        }
        if (IsComboHit(0))
        {
            // Kick spin: the cut tears upward, the enemy is hauled in toward the rising blade.
            Punch(0.2f, 0.2f, 2.5f, 0.2f);
            fx.Sparks(point, direction * 0.5f + Vector3.up, 24, 9f, 0.5f);
            fx.Play(ProceduralSfx.Hit, 0.7f, 1.0f);
            Drag(enemy, transform.position - enemy.transform.position, BitePull);
        }
        else
        {
            // Left spin: a fast tearing slash; sparks and enemy are whipped along the sweep.
            Punch(0.14f, 0.14f, -2.5f, 0.15f);
            fx.Sparks(point, sweep + Vector3.up * 0.3f, 22, 11f, 0.35f);
            fx.Play(ProceduralSfx.Hit, 0.65f, 1.3f);
            Drag(enemy, sweep, BitePull * 1.2f);
        }
    }

    // Sawing rhythm: alternating sparks left and right, wobble, and periodic gore.
    void GrindTickFx(Enemy enemy, Vector3 direction)
    {
        grindSide = -grindSide;
        tickCount++;
        Vector3 toPlayer = transform.position - enemy.transform.position;
        Drag(enemy, toPlayer, TickPull);
        Drag(enemy, transform.right * grindSide, TickWobble);
        Vector3 spray = -direction * 0.6f + transform.right * grindSide * 0.6f + Vector3.up * 0.7f;
        fx.Sparks(enemy.Center, spray, 6, 8f, 0.4f);
        if (tickCount % BloodEveryTicks == 0 && weapon.bloody)
            HitFeedback.Instance.PlayBlood(enemy.Center, spray, 0.35f);
    }

    // Shove an enemy along the ground without touching its own AI; stops short of the player.
    void Drag(Enemy enemy, Vector3 direction, float distance)
    {
        if (enemy == null || enemy.IsDead) return;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return;
        Vector3 away = enemy.transform.position - transform.position;
        away.y = 0f;
        // Pulling toward the player must not carry it through the player.
        if (Vector3.Dot(direction, away) < 0f && away.magnitude < enemy.Radius + PullStandOff) return;
        var body = enemy.GetComponent<CharacterController>();
        if (body != null && body.enabled) body.Move(direction.normalized * distance);
    }

    protected override void OnSkill()
    {
        buffUntil = Time.time + SkillDuration;
        // Overclock ignition: rev flare, shock ring and a quick zoom.
        Punch(0.22f, 0.3f, 5f, 0.3f);
        HitFeedback.Instance.PlaySwing(true);
        fx.Ring(transform.position + Vector3.up * 0.5f, 40, 7f);
        fx.Sparks(fx.Tip, Vector3.up, 30, 8f, 0.8f);
        fx.Surge(1.3f);
    }

    protected override void OnDash(Vector3 dir)
    {
        BeginDrive(DriveMode.Dash, dir);
        avatar.GrantInvulnerability(DashSeconds);
    }

    protected override void OnSpecial()
    {
        CancelAttack();
        BeginDrive(DriveMode.Special, CameraForward);
        avatar.Lunge(driveDirection, SpecialLunge, SpecialSeconds);
    }

    protected override void OnUltimate()
    {
        CancelAttack();
        BeginDrive(DriveMode.Ultimate, CameraForward);
        avatar.Hop(UltimateHop, true);
    }

    void BeginDrive(DriveMode mode, Vector3 direction)
    {
        drive = mode;
        driveTime = 0f;
        nextDriveHit.Clear();
        previousDrivePosition = transform.position;
        assistTarget = FindAssistTarget(DriveAssistRange,
            mode == DriveMode.Special ? SpecialHalfAngle : DriveAssistAngle, direction);
        driveDirection = direction.normalized;
        if (assistTarget != null)
        {
            Vector3 to = assistTarget.transform.position - transform.position;
            to.y = 0f;
            driveDirection = to.normalized;
        }
        transform.rotation = Quaternion.LookRotation(driveDirection);
        avatar.PlanarVelocity = Vector3.zero;
        DriveStartFx(mode);
    }

    void DriveStartFx(DriveMode mode)
    {
        driveAcc = 0f;
        ultimateBeat = 0f;
        ultimateHits = 0;
        HitFeedback.Instance.PlaySwing(true);
        lastDriveHitTime = -10f;
        fx.Surge(mode == DriveMode.Ultimate ? 1.2f : mode == DriveMode.Special ? 0.9f : 0.7f);
        if (mode == DriveMode.Dash)
        {
            Punch(0.12f, 0.15f, -6f, 0.2f);
            fx.Sparks(Feet, -driveDirection + Vector3.up * 0.3f, 26, 8f, 0.6f);
        }
        else if (mode == DriveMode.Special)
        {
            Punch(0.25f, 0.25f, 5f, 0.3f);
            fx.Ring(Feet, 24, 7f);
        }
        else
        {
            Punch(0.3f, 0.3f, -7f, 0.4f);
            fx.Ring(Feet, 50, 9f);
            fx.Sparks(Feet, Vector3.up, 24, 7f, 0.8f);
        }
    }

    bool TickDrive(AvatarInput input, float dt)
    {
        if (drive == DriveMode.None) return false;
        float duration = drive == DriveMode.Dash ? DashSeconds
            : drive == DriveMode.Special ? SpecialSeconds : UltimateSeconds;
        if (driveTime >= duration)
        {
            if (drive == DriveMode.Dash)
            {
                fx.Sparks(Feet, driveDirection, 12, 6f, 0.7f);
                // Brake: the FOV snaps back as the roll ends.
                Punch(0f, 0f, 3f, 0.15f);
            }
            else if (drive == DriveMode.Ultimate)
            {
                // Grinder winds down with a final burst, bigger the more it chewed through.
                float chewed = Mathf.Clamp01(ultimateHits / 12f);
                Punch(0.35f + 0.3f * chewed, 0.3f, 5f + 3f * chewed, 0.3f);
                fx.Ring(Feet, 50, 9f);
                fx.Sparks(fx.Tip, driveDirection + Vector3.up * 0.4f, 30, 10f, 0.8f, 0.06f, 0.4f);
                fx.Smear(Feet + Vector3.up * 0.3f, 2.6f, 0.35f, new Color(1f, 0.75f, 0.4f, 0.55f));
                fx.Play(ProceduralSfx.HitHeavy, 0.85f, 0.75f);
                if (ultimateHits > 0) fx.Play(ProceduralSfx.Shatter, 0.5f, 1.05f);
                fx.Surge(1f);
            }
            drive = DriveMode.None;
            avatar.PlanarVelocity = Vector3.zero;
            avatar.Motion.localRotation = Quaternion.identity;
            return false;
        }

        driveTime += dt;
        if (drive == DriveMode.Ultimate)
        {
            Vector3 steering = input.wish;
            if (steering.sqrMagnitude < 0.01f)
            {
                assistTarget = FindAssistTarget(DriveAssistRange, DriveAssistAngle, driveDirection);
                if (assistTarget != null) steering = assistTarget.transform.position - transform.position;
            }
            steering.y = 0f;
            if (steering.sqrMagnitude > 0.01f)
                driveDirection = Vector3.RotateTowards(driveDirection, steering.normalized,
                    UltimateSteering * Mathf.Deg2Rad * dt, 0f);
            // Small repeated hops keep the chain grinding near ground-level enemies.
            if (avatar.IsGrounded && avatar.VerticalVelocity <= 0f) avatar.Hop(UltimateHop);
        }
        transform.rotation = Quaternion.LookRotation(driveDirection);
        avatar.PlanarVelocity = driveDirection * (drive == DriveMode.Dash ? DashSpeed
            : drive == DriveMode.Ultimate ? UltimateSpeed : 0f);
        chainsaw.Rev = 1f;

        Quaternion restInverse = Quaternion.Inverse(Quaternion.Euler(weapon.restEuler));
        float pitch = drive == DriveMode.Dash ? DashSpinDegrees * driveTime / DashSeconds
            : drive == DriveMode.Ultimate ? UltimateSpinSpeed * driveTime : 0f;
        float yaw = drive == DriveMode.Special
            ? Mathf.Lerp(-SpecialHalfAngle, SpecialHalfAngle, Mathf.Clamp01(driveTime / duration)) : 0f;
        // Ultimate rattles side to side and pulses; dash and special stretch.
        float roll = drive == DriveMode.Ultimate
            ? Mathf.Sin(driveTime * UltimateWobbleHz * Mathf.PI * 2f) * UltimateWobbleDegrees : 0f;
        float stretch = drive == DriveMode.Ultimate ? 1f + 0.04f * Mathf.Sin(driveTime * 50f) : 1.1f;
        EaseMotion(Vector3.zero, Quaternion.AngleAxis(pitch, restInverse * Vector3.right)
            * Quaternion.AngleAxis(yaw, restInverse * Vector3.up)
            * Quaternion.AngleAxis(roll, restInverse * Vector3.forward), Vector3.one * stretch, DrivePoseSharpness, dt);
        TickDriveFx(dt);
        return true;
    }

    void TickDriveFx(float dt)
    {
        if (drive == DriveMode.Dash)
        {
            // Floor friction sparks plus a fading glow afterimage along the path.
            int n = ChainsawFx.Steps(ref driveAcc, DashSparkRate, dt);
            if (n > 0) fx.Sparks(Feet, -driveDirection + Vector3.up * 0.3f, n, 7f, 0.6f);
            fx.Grind(0.8f, false);
            fx.Smear(transform.position + Vector3.up * 0.8f, 1.1f, 0.22f, new Color(1f, 0.7f, 0.3f, 0.5f));
        }
        else if (drive == DriveMode.Special)
        {
            // A crescent of glow and sparks follows the sweep.
            float yaw = Mathf.Lerp(-SpecialHalfAngle, SpecialHalfAngle, Mathf.Clamp01(driveTime / SpecialSeconds));
            Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * driveDirection;
            Vector3 center = transform.position + Vector3.up * 0.9f;
            fx.Smear(center + dir * (SpecialRange * 0.45f), 0.9f, 0.25f, new Color(1f, 0.8f, 0.4f, 0.55f));
            fx.Smear(center + dir * (SpecialRange * 0.8f), 1.1f, 0.25f, new Color(1f, 0.6f, 0.2f, 0.5f));
            fx.Sparks(center + dir * (SpecialRange * 0.8f), dir, 5, 7f, 0.5f);
        }
        else if (drive == DriveMode.Ultimate)
        {
            int n = ChainsawFx.Steps(ref driveAcc, UltimateSparkRate, dt);
            if (n > 0) fx.Sparks(Feet, -driveDirection + Vector3.up * 0.4f, n, 8f, 1f);
            int t = ChainsawFx.Steps(ref sparkAcc, UltimateTipSparkRate, dt);
            if (t > 0) fx.Sparks(fx.Tip, tipVelocity + Vector3.up, t, 7f, 0.5f);
            // Beat: a small FOV pulse and rumble on every damage tick.
            ultimateBeat += dt;
            if (ultimateBeat >= UltimateTick)
            {
                ultimateBeat -= UltimateTick;
                // Rumble builds as the grinder chews through a crowd.
                Punch(0.06f + 0.012f * Mathf.Min(ultimateHits, 10), 0.12f, -2.2f, 0.2f);
            }
            fx.Grind(1f, Time.time - lastDriveHitTime < 0.3f);
        }
    }

    // Spark and overclock work that runs every frame regardless of mode.
    void TickFx(float dt)
    {
        if (dt <= 0f) return;
        Vector3 tip = fx.Tip;
        tipVelocity = Vector3.ClampMagnitude((tip - lastTip) / dt, 40f);
        lastTip = tip;

        bool buffed = Time.time < buffUntil;
        if (buffed)
        {
            int n = ChainsawFx.Steps(ref sparkAcc, OverclockSparkRate, dt);
            if (n > 0) fx.Sparks(tip, Vector3.up, n, 4f, 1.2f, 0.035f, 0.25f);
        }
        else if (wasBuffed) fx.Sparks(tip, Vector3.up, 16, 3f, 1f, 0.04f, 0.3f);
        wasBuffed = buffed;
        if (buffed) fx.Grind(0.18f, false);

        bitePause = Mathf.Max(0f, bitePause - dt);
        if (!swinging || swinger.Phase != SwingPhase.Strike) return;
        if (hitbox.Touching) GrindFeel(dt);
        // Sparks fly along the blade path; sawing into an enemy or the floor throws many more.
        float rate = SwingSparkRate * (hitbox.Touching ? 3f : 1f) * (currentSwing.heavy ? 1.5f : 1f);
        if (tip.y < 0.35f) rate += GroundSparkRate;
        int count = ChainsawFx.Steps(ref driveAcc, rate, dt);
        if (count > 0)
            fx.Sparks(tip, (tipVelocity.sqrMagnitude > 1f ? tipVelocity.normalized : transform.forward) + Vector3.up * 0.4f,
                count, 6f + tipVelocity.magnitude * 0.2f, 0.5f);
    }

    // Continuous grinding: the camera shivers on a rhythm and the engine layer revs up under load.
    void GrindFeel(float dt)
    {
        fx.Grind(1f);
        grindBeat += dt;
        if (grindBeat < GrindBeatSeconds) return;
        grindBeat -= GrindBeatSeconds;
        Punch(GrindShake * (grindSide > 0f ? 1.6f : 1f), 0.08f, 0f, 0f);
    }

    // Sweep the travelled path after CharacterController movement, including fast dash frames.
    void LateUpdate()
    {
        if (drive == DriveMode.None) return;
        Vector3 center = transform.position + Vector3.up * DriveHeight;
        int count = drive == DriveMode.Special
            ? Physics.OverlapSphereNonAlloc(center, SpecialRange, contacts, ~0, QueryTriggerInteraction.Ignore)
            : Physics.OverlapCapsuleNonAlloc(previousDrivePosition + Vector3.up * DriveHeight,
                center + driveDirection * DriveReach, DriveRadius, contacts, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Enemy enemy = contacts[i].GetComponentInParent<Enemy>();
            if (enemy == null || enemy.IsDead) continue;
            Vector3 to = enemy.transform.position - transform.position;
            to.y = 0f;
            if (drive == DriveMode.Special && Vector3.Angle(driveDirection, to) > SpecialHalfAngle) continue;
            if (drive == DriveMode.Ultimate
                && Vector3.Dot(driveDirection, enemy.transform.position - previousDrivePosition) < -enemy.Radius) continue;
            if (nextDriveHit.TryGetValue(enemy, out float due) && Time.time < due) continue;
            nextDriveHit[enemy] = drive == DriveMode.Ultimate ? Time.time + UltimateTick : float.PositiveInfinity;
            HitDriveEnemy(enemy, to);
        }
        previousDrivePosition = transform.position;
    }

    void HitDriveEnemy(Enemy enemy, Vector3 to)
    {
        bool special = drive == DriveMode.Special;
        bool tick = drive == DriveMode.Ultimate;
        float scale = special ? SpecialDamageScale : tick ? UltimateDamageScale : DashDamageScale;
        var info = new HitInfo
        {
            damage = Mathf.Max(1, Mathf.RoundToInt(weapon.damage * scale)),
            direction = to.sqrMagnitude > 0.01f ? to.normalized : driveDirection,
            point = enemy.Center,
            knockback = weapon.knockback * (tick ? 0.08f : 0.6f),
            stagger = tick ? 0.15f : 0.4f,
            tick = tick,
            slice = weapon.sliceOnKill,
            bloody = weapon.bloody,
            sliceNow = special,
            sliceDelay = tick ? weapon.sliceDelay : 0f,
            sliceRadius = weapon.sliceBurstRadius,
            cutPoint = enemy.Center,
            cutNormal = Vector3.up,
        };
        enemy.TakeHit(info);
        avatar.RecordHit(info.damage, enemy.IsDead);
        chainsaw.NotifyContact();
        lastDriveHitTime = Time.time;
        if (enemy.IsDead && weapon.sliceOnKill) HitFeedback.Instance.PlaySlice(info.point, info.direction, weapon.bloody);
        else HitFeedback.Instance.PlayTick(info);
        DriveHitFx(info, special, tick);
        DriveShove(enemy, info.direction, special, tick);
    }

    // Dash carries the enemy along the path, special flings it along the sweep, ultimate drags it under the saw.
    void DriveShove(Enemy enemy, Vector3 direction, bool special, bool tick)
    {
        if (special) Drag(enemy, Quaternion.AngleAxis(90f, Vector3.up) * direction, SpecialCarry);
        else if (tick)
        {
            Vector3 target = transform.position + driveDirection * (enemy.Radius + PullStandOff + 0.4f);
            Drag(enemy, target - enemy.transform.position, UltimatePull);
            Drag(enemy, transform.right * grindSide, TickWobble);
        }
        else Drag(enemy, driveDirection, DashCarry);
    }

    void DriveHitFx(HitInfo info, bool special, bool tick)
    {
        if (!tick)
        {
            // Dash and special land as one solid hit.
            Punch(special ? 0.3f : 0.22f, 0.18f, special ? 3f : 2f, 0.15f);
            // Dash sparks stream back along the path; the special's fan out along the sweep.
            Vector3 dir = special ? info.direction + Vector3.up * 0.3f : driveDirection + Vector3.up * 0.4f;
            fx.Sparks(info.point, dir, special ? 30 : 20, 10f, special ? 0.7f : 0.45f);
            fx.Play(special ? ProceduralSfx.HitHeavy : ProceduralSfx.Hit, special ? 0.8f : 0.6f, special ? 0.9f : 1.25f);
            if (weapon.bloody) HitFeedback.Instance.PlayBlood(info.point, dir, special ? 0.9f : 0.6f);
            return;
        }
        // Grinding rhythm: sparks alternate sides, a tick pitches up with the combo, every few ticks a heavier chunk rips off.
        fx.Sparks(info.point, info.direction * 0.5f + transform.right * grindSide + Vector3.up * 0.5f, 8, 8f, 0.6f);
        fx.Play(ProceduralSfx.Hit, 0.25f, 0.95f + 0.05f * Mathf.Min(ultimateHits, 12));
        grindSide = -grindSide;
        if (++ultimateHits % UltimateBigBeat != 0) return;
        Punch(0.2f, 0.16f, 2f, 0.1f);
        fx.Play(ProceduralSfx.HitHeavy, 0.4f, 1.1f);
        fx.Sparks(info.point, info.direction + Vector3.up * 0.4f, 24, 11f, 0.9f, 0.06f, 0.4f);
        if (weapon.bloody) HitFeedback.Instance.PlayBlood(info.point, info.direction, 0.5f);
    }

    protected override void Locomote(AvatarInput input, float dt)
    {
        TickFx(dt);
        if (TickDrive(input, dt)) return;
        // Holding freezes the body spin as well as the blade pose.
        if (!swinger.Holding) UpdateFacing(input.wish, dt, swinging ? 1f : TurnScale);

        if (swinging && IsComboHit(2) && !avatar.IsGrounded) slamAirborne = true;
        if (slamDriving && slamAirborne && avatar.IsGrounded)
        {
            slamDriving = false;
            Punch(weapon.shake * SlamLandShake, 0.3f, SlamLandFov, 0.25f);
            landPulse = LandPulseSeconds;
            bitePause = Mathf.Max(bitePause, LandHold);
            fx.Play(ProceduralSfx.HitHeavy, 0.9f, 0.6f);
            fx.Play(ProceduralSfx.Shatter, 0.4f, 0.9f);
            fx.Ring(Feet, 40, 9f);
            fx.Sparks(Feet + Vector3.up * 0.1f, Vector3.up, 22, 8f, 0.7f);
            fx.Smear(Feet + Vector3.up * 0.2f, 2.2f, 0.35f, new Color(0.8f, 0.7f, 0.55f, 0.45f));
        }
        // WeaponAvatar applies gravity after this tick; cancel that step while sawing in place.
        if (swinger.Holding) avatar.VerticalVelocity = -avatar.Gravity * dt;
        else if (slamDriving && !avatar.IsGrounded)
            avatar.VerticalVelocity = Mathf.Min(avatar.VerticalVelocity, -SlamSpeed);

        float forwardSpeed = Vector3.Dot(avatar.PlanarVelocity, transform.forward);
        float scale = swinging ? weapon.swingMoveScale : 1f;
        Accelerate(input.wish * (MoveSpeed * MoveSpeedScale * scale),
            input.wish.sqrMagnitude > 0.01f ? Acceleration : Braking, dt);
        SlamHoming(dt);
        float speed = avatar.PlanarVelocity.magnitude;
        float groundSpeed = avatar.IsGrounded ? speed : 0f;
        chainsaw.Rev = swinging ? 1f : Mathf.Lerp(0.3f, 1f, groundSpeed / MoveSpeed);

        float acceleration = (Vector3.Dot(avatar.PlanarVelocity, transform.forward) - forwardSpeed)
            / Mathf.Max(Acceleration * dt, 0.0001f);
        float kick = !swinging && avatar.IsGrounded ? Mathf.Clamp01(acceleration) * PitchKickDegrees : 0f;
        pitchKick = Mathf.Lerp(pitchKick, kick, 1f - Mathf.Exp(-PitchSharpness * dt));

        if (swinging)
        {
            bounce = BouncePhase.None;
            if (currentAlt) AirSpinMotion(dt);
            else if (IsComboHit(0)) KickMotion(dt);
            else if (IsComboHit(1)) LeftSpinMotion(dt);
            else if (IsComboHit(2)) SlamMotion(dt);
            else RelaxMotion(dt);
            // The blade shudders in the pose while buried.
            if (swinger.Holding) Shudder();
            return;
        }

        if (input.jump && avatar.IsGrounded && bounce == BouncePhase.None)
        {
            bounce = BouncePhase.Crouch;
            bounceTime = 0f;
        }
        if (TickBounce(dt)) return;

        float run = Mathf.Clamp01(groundSpeed / MoveSpeed);
        float shake = Mathf.Lerp(0.25f, 1f, run) * (Time.time < buffUntil ? OverclockShake : 1f);
        // Motion sits below Pose, so pitch about the avatar's right axis in the edge rest pose.
        Quaternion restInverse = Quaternion.Inverse(Quaternion.Euler(weapon.restEuler));
        Vector3 pitchAxis = restInverse * Vector3.right;
        // Lean into sideways travel and chug up and down with the motor while running.
        float lean = -Vector3.Dot(avatar.PlanarVelocity, transform.right) / MoveSpeed * LeanDegrees;
        Vector3 bob = restInverse * Vector3.up * (Mathf.Abs(Mathf.Sin(Time.time * BobHz * Mathf.PI)) * BobHeight * run);
        EaseMotion(Random.insideUnitSphere * (JitterDistance * shake) + bob,
            Quaternion.AngleAxis(UprightPitch - pitchKick, pitchAxis)
                * Quaternion.AngleAxis(lean, restInverse * Vector3.forward)
                * Quaternion.Euler(Random.insideUnitSphere * (JitterDegrees * shake)), Vector3.one, 60f, dt);
    }

    // Keeps the slam's turn and flight on the target so the plunge lands on it instead of whiffing past.
    void SlamHoming(float dt)
    {
        if (!swinging || !IsComboHit(2) || assistTarget == null || assistTarget.IsDead || avatar.IsGrounded && swinger.Phase != SwingPhase.Windup) return;
        if (swinger.Phase == SwingPhase.Recovery) return;
        swingFacing = YawTo(assistTarget.transform.position);
        Vector3 to = assistTarget.transform.position - transform.position;
        to.y = 0f;
        float remaining = swinger.Phase == SwingPhase.Windup ? (1f - swinger.PhaseProgress) * currentSwing.windup + 0.12f : 0.1f;
        float distance = Mathf.Max(0f, to.magnitude - SlamStandOff);
        float speed = Mathf.Min(SlamHomeSpeed, distance / remaining);
        avatar.PlanarVelocity = Vector3.MoveTowards(avatar.PlanarVelocity, to.normalized * speed, 200f * dt);
    }

    void Shudder()
    {
        avatar.Motion.localPosition += Random.insideUnitSphere * ShudderDistance;
        avatar.Motion.localRotation *= Quaternion.Euler(Random.insideUnitSphere * ShudderDegrees);
    }

    // First ground hit: sink and lean back during the windup, flip upright while rising, then spin the cut.
    void KickMotion(float dt)
    {
        if (swinger.Phase == SwingPhase.Strike)
        {
            Vector3 pitchAxis = avatar.Motion.parent.InverseTransformDirection(transform.right);
            float flip = Mathf.SmoothStep(0f, 1f, swinger.PhaseProgress / FirstHitFlipEnd);
            EaseMotion(Vector3.zero, Quaternion.AngleAxis(FirstHitFlipDegrees * flip, pitchAxis),
                Vector3.one * 1.1f, KickReleaseSharpness, dt);
            return;
        }
        if (swinger.Phase != SwingPhase.Windup)
        {
            EaseMotion(Vector3.zero, Quaternion.identity, Vector3.one, KickReleaseSharpness, dt);
            return;
        }
        // Motion follows the windup pose, so express world down and the avatar's right in Pose space.
        Transform pose = avatar.Motion.parent;
        float u = swinger.PhaseProgress;
        EaseMotion(pose.InverseTransformDirection(Vector3.down) * (CrouchDepth * u),
            Quaternion.AngleAxis(FirstHitWindupLean * u, pose.InverseTransformDirection(transform.right)),
            Vector3.one * (1f - 0.07f * u), 30f, dt);
    }

    // Second hit: pull back and cock the saw, then lean into the left turn as it whips round.
    void LeftSpinMotion(float dt)
    {
        Transform pose = avatar.Motion.parent;
        Vector3 forwardAxis = pose.InverseTransformDirection(transform.forward);
        float u = swinger.PhaseProgress;
        if (swinger.Phase == SwingPhase.Windup)
            EaseMotion(pose.InverseTransformDirection(-transform.forward) * (SpinWindupPull * u),
                Quaternion.AngleAxis(SpinWindupRoll * u, forwardAxis), Vector3.one * (1f - 0.04f * u), 30f, dt);
        else if (swinger.Phase == SwingPhase.Strike)
        {
            float arc = Mathf.Sin(u * Mathf.PI);
            EaseMotion(Vector3.zero, Quaternion.AngleAxis(-SpinLeanDegrees * arc, forwardAxis),
                Vector3.one * (1f + SpinStretch * arc), 25f, dt);
        }
        else RelaxMotion(dt);
    }

    // Slam: stretch while rising and plunging, squash on touchdown.
    void SlamMotion(float dt)
    {
        Transform pose = avatar.Motion.parent;
        landPulse = Mathf.MoveTowards(landPulse, 0f, dt);
        float windup = swinger.Phase == SwingPhase.Windup ? swinger.PhaseProgress : 0f;
        float stretch = slamDriving ? SlamStretch : SlamStretch * windup;
        float squash = SlamSquash * landPulse / LandPulseSeconds;
        EaseMotion(Vector3.zero, Quaternion.AngleAxis(-10f * windup, pose.InverseTransformDirection(transform.right)),
            Vector3.one * (1f + stretch - squash), 30f, dt);
    }

    // Air 360: tuck while winding up, then bank hard through the spin.
    void AirSpinMotion(float dt)
    {
        Transform pose = avatar.Motion.parent;
        float u = swinger.PhaseProgress;
        if (swinger.Phase == SwingPhase.Windup)
            EaseMotion(pose.InverseTransformDirection(Vector3.down) * (CrouchDepth * 0.5f * u),
                Quaternion.identity, Vector3.one * (1f - 0.05f * u), 30f, dt);
        else if (swinger.Phase == SwingPhase.Strike)
        {
            float arc = Mathf.Sin(u * Mathf.PI);
            EaseMotion(Vector3.zero, Quaternion.AngleAxis(AirBankDegrees * arc, pose.InverseTransformDirection(transform.forward)),
                Vector3.one * (1f + 0.08f * arc), 25f, dt);
        }
        else RelaxMotion(dt);
    }

    bool TickBounce(float dt)
    {
        if (bounce == BouncePhase.None) return false;

        bounceTime += dt;
        Quaternion restInverse = Quaternion.Inverse(Quaternion.Euler(weapon.restEuler));
        Vector3 pitchAxis = restInverse * Vector3.right;
        if (bounce == BouncePhase.Crouch)
        {
            float u = Mathf.Clamp01(bounceTime / CrouchDuration);
            EaseMotion(restInverse * Vector3.down * (CrouchDepth * u),
                Quaternion.AngleAxis(CrouchPitch * u, pitchAxis), Vector3.one, 60f, dt);
            if (u >= 1f)
            {
                avatar.Hop(JumpHeight, true);
                flightDuration = 2f * avatar.VerticalVelocity / -avatar.Gravity;
                bounce = BouncePhase.Flip;
                bounceTime = 0f;
                bounceAirborne = false;
            }
            return true;
        }

        if (!avatar.IsGrounded) bounceAirborne = true;
        if (bounceAirborne && avatar.IsGrounded && avatar.VerticalVelocity <= 0f)
        {
            if (bounce == BouncePhase.Flip)
            {
                avatar.Hop(LandingBounceHeight, true);
                flightDuration = 2f * avatar.VerticalVelocity / -avatar.Gravity;
                bounce = BouncePhase.Rebound;
                bounceTime = 0f;
                bounceAirborne = false;
            }
            else bounce = BouncePhase.None;
            // Finish the full turn before the small landing spring.
            avatar.Motion.localRotation = Quaternion.identity;
            avatar.Motion.localPosition = Vector3.zero;
            return true;
        }

        float progress = Mathf.Clamp01(bounceTime / flightDuration);
        float pitch = bounce == BouncePhase.Flip
            ? 360f * progress + CrouchPitch * (1f - progress)
            : -8f * Mathf.Sin(progress * Mathf.PI);
        EaseMotion(Vector3.zero, Quaternion.AngleAxis(pitch, pitchAxis), Vector3.one, 60f, dt);
        return true;
    }
}

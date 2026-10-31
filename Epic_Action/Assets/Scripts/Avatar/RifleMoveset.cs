using UnityEngine;

// The rifle stands on its stock and bounces along; a jump flips it muzzle down and shots push it up.
// While firing it lies level and glides. The trigger remains independent of the stock combo.
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
    // Aiming through the sights: the aperture sits this high above the avatar origin.
    const float SightHeight = 1.2f;
    const float SightPoseSharpness = 60f;
    const float AimTurnSpeed = 1800f;
    const float ShotRecoil = 0.25f;
    const float ShotRecoilTime = 0.06f;
    const float ShotStagger = 0.12f;
    const float ShotKnockbackScale = 0.15f;
    const float TracerLife = 0.045f;
    const float TracerWidth = 0.025f;
    static readonly SwingData HeavyShot = new SwingData { heavy = true };

    Camera aimCamera;
    Material tracerMaterial;
    LineRenderer tracer;
    Transform sightPoint;
    // How far the line from rear aperture to front post points above the barrel axis.
    float sightTilt;
    float tracerTimer, hopTimer, fireTimer, jumpShotTimer, jumpFlipTimer, squashTimer, pitchKick;
    int jumpShotsLeft;
    bool firing, aiming, jumpQueued, wasGrounded;
    Vector3 aimPoint;

    public override bool Busy => base.Busy || firing;
    public override float ChargeProgress => 0f;
    public override bool Aiming => aiming;
    public override string Status => (firing ? "연사 중\n" : "") + "V 근접공격 · 우클릭 유지 가늠좌 조준";

    public override void Configure(WeaponAvatar owner, WeaponData data, Transform weaponVisual)
    {
        base.Configure(owner, data, weaponVisual);
        var sight = new GameObject("RifleSight").transform;
        sight.SetParent(weaponVisual, false);
        sight.localPosition = data.rifleSightRear;
        sightPoint = sight;
        Vector3 sightLine = data.rifleSightFront - data.rifleSightRear;
        sightTilt = Mathf.Atan2(sightLine.y, sightLine.z) * Mathf.Rad2Deg;
        aimCamera = owner.CameraRig != null ? owner.CameraRig.GetComponent<Camera>() : Camera.main;
        tracerMaterial = new Material(Shader.Find("Sprites/Default"));
        var go = new GameObject("RifleTracer");
        go.transform.SetParent(transform, false);
        tracer = go.AddComponent<LineRenderer>();
        tracer.sharedMaterial = tracerMaterial;
        tracer.useWorldSpace = true;
        tracer.positionCount = 2;
        tracer.startWidth = TracerWidth;
        tracer.endWidth = TracerWidth * 0.3f;
        tracer.startColor = new Color(1f, 0.9f, 0.5f, 0.9f);
        tracer.endColor = new Color(1f, 0.65f, 0.2f, 0.3f);
        tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tracer.receiveShadows = false;
        tracer.enabled = false;
    }

    public override void Teardown()
    {
        firing = aiming = false;
        if (avatar.CameraRig != null) avatar.CameraRig.Sight = null;
        ReleaseTracer();
        base.Teardown();
    }

    void OnDestroy() => ReleaseTracer();

    void ReleaseTracer()
    {
        if (tracer != null) Destroy(tracer.gameObject);
        if (tracerMaterial != null) Destroy(tracerMaterial);
        tracer = null;
        tracerMaterial = null;
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
        tracerTimer -= dt;
        if (tracer != null && tracerTimer <= 0f) tracer.enabled = false;
        fireTimer = Mathf.Max(0f, fireTimer - dt);
        // Only the V key reaches the base attack buffer, as the stock combo.
        var stockInput = input;
        stockInput.attack = stockInput.attackHeld = false;
        stockInput.alt = input.melee;
        base.TickAttack(stockInput, dt);
        firing = input.attackHeld && !swinging;
        // Right click looks through the sights; a stock swing or a jump shot sequence ends it.
        aiming = input.altHeld && avatar.CameraRig != null && !swinging && !jumpQueued && jumpShotsLeft == 0;
        if (avatar.CameraRig != null) avatar.CameraRig.Sight = aiming ? sightPoint : null;
        if (!firing || dt <= 0f) return;
        aimPoint = FindAimPoint(!aiming && weapon.rifleAimAssist);
        if (!aiming) FaceYaw(YawTo(aimPoint), AimTurnSpeed, dt);
        if (fireTimer > 0f) return;
        fireTimer = Mathf.Max(0.02f, weapon.fireInterval);
        Fire();
    }

    protected override void Locomote(AvatarInput input, float dt)
    {
        hopTimer = Mathf.Max(0f, hopTimer - dt);
        squashTimer = Mathf.Max(0f, squashTimer - dt);
        pitchKick = Mathf.MoveTowards(pitchKick, 0f, KickDecay * dt);
        bool grounded = avatar.IsGrounded;
        if (grounded && !wasGrounded) squashTimer = LandSquashTime;
        wasGrounded = grounded;

        if (swinging)
        {
            UpdateFacing(input.wish, dt);
            Accelerate(input.wish * (AirSpeed * weapon.swingMoveScale), GroundDrag, dt);
            RelaxMotion(dt);
            return;
        }

        if (aiming)
        {
            // The view follows the mouse exactly; the rifle turns with it so the sight line stays on the crosshair.
            transform.rotation = Quaternion.Euler(0f, avatar.CameraRig.Yaw, 0f);
            Accelerate(input.wish * (BounceSpeed * FireMoveScale), FireGlideAcceleration, dt);
            PivotPose(avatar.CameraRig.Pitch + sightTilt + pitchKick, weapon.rifleSightRear * weapon.heldScale,
                Vector3.one, dt, SightHeight, SightPoseSharpness);
            return;
        }

        if (!firing) UpdateFacing(input.wish, dt);
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

        if (firing)
            Accelerate(input.wish * (BounceSpeed * FireMoveScale), FireGlideAcceleration, dt);
        else if (!grounded)
            Accelerate(input.wish * BounceSpeed, AirAcceleration, dt);
        else if (moving && !jumpShooting && avatar.VerticalVelocity <= 0f && hopTimer <= 0f && squashTimer <= 0f)
        {
            // Stock bounce: spring off the ground once the landing squash has played.
            avatar.Hop(HopHeight);
            avatar.PlanarVelocity = input.wish * BounceSpeed;
            hopTimer = HopInterval;
        }
        else
            Accelerate(Vector3.zero, GroundDrag, dt);

        if (firing)
            EaseMotion(Vector3.zero, Quaternion.Euler(pitchKick, 0f, 0f), Vector3.one, 35f, dt);
        else if (jumpShooting)
            PivotPose(MuzzleDownPitch, weapon.rifleMuzzle * weapon.heldScale, Vector3.one, dt);
        else
        {
            float pitch = !grounded && avatar.VerticalVelocity > 0f ? weapon.rifleStandPitch + 14f : weapon.rifleStandPitch;
            Vector3 scale = grounded && squashTimer > 0f
                ? new Vector3(1f / Mathf.Sqrt(LandSquash), 1f / Mathf.Sqrt(LandSquash), LandSquash)
                : Vector3.one;
            Vector3 standPivot = weapon.rifleStandPivot != Vector3.zero ? weapon.rifleStandPivot : weapon.bladeTip;
            PivotPose(pitch, standPivot * weapon.heldScale, scale, dt);
        }
    }

    // Tilts the rifle by pitch with the given point (Motion space) held `height` above the avatar's origin
    // (0 = resting on the ground). Motion's z axis is the rifle's length, so scale.z squashes it along the barrel.
    void PivotPose(float pitch, Vector3 pivot, Vector3 scale, float dt, float height = 0f, float sharpness = 35f)
    {
        var rotation = Quaternion.Euler(pitch, 0f, 0f);
        var anchor = new Vector3(0f, height - weapon.restPosition.y, 0f);
        EaseMotion(anchor - rotation * Vector3.Scale(scale, pivot), rotation, scale, sharpness, dt);
    }

    Vector3 Muzzle => visual.TransformPoint(weapon.rifleMuzzle);

    Vector3 FindAimPoint(bool assist)
    {
        var target = assist ? FindAssistTarget() : null;
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

    void Fire()
    {
        Vector3 muzzle = Muzzle;
        Vector3 direction = aimPoint - muzzle;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        Vector3 end = muzzle + direction * weapon.fireRange;
        if (FirstHit(muzzle, direction, weapon.fireRange, out var hit))
        {
            end = hit.point;
            var enemy = hit.collider.GetComponentInParent<Enemy>();
            if (enemy != null && !enemy.IsDead)
            {
                var info = new HitInfo
                {
                    damage = weapon.damage,
                    direction = direction,
                    point = hit.point,
                    knockback = weapon.knockback * ShotKnockbackScale,
                    stagger = ShotStagger,
                    heavy = weapon.rifleHeavyHit,
                    tick = false,
                };
                enemy.TakeHit(info);
                avatar.RecordHit(info.damage, enemy.IsDead);
                if (HitFeedback.Instance != null)
                {
                    if (weapon.rifleHeavyHit) HitFeedback.Instance.PlayHit(info, weapon, HeavyShot, enemy.IsDead);
                    else HitFeedback.Instance.PlayTick(info);
                }
            }
        }
        if (HitFeedback.Instance != null) HitFeedback.Instance.PlayNailShot(muzzle, direction, false);
        ShowTracer(muzzle, end);
        avatar.Lunge(Vector3.ProjectOnPlane(-direction, Vector3.up), ShotRecoil, ShotRecoilTime);
        if (avatar.CameraRig != null)
        {
            if (weapon.rifleHeavyHit) avatar.CameraRig.Shake(weapon.shake, 0.1f);
            else avatar.CameraRig.Shake(0.035f, 0.06f);
        }
        pitchKick = -ShotPitch;
    }

    void PropulsionShot(Vector3 direction)
    {
        Vector3 muzzle = Muzzle;
        if (HitFeedback.Instance != null) HitFeedback.Instance.PlayNailShot(muzzle, direction, false);
        Vector3 end = FirstHit(muzzle, direction, 3f, out var hit) ? hit.point : muzzle + direction * 3f;
        ShowTracer(muzzle, end);
    }

    void ShowTracer(Vector3 start, Vector3 end)
    {
        if (tracer == null) return;
        tracer.SetPosition(0, start);
        tracer.SetPosition(1, end);
        tracer.enabled = true;
        tracerTimer = TracerLife;
    }
}

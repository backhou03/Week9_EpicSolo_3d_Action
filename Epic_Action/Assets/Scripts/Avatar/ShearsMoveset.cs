using System.Collections.Generic;
using UnityEngine;

// The shears walk on their two blades like crossing legs. Jump: the jaw snaps shut
// (a crouch), then springs wide open. Left click slashes with the jaw held open, right click snips.
public class ShearsMoveset : AvatarMoveset
{
    const float MoveSpeed = 4.5f;
    const float Acceleration = 40f;
    const float JumpHeight = 2f;
    const float CrouchTime = 0.08f;
    const float CrouchDip = 0.1f;
    const float StrideAngle = 30f;
    const float AirJaw = 55f;
    const float JawSpeed = 900f;
    // Enemies the blades overlapped this recently when the jaw snaps shut count as caught between them.
    const float SnapCatchWindow = 0.3f;

    readonly List<Enemy> caught = new List<Enemy>();
    ShearsDriver shears;
    float legLength;
    // Each step travels one blade length; two steps complete a cycle.
    float stridePhase;
    float jawFrom;
    float crouchTimer;
    bool jumpQueued;
    bool snipResolved;

    public override void Configure(WeaponAvatar owner, WeaponData data, Transform weaponVisual)
    {
        legLength = Mathf.Max(0.3f, (data.bladeTip - data.bladeBase).magnitude * data.heldScale);
        shears = weaponVisual.gameObject.AddComponent<ShearsDriver>();
        shears.Configure(data.jawClosed, data.restJaw);
        shears.Snapped += OnShearsSnapped;
        base.Configure(owner, data, weaponVisual);
    }

    public override void Teardown()
    {
        shears.Snapped -= OnShearsSnapped;
        base.Teardown();
    }

    // Each blade rides on its own half; the cut plane is the plane the blades lie in.
    protected override void BuildHitbox()
    {
        hitbox = visual.gameObject.AddComponent<WeaponHitbox>();
        hitbox.Configure(weapon.bladeBase, weapon.bladeTip, weapon.hitRadius, weapon.tickInterval);
        hitbox.Hit += OnBladeHit;
        hitbox.ClearBlades();
        hitbox.AddBlade(shears.HalfA, weapon.bladeBase, weapon.bladeTip);
        if (weapon.mirrorBlade) hitbox.AddBlade(shears.HalfB, MirrorX(weapon.bladeBase), MirrorX(weapon.bladeTip));
        hitbox.CutPlane = visual;
        trail = visual.gameObject.AddComponent<BladeTrail>();
        trail.Configure(shears.HalfA, weapon.bladeTip, shears.HalfB, MirrorX(weapon.bladeTip), weapon.trailColor);
    }

    static Vector3 MirrorX(Vector3 v) => new Vector3(-v.x, v.y, v.z);

    protected override void StartSwing(bool alt)
    {
        jumpQueued = false;
        snipResolved = false;
        jawFrom = shears.Jaw;
        shears.Jaw = jawFrom;
        base.StartSwing(alt);
        if (!alt) shears.Jaw = currentSwing.jawWindup;
    }

    // Snips skip the lunge and the trail; they deal damage only when the jaws close.
    protected override void OnStrikeBegan()
    {
        if (!currentAlt)
        {
            base.OnStrikeBegan();
            return;
        }
        hitbox.SetActive(true);
    }

    protected override void OnStrikeEnded()
    {
        // Resolve the close even when a frame steps past the final strike pose.
        if (currentAlt)
        {
            shears.Jaw = weapon.jawClosed;
            OnShearsSnapped();
        }
        base.OnStrikeEnded();
    }

    protected override void OnBladeHit(HitContact contact)
    {
        if (!currentAlt) HandleHit(contact);
    }

    protected override bool SlicesNow(HitContact contact) => currentAlt || base.SlicesNow(contact);

    // The blades just met: whatever they overlapped during this swing is cut along the plane they lie in.
    void OnShearsSnapped()
    {
        if (!swinging || !currentAlt || snipResolved) return;
        snipResolved = true;

        hitbox.GetRecentlyTouched(SnapCatchWindow, caught);
        foreach (var enemy in caught)
        {
            HandleHit(new HitContact
            {
                enemy = enemy,
                point = enemy.Center,
                cutNormal = visual.forward,
                firstContact = true,
                bladeSpeed = float.MaxValue,
            });
        }
        caught.Clear();
        HitFeedback.Instance.PlaySnip(visual.TransformPoint(weapon.bladeTip));
    }

    protected override void Locomote(AvatarInput input, float dt)
    {
        UpdateFacing(input.wish, dt);

        if (swinging)
        {
            UpdateJaw();
            Accelerate(input.wish * (MoveSpeed * weapon.swingMoveScale), Acceleration, dt);
            RelaxMotion(dt);
            return;
        }

        Accelerate(input.wish * MoveSpeed, Acceleration, dt);
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

    // Slashes stay open for the entire attack; snips open, close, then relax.
    void UpdateJaw()
    {
        if (!currentAlt)
        {
            shears.Jaw = currentSwing.jawWindup;
            return;
        }

        float jaw = weapon.restJaw;
        float u = swinger.PhaseProgress;
        switch (swinger.Phase)
        {
            case SwingPhase.Windup: jaw = Mathf.LerpUnclamped(jawFrom, currentSwing.jawWindup, currentSwing.windupCurve.Evaluate(u)); break;
            case SwingPhase.Strike: jaw = Mathf.LerpUnclamped(currentSwing.jawWindup, currentSwing.jawStrike, currentSwing.strikeCurve.Evaluate(u)); break;
            case SwingPhase.Recovery: jaw = Mathf.LerpUnclamped(currentSwing.jawStrike, weapon.restJaw, currentSwing.recoveryCurve.Evaluate(u)); break;
        }
        shears.Jaw = jaw;
    }
}

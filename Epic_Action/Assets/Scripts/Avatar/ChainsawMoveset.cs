using UnityEngine;

// The chain rides on its edge and drives the saw forward; its weight makes steering drift.
public class ChainsawMoveset : AvatarMoveset
{
    const float AttackBuffer = 0.4f;
    const float MoveSpeed = 11f;
    const float Acceleration = 20f;
    const float TurnScale = 0.45f;
    const float JumpHeight = 2f;
    const float CrouchDuration = 0.09f;
    const float CrouchDepth = 0.12f;
    const float CrouchPitch = 18f;
    const float KickReleaseSharpness = 40f;
    const float LandingBounceHeight = 0.2f;
    const float RisingHeight = 1.5f;
    const float SlamHeight = 2.5f;
    const float SlamSpeed = 24f;
    const float ChainsawPush = 0.12f;
    const float JitterDistance = 0.008f;
    const float JitterDegrees = 1.2f;
    const float PitchKickDegrees = 5f;
    const float PitchSharpness = 10f;

    ChainsawDriver chainsaw;
    float pitchKick;
    enum BouncePhase { None, Crouch, Flip, Rebound }
    BouncePhase bounce;
    float bounceTime, flightDuration;
    bool bounceAirborne, slamAirborne, slamDriving;
    bool airAttackUsed, bufferedAirAttack;
    float bufferedAttackTime = -10f;

    bool IsComboHit(int index) => !currentAlt && weapon.combo.Count > index && currentSwing == weapon.combo[index];

    public override void Configure(WeaponAvatar owner, WeaponData data, Transform weaponVisual)
    {
        base.Configure(owner, data, weaponVisual);
        chainsaw = weaponVisual.gameObject.AddComponent<ChainsawDriver>();
    }

    protected override void StartSwing(bool alt)
    {
        bounce = BouncePhase.None;
        slamAirborne = false;
        slamDriving = false;
        base.StartSwing(alt);
        if (alt)
        {
            airAttackUsed = true;
            avatar.Motion.localPosition = Vector3.zero;
            avatar.Motion.localRotation = Quaternion.identity;
            avatar.Motion.localScale = Vector3.one;
        }
        if (IsComboHit(3)) avatar.Hop(SlamHeight, true);
    }

    protected override void OnStrikeBegan()
    {
        base.OnStrikeBegan();
        if (IsComboHit(2)) avatar.Hop(RisingHeight, true);
        if (IsComboHit(3))
        {
            slamDriving = true;
            avatar.VerticalVelocity = -SlamSpeed;
        }
    }

    protected override void TickAttack(AvatarInput input, float dt)
    {
        if (avatar.IsGrounded)
        {
            airAttackUsed = false;
            if (bufferedAirAttack) bufferedAttackTime = -10f;
        }
        // Only left clicks queue attacks; retain whether the press happened in the air.
        if (input.attack)
        {
            bufferedAirAttack = !avatar.IsGrounded;
            bufferedAttackTime = bufferedAirAttack && airAttackUsed ? -10f : Time.unscaledTime;
        }
        bool wantAttack = Time.unscaledTime - bufferedAttackTime <= AttackBuffer;
        bool canStart = !swinging
            || (swinger.Phase == SwingPhase.Recovery && swinger.PhaseProgress >= currentSwing.chainAt);
        bool validAttack = bufferedAirAttack
            ? !avatar.IsGrounded && !airAttackUsed && weapon.altCombo.Count > 0
            : avatar.IsGrounded && weapon.combo.Count > 0;
        if (wantAttack && canStart && validAttack)
        {
            bufferedAttackTime = -10f;
            StartSwing(bufferedAirAttack);
        }
        swinger.Holding = swinging && weapon.sliceDelay > 0f && swinger.Phase == SwingPhase.Strike
            && hitbox.Touching;
    }

    // A click grinds like a hold: even a fast spin must saw for the full delay.
    protected override bool SlicesNow(HitContact contact) => false;

    protected override void OnEnemyHit(Enemy enemy, bool tick, Vector3 direction)
    {
        chainsaw.NotifyContact();
        swinger.Holding = swinging && weapon.sliceDelay > 0f && swinger.Phase == SwingPhase.Strike;
        if (tick && !enemy.IsDead) avatar.Lunge(direction, ChainsawPush, weapon.tickInterval);
    }

    protected override void Locomote(AvatarInput input, float dt)
    {
        // Holding freezes the body spin as well as the blade pose.
        if (!swinger.Holding) UpdateFacing(input.wish, dt, swinging ? 1f : TurnScale);

        if (swinging && IsComboHit(3) && !avatar.IsGrounded) slamAirborne = true;
        if (slamDriving && slamAirborne && avatar.IsGrounded)
        {
            slamDriving = false;
            if (avatar.CameraRig != null) avatar.CameraRig.Shake(weapon.shake * 1.8f, 0.22f);
        }
        // WeaponAvatar applies gravity after this tick; cancel that step while sawing in place.
        if (swinger.Holding) avatar.VerticalVelocity = -avatar.Gravity * dt;
        else if (slamDriving && !avatar.IsGrounded)
            avatar.VerticalVelocity = Mathf.Min(avatar.VerticalVelocity, -SlamSpeed);

        float forwardSpeed = Vector3.Dot(avatar.PlanarVelocity, transform.forward);
        float scale = swinging ? weapon.swingMoveScale : 1f;
        Accelerate(transform.forward * (MoveSpeed * scale * input.wish.magnitude), Acceleration, dt);
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
            if (IsComboHit(0)) KickMotion(dt);
            else RelaxMotion(dt);
            return;
        }

        if (input.jump && avatar.IsGrounded && bounce == BouncePhase.None)
        {
            bounce = BouncePhase.Crouch;
            bounceTime = 0f;
        }
        if (TickBounce(dt)) return;

        float shake = Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(groundSpeed / MoveSpeed));
        // Motion sits below Pose, so pitch about the avatar's right axis in the edge rest pose.
        Vector3 pitchAxis = Quaternion.Inverse(Quaternion.Euler(weapon.restEuler)) * Vector3.right;
        EaseMotion(Random.insideUnitSphere * (JitterDistance * shake),
            Quaternion.AngleAxis(-pitchKick, pitchAxis)
                * Quaternion.Euler(Random.insideUnitSphere * (JitterDegrees * shake)), Vector3.one, 60f, dt);
    }

    // First ground hit: sink and tilt during the windup, then spring back as the leap starts.
    void KickMotion(float dt)
    {
        if (swinger.Phase != SwingPhase.Windup)
        {
            EaseMotion(Vector3.zero, Quaternion.identity, Vector3.one, KickReleaseSharpness, dt);
            return;
        }
        // Motion follows the windup pose, so express world down and the avatar's right in Pose space.
        Transform pose = avatar.Motion.parent;
        float u = swinger.PhaseProgress;
        EaseMotion(pose.InverseTransformDirection(Vector3.down) * (CrouchDepth * u),
            Quaternion.AngleAxis(CrouchPitch * u, pose.InverseTransformDirection(transform.right)),
            Vector3.one, 30f, dt);
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

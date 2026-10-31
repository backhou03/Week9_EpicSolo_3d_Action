using UnityEngine;

// The ruler tumbles end over end: it falls forward about the end on the ground, lands flat, then rises
// about the far end until it stands upside down one length further on. Jump: it bends, then springs up.
public class RulerMoveset : AvatarMoveset
{
    const float MoveSpeed = 5f;
    const float Acceleration = 40f;
    const float JumpHeight = 2.2f;
    const float SquashTime = 0.08f;

    float length;
    // Steps tumbled; the fractional part is the progress of the current step.
    float tumble;
    float squashTimer;
    bool jumpQueued;

    public override void Configure(WeaponAvatar owner, WeaponData data, Transform weaponVisual)
    {
        base.Configure(owner, data, weaponVisual);
        length = Mathf.Max(0.3f, (data.bladeTip - data.bladeBase).magnitude * data.heldScale);
    }

    protected override void StartSwing(bool alt)
    {
        tumble = 0f;
        base.StartSwing(alt);
    }

    protected override void Locomote(AvatarInput input, float dt)
    {
        UpdateFacing(input.wish, dt);

        if (swinging)
        {
            Accelerate(input.wish * (MoveSpeed * MoveSpeedScale * weapon.swingMoveScale), Acceleration, dt);
            RelaxMotion(dt);
            return;
        }

        UpdateJump(input.jump, dt);

        // A started step always finishes, so the ruler never stops leaning on one end.
        float step = tumble - Mathf.Floor(tumble);
        bool moving = input.wish.sqrMagnitude > 0.01f || step > 0.001f;
        float speed = moving ? MoveSpeed * MoveSpeedScale * Mathf.Max(input.wish.magnitude, 0.6f) : 0f;
        Accelerate(transform.forward * speed, Acceleration, dt);

        if (avatar.IsGrounded && moving)
        {
            float next = tumble + avatar.PlanarVelocity.magnitude * dt / length;
            // Stop exactly on a step boundary when there is no more input.
            if (input.wish.sqrMagnitude <= 0.01f && Mathf.Floor(next) > Mathf.Floor(tumble)) next = Mathf.Floor(next);
            tumble = next;
        }

        if (squashTimer > 0f) return;
        TumblePose(out Vector3 position, out Quaternion rotation);
        EaseMotion(position, rotation, Vector3.one, 40f, dt);
    }

    void UpdateJump(bool pressed, float dt)
    {
        if (pressed && avatar.IsGrounded && !jumpQueued)
        {
            jumpQueued = true;
            squashTimer = SquashTime;
        }
        if (!jumpQueued) return;

        squashTimer -= dt;
        if (squashTimer > 0f)
        {
            // Bend: squashed down before the spring.
            EaseMotion(avatar.Motion.localPosition, avatar.Motion.localRotation, new Vector3(1.25f, 0.65f, 1.25f), 30f, dt);
            return;
        }
        jumpQueued = false;
        avatar.Hop(JumpHeight);
        avatar.Motion.localScale = new Vector3(0.8f, 1.35f, 0.8f);
    }

    // Motion offset (in Pose space) for the current tumble phase.
    void TumblePose(out Vector3 position, out Quaternion rotation)
    {
        int k = Mathf.FloorToInt(tumble);
        float f = tumble - k;
        float angle = 180f * f;
        Vector3 axis = Quaternion.Euler(angle, 0f, 0f) * Vector3.up;

        // The ground end slides back as the body moves forward, so it stays put in the world.
        Vector3 groundEnd = new Vector3(0f, 0f, -length * f);
        Vector3 nearEnd = angle <= 90f ? groundEnd : groundEnd + new Vector3(0f, 0f, length) - axis * length;
        bool flipped = (k & 1) == 1;
        position = flipped ? nearEnd + axis * length : nearEnd;
        rotation = Quaternion.Euler(angle + (flipped ? 180f : 0f), 0f, 0f);
    }
}

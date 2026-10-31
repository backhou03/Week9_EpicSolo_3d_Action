using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] InputActionAsset actions;
    [SerializeField] ThirdPersonCamera cameraRig;
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float acceleration = 45f;
    [SerializeField] float turnSpeed = 720f;
    [SerializeField] float attackTurnSpeed = 2400f;
    [SerializeField] float jumpHeight = 1.3f;
    [SerializeField] float gravity = -25f;

    CharacterController cc;
    InputAction move, jump;
    Vector3 planarVelocity;
    float verticalVelocity;

    Vector3 lungeVelocity;
    float lungeTimeLeft;

    bool hasFacingOverride;
    float facingYaw;

    // Drag toward the weapon's centre of mass: a velocity that fades out once it is no longer refreshed.
    Vector3 weightDrift;
    Quaternion leanTarget = Quaternion.identity;
    Quaternion lean = Quaternion.identity;
    Transform body;

    public bool CombatLocked { get; set; }

    public void MoveCombat(Vector3 displacement) => cc.Move(displacement);

    public float SpeedScale { get; set; } = 1f;
    public float TurnScale { get; set; } = 1f;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        body = transform.Find("Body");
    }

    void OnEnable()
    {
        var map = actions.FindActionMap("Player", true);
        move = map.FindAction("Move", true);
        jump = map.FindAction("Jump", true);
        map.Enable();
    }

    // Moves the player `distance` metres along `direction` over `duration` seconds.
    public void Lunge(Vector3 direction, float distance, float duration)
    {
        if (duration <= 0f || distance <= 0f) return;
        // A weaker push (chainsaw tick) must not cut a stronger lunge short.
        float speed = distance / duration;
        if (lungeTimeLeft > 0f && lungeVelocity.magnitude > speed) return;
        lungeVelocity = direction.normalized * speed;
        lungeTimeLeft = duration;
    }

    public void FaceYaw(float yaw)
    {
        hasFacingOverride = true;
        facingYaw = yaw;
    }

    public void ReleaseFacing() => hasFacingOverride = false;

    // Hops up to `height` metres (only from the ground).
    public void Hop(float height)
    {
        if (cc.isGrounded) verticalVelocity = Mathf.Sqrt(height * -2f * gravity);
    }

    // Drags the player along `drift` (m/s) and tilts the body `degrees` toward `leanDirection`. Call every
    // frame while the weight is acting; afterwards the drift fades out and the lean straightens up.
    public void SetWeight(Vector3 drift, Vector3 leanDirection, float degrees)
    {
        if (drift.sqrMagnitude > 0f) weightDrift = drift;
        leanTarget = degrees > 0f && leanDirection.sqrMagnitude > 0.0001f
            ? Quaternion.AngleAxis(degrees, Vector3.Cross(Vector3.up, leanDirection.normalized))
            : Quaternion.identity;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (CombatLocked)
        {
            planarVelocity = Vector3.zero;
            lungeVelocity = Vector3.zero;
            lungeTimeLeft = 0f;
            weightDrift = Vector3.zero;
            Rotate(Vector3.zero, dt);
            UpdateLean(dt);
            return;
        }

        Vector2 input = move.ReadValue<Vector2>();
        Vector3 wish = Quaternion.Euler(0f, cameraRig.Yaw, 0f) * new Vector3(input.x, 0f, input.y);
        wish = Vector3.ClampMagnitude(wish, 1f);

        planarVelocity = Vector3.MoveTowards(planarVelocity, wish * (moveSpeed * SpeedScale), acceleration * dt);

        if (cc.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        if (jump.WasPressedThisFrame() && cc.isGrounded) verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        verticalVelocity += gravity * dt;

        Vector3 velocity = planarVelocity + Vector3.up * verticalVelocity;
        if (lungeTimeLeft > 0f)
        {
            velocity += lungeVelocity;
            lungeTimeLeft -= dt;
        }
        velocity += weightDrift;
        weightDrift *= Mathf.Exp(-8f * dt);
        cc.Move(velocity * dt);

        Rotate(wish, dt);
        UpdateLean(dt);
    }

    // Tilts the body around the feet, not around its own centre.
    void UpdateLean(float dt)
    {
        if (body == null) return;
        lean = Quaternion.Slerp(lean, leanTarget, 1f - Mathf.Exp(-14f * dt));
        // `lean` is a world-space tilt; the body sits under the yawing player.
        Quaternion local = Quaternion.Inverse(transform.rotation) * lean * transform.rotation;
        body.localRotation = local;
        body.localPosition = local * Vector3.up;
    }

    void Rotate(Vector3 wish, float dt)
    {
        if (hasFacingOverride)
        {
            Quaternion face = Quaternion.Euler(0f, facingYaw, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, face, attackTurnSpeed * TurnScale * dt);
        }
        else if (wish.sqrMagnitude > 0.01f)
        {
            Quaternion face = Quaternion.LookRotation(wish);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, face, turnSpeed * TurnScale * dt);
        }
    }
}

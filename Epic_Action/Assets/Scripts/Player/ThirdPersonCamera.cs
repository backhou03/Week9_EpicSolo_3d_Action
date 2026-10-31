using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [SerializeField] InputActionAsset actions;
    [SerializeField] Transform target;
    [SerializeField] Vector3 pivotOffset = new Vector3(0.7f, 1.55f, 0f);
    [SerializeField] float distance = 4.2f;
    [SerializeField] float mouseSensitivity = 0.12f;
    [SerializeField] float stickSpeed = 180f;
    [SerializeField] Vector2 pitchLimits = new Vector2(-20f, 60f);
    [SerializeField] float baseFov = 70f;
    [SerializeField] float zoomFov = 40f;
    [SerializeField] float zoomSharpness = 14f;
    [Tooltip("How far behind the sight point the eye sits while looking through it.")]
    [SerializeField] float sightEyeRelief = 0.1f;
    [SerializeField] float sightNearClip = 0.02f;
    [SerializeField] float collisionRadius = 0.25f;
    [SerializeField] float followSharpness = 22f;

    public float Yaw => yaw;
    public float Pitch => pitch;
    // While set, the eye moves to this point and the FOV narrows to zoomFov.
    public Transform Sight { get; set; }

    Camera cam;
    InputAction look;
    float yaw, pitch = 15f;
    Vector3 smoothedPivot;
    float currentDistance;
    readonly RaycastHit[] hits = new RaycastHit[8];

    float shakeMagnitude, shakeEnd, shakeDuration = 1f;
    float fovKick, fovKickEnd, fovKickDuration = 1f;
    float zoomBlend, defaultNearClip;
    Transform lastSight;
    float framingScale = 1f;
    Transform framingVisual;
    Renderer[] framingRenderers;

    // Temporary large forms opt in; all other weapons keep the serialized framing.
    public void SetFramingScale(float scale, Transform weaponVisual = null)
    {
        framingScale = Mathf.Max(1f, scale);
        if (framingScale <= 1f) weaponVisual = null;
        if (framingVisual == weaponVisual) return;
        framingVisual = weaponVisual;
        framingRenderers = weaponVisual != null ? weaponVisual.GetComponentsInChildren<Renderer>() : null;
    }

    void Awake()
    {
        cam = GetComponent<Camera>();
        defaultNearClip = cam.nearClipPlane;
        currentDistance = distance;
    }

    void OnEnable()
    {
        var map = actions.FindActionMap("Player", true);
        look = map.FindAction("Look", true);
        map.Enable();
        LockCursor(true);
        yaw = target != null ? target.eulerAngles.y : 0f;
        if (target != null) smoothedPivot = target.position;
    }

    void OnDisable() => LockCursor(false);

    // Positive amount pushes the FOV in (zoom), negative widens it; eases back out.
    public void FovPunch(float amount, float duration)
    {
        fovKick = amount;
        fovKickDuration = Mathf.Max(duration, 0.01f);
        fovKickEnd = Time.unscaledTime + fovKickDuration;
    }

    public void Shake(float magnitude, float duration)
    {
        float remaining = Mathf.Max(0f, shakeEnd - Time.unscaledTime) / shakeDuration;
        if (magnitude < shakeMagnitude * remaining * remaining) return;
        shakeMagnitude = magnitude;
        shakeDuration = Mathf.Max(duration, 0.01f);
        shakeEnd = Time.unscaledTime + shakeDuration;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) LockCursor(false);
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);

        if (Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 delta = look.ReadValue<Vector2>();
        bool stick = look.activeControl != null && look.activeControl.device is Gamepad;
        float scale = stick ? stickSpeed * Time.unscaledDeltaTime : mouseSensitivity;
        yaw += delta.x * scale;
        pitch = Mathf.Clamp(pitch - delta.y * scale, pitchLimits.x, pitchLimits.y);
    }

    void LateUpdate()
    {
        if (target == null) return;

        float dt = Time.unscaledDeltaTime;
        smoothedPivot = Vector3.Lerp(smoothedPivot, target.position, 1f - Mathf.Exp(-followSharpness * dt));

        Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = smoothedPivot + yawRot * (pivotOffset * framingScale);
        Vector3 back = rot * Vector3.back;

        float desiredDistance = distance * framingScale;
        float allowed = CollisionDistance(pivot, back, desiredDistance);
        currentDistance = allowed < currentDistance ? allowed : Mathf.Lerp(currentDistance, allowed, 1f - Mathf.Exp(-8f * dt));

        // A wall may push the eye into a large weapon. Recast from above its bounds.
        if (TryFramingBounds(out Bounds bounds))
        {
            float clearance = collisionRadius + cam.nearClipPlane;
            bounds.Expand(clearance * 2f);
            if (bounds.Contains(pivot + back * currentDistance))
            {
                pivot.y = Mathf.Max(pivot.y, bounds.max.y + clearance + Mathf.Max(0f, -back.y) * desiredDistance);
                currentDistance = CollisionDistance(pivot, back, desiredDistance);
            }
        }

        transform.position = pivot + back * currentDistance;
        transform.rotation = rot;

        ApplySight();
        ApplyShake();
        ApplyFov();
    }

    float CollisionDistance(Vector3 pivot, Vector3 back, float desired)
    {
        float allowed = desired;
        int n = Physics.SphereCastNonAlloc(pivot, collisionRadius, back, hits, desired, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider;
            if (c.transform.IsChildOf(target) || c.GetComponentInParent<Enemy>() != null) continue;
            allowed = Mathf.Min(allowed, hits[i].distance);
        }
        return Mathf.Max(allowed, 0.6f);
    }

    bool TryFramingBounds(out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        if (framingVisual == null || framingRenderers == null) return false;
        foreach (var r in framingRenderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (!found) bounds = r.bounds;
            else bounds.Encapsulate(r.bounds);
            found = true;
        }
        return found;
    }

    // Blends the eye from the third-person position to just behind the sight point; the view direction stays on the mouse.
    void ApplySight()
    {
        if (Sight != null) lastSight = Sight;
        // A destroyed sight (weapon switched) ends the view at once.
        if (lastSight == null) zoomBlend = 0f;
        else zoomBlend = Mathf.Lerp(zoomBlend, Sight != null ? 1f : 0f, 1f - Mathf.Exp(-zoomSharpness * Time.unscaledDeltaTime));
        cam.nearClipPlane = zoomBlend > 0.01f ? sightNearClip : defaultNearClip;
        if (lastSight == null) return;
        if (Sight == null && zoomBlend < 0.001f)
        {
            lastSight = null;
            return;
        }
        Vector3 eye = lastSight.position - transform.forward * sightEyeRelief;
        transform.position = Vector3.Lerp(transform.position, eye, zoomBlend);
    }

    void ApplyShake()
    {
        float remaining = Mathf.Clamp01((shakeEnd - Time.unscaledTime) / shakeDuration);
        if (remaining <= 0f) return;

        float amp = shakeMagnitude * remaining * remaining;
        float t = Time.unscaledTime * 38f;
        Vector3 offset = new Vector3(Mathf.PerlinNoise(t, 0.1f) - 0.5f, Mathf.PerlinNoise(0.2f, t) - 0.5f, 0f) * 2f * amp;
        transform.position += transform.rotation * offset;
        float roll = (Mathf.PerlinNoise(t, 7.7f) - 0.5f) * 2f * amp * 4f;
        transform.rotation *= Quaternion.Euler(0f, 0f, roll);
    }

    void ApplyFov()
    {
        float remaining = Mathf.Clamp01((fovKickEnd - Time.unscaledTime) / fovKickDuration);
        cam.fieldOfView = Mathf.Lerp(baseFov, zoomFov, zoomBlend) - fovKick * remaining * remaining;
    }

    static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}

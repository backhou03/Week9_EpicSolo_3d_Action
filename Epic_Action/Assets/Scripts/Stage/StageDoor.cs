using System;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class StageDoor : MonoBehaviour
{
    const float SinkDepth = 6.3f;
    const float RiseTime = 0.45f;
    const float LowerTime = 0.8f;
    const float RiseShake = 0.45f;
    const float RumbleShake = 0.08f;
    static readonly Color LockFlash = new Color(1f, 0.25f, 0.1f);
    static readonly Color OpenFlash = new Color(0.35f, 1f, 0.55f);
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    public bool IsLocked { get; private set; }
    public event Action<bool> LockChanged;
    Vector3 upPosition, downPosition;
    Renderer mesh;
    Color baseColor;
    MaterialPropertyBlock block;
    bool initialized, moving;
    float moveStart;

    void Awake()
    {
        upPosition = transform.position;
        downPosition = upPosition + Vector3.down * SinkDepth;
        mesh = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        if (mesh != null && mesh.sharedMaterial != null) baseColor = mesh.sharedMaterial.HasProperty(BaseColorId) ? mesh.sharedMaterial.GetColor(BaseColorId) : Color.white;
    }

    public void SetLocked(bool locked)
    {
        // Collider gating stays immediate; only the visual wall animates.
        bool changed = locked != IsLocked;
        IsLocked = locked;
        GetComponent<BoxCollider>().enabled = locked;
        if (!initialized)
        {
            initialized = true;
            transform.position = locked ? upPosition : downPosition;
            if (mesh != null) mesh.enabled = locked;
        }
        else if (changed)
        {
            moving = true;
            moveStart = Time.unscaledTime;
            if (mesh != null) mesh.enabled = true;
            Cue(locked);
        }
        LockChanged?.Invoke(locked);
    }

    // Slam sound, dust along the base and a camera jolt on lock; light sweep on unlock.
    void Cue(bool locked)
    {
        Vector3 floor = new Vector3(upPosition.x, 0f, upPosition.z);
        if (locked)
        {
            StageFx.Shake(floor, RiseShake, 0.4f, 55f);
            StageFx.Play(ProceduralSfx.HitHeavy, floor, 0.9f, 0.7f);
            StageFx.Flash(floor + Vector3.up * 3f, LockFlash, 5f, 18f, 0.5f);
        }
        else
        {
            StageFx.Play(ProceduralSfx.SwingHeavy, floor, 0.6f, 0.55f);
            StageFx.Flash(floor + Vector3.up * 3f, OpenFlash, 4f, 18f, 0.9f);
        }
    }

    void Update()
    {
        if (!moving) return;
        float time = locked ? RiseTime : LowerTime;
        float t = Mathf.Clamp01((Time.unscaledTime - moveStart) / time);
        float e = locked ? Back(t) : t * t * (3f - 2f * t);
        Vector3 from = locked ? downPosition : upPosition, to = locked ? upPosition : downPosition;
        Vector3 pos = Vector3.LerpUnclamped(from, to, e);
        if (!locked) pos.x += Mathf.Sin(Time.unscaledTime * 90f) * RumbleShake * (1f - t);
        transform.position = pos;
        if (mesh != null)
        {
            Color flash = locked ? LockFlash : OpenFlash;
            block.SetColor(BaseColorId, Color.Lerp(baseColor, Color.Lerp(baseColor, flash, 0.85f), 1f - t));
            block.SetColor(ColorId, block.GetColor(BaseColorId));
            mesh.SetPropertyBlock(block);
        }
        if (t < 1f) return;
        moving = false;
        if (locked) StageFx.Dust(new Vector3(upPosition.x, 0.1f, upPosition.z), new Vector3(13f, 0.1f, 0.4f));
        else if (mesh != null) mesh.enabled = false;
        if (mesh != null) mesh.SetPropertyBlock(null);
    }

    bool locked => IsLocked;

    // Ease-out with a small overshoot so the wall bounces into place.
    static float Back(float t)
    {
        const float c1 = 1.2f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}

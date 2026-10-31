using System;
using System.Collections.Generic;
using UnityEngine;

public struct HitContact
{
    public Enemy enemy;
    public Vector3 point;
    // Normal of the plane spanned by the blade axis and the blade's sweep direction.
    public Vector3 cutNormal;
    // False for continuous-cut ticks on an enemy already hit in this swing.
    public bool firstContact;
    // Speed (m/s) of the blade at the contact point.
    public float bladeSpeed;
}

public class WeaponHitbox : MonoBehaviour
{
    // One blade segment, given in the local space of `space` (the weapon itself, or a moving part of it).
    class Blade
    {
        public Transform space;
        public Vector3 baseLocal, tipLocal;
        public Vector3 prevBase, prevTip;
        public Vector3 cutNormal = Vector3.up;
    }

    public event Action<HitContact> Hit;

    // When set, the cut plane is this transform's local XY plane (shears: the plane the blades lie in)
    // instead of the plane spanned by the blade axis and its motion.
    public Transform CutPlane { get; set; }

    readonly List<Blade> blades = new List<Blade>();
    float radius = 0.2f;
    float tickInterval;
    bool active;
    float lastTouch = -10f;
    // Enemy -> time of its next allowed tick. Presence means "already hit in this swing".
    readonly Dictionary<Enemy, float> nextTick = new Dictionary<Enemy, float>();
    // Enemy -> last time a blade overlapped it during this activation.
    readonly Dictionary<Enemy, float> touchTime = new Dictionary<Enemy, float>();
    readonly Collider[] buffer = new Collider[16];

    // Replaces all blades with one on the weapon itself.
    public void Configure(Vector3 blade0, Vector3 blade1, float hitRadius, float tickIntervalSeconds)
    {
        blades.Clear();
        AddBlade(transform, blade0, blade1);
        radius = hitRadius;
        tickInterval = tickIntervalSeconds;
    }

    public void AddBlade(Transform space, Vector3 blade0, Vector3 blade1)
    {
        blades.Add(new Blade { space = space, baseLocal = blade0, tipLocal = blade1 });
    }

    public void ClearBlades() => blades.Clear();

    // Living enemies a blade overlapped within the last `window` seconds (shears: the ones caught in the jaws).
    public void GetRecentlyTouched(float window, List<Enemy> into)
    {
        foreach (var pair in touchTime)
            if (pair.Key != null && !pair.Key.IsDead && Time.time - pair.Value <= window) into.Add(pair.Key);
    }

    // True while the blade overlapped a living enemy a moment ago.
    public bool Touching => active && Time.time - lastTouch < 0.08f;

    public void SetActive(bool on)
    {
        active = on;
        if (!on) return;
        nextTick.Clear();
        touchTime.Clear();
        foreach (var blade in blades)
        {
            blade.prevBase = blade.space.TransformPoint(blade.baseLocal);
            blade.prevTip = blade.space.TransformPoint(blade.tipLocal);
        }
    }

    // Runs after WeaponSwinger.Update so the blade pose of this frame is final.
    void LateUpdate()
    {
        if (!active) return;

        float dt = Mathf.Max(Time.deltaTime, 1e-5f);
        foreach (var blade in blades) SweepBlade(blade, dt);
    }

    void SweepBlade(Blade blade, float dt)
    {
        Vector3 curBase = blade.space.TransformPoint(blade.baseLocal);
        Vector3 curTip = blade.space.TransformPoint(blade.tipLocal);
        UpdateCutNormal(blade, curBase, curTip);

        float baseSpeed = (curBase - blade.prevBase).magnitude / dt;
        float tipSpeed = (curTip - blade.prevTip).magnitude / dt;

        // Sub-step the motion so a fast swing cannot skip over an enemy between frames.
        float travel = Mathf.Max((curBase - blade.prevBase).magnitude, (curTip - blade.prevTip).magnitude);
        int steps = Mathf.Clamp(Mathf.CeilToInt(travel / (radius * 1.5f)), 1, 12);

        for (int s = 1; s <= steps; s++)
        {
            float f = s / (float)steps;
            Vector3 a = Vector3.Lerp(blade.prevBase, curBase, f);
            Vector3 b = Vector3.Lerp(blade.prevTip, curTip, f);
            int n = Physics.OverlapCapsuleNonAlloc(a, b, radius, buffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var enemy = buffer[i].GetComponentInParent<Enemy>();
                if (enemy == null || enemy.IsDead) continue;
                lastTouch = Time.time;
                touchTime[enemy] = Time.time;

                bool first = !nextTick.TryGetValue(enemy, out float due);
                if (!first && (tickInterval <= 0f || Time.time < due)) continue;

                nextTick[enemy] = Time.time + tickInterval;
                Vector3 point = ContactPoint(enemy, a, b, out float along);
                Hit?.Invoke(new HitContact
                {
                    enemy = enemy,
                    point = point,
                    cutNormal = CutPlane != null ? CutPlane.forward : blade.cutNormal,
                    firstContact = first,
                    bladeSpeed = Mathf.Lerp(baseSpeed, tipSpeed, along),
                });
            }
        }

        blade.prevBase = curBase;
        blade.prevTip = curTip;
    }

    // Keeps the last valid normal while the blade is (nearly) still, e.g. during hit-stop.
    static void UpdateCutNormal(Blade blade, Vector3 curBase, Vector3 curTip)
    {
        Vector3 axis = curTip - curBase;
        Vector3 motion = (curBase + curTip) * 0.5f - (blade.prevBase + blade.prevTip) * 0.5f;
        Vector3 n = Vector3.Cross(axis, motion);
        if (n.sqrMagnitude > 1e-8f) blade.cutNormal = n.normalized;
    }

    // `along` is where on the blade (0 = base, 1 = tip) the enemy's centre is closest.
    static Vector3 ContactPoint(Enemy enemy, Vector3 a, Vector3 b, out float along)
    {
        Vector3 c = enemy.Center;
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(c - a, ab) / ab.sqrMagnitude) : 0f;
        along = t;
        Vector3 onBlade = a + ab * t;
        Vector3 toBlade = onBlade - c;
        toBlade.y = 0f;
        Vector3 surface = c + (toBlade.sqrMagnitude > 0.0001f ? toBlade.normalized : Vector3.forward) * enemy.Radius;
        surface.y = onBlade.y;
        return surface;
    }
}

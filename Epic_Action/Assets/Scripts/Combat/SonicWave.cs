using System.Collections.Generic;
using UnityEngine;

// One expanding sound pulse. Each enemy is affected once as the front reaches it.
public class SonicWave : MonoBehaviour
{
    const int Segments = 64;
    readonly HashSet<Enemy> hit = new HashSet<Enemy>();
    readonly RaycastHit[] obstacles = new RaycastHit[32];
    LineRenderer[] rings;
    Material material;
    WeaponData weapon;
    Transform owner;
    Vector3 origin, direction;
    float distance, age, lifetime;
    bool crushing;

    public static void Emit(WeaponData data, Transform source, Vector3 muzzle, Vector3 forward)
    {
        var go = new GameObject(data.sonicCrush ? "SonicWave_Crush" : "SonicWave_Push");
        go.transform.position = muzzle;
        go.transform.rotation = Quaternion.LookRotation(forward);
        go.AddComponent<SonicWave>().Initialize(data, source, muzzle, forward);
    }

    void Initialize(WeaponData data, Transform source, Vector3 muzzle, Vector3 forward)
    {
        weapon = data;
        owner = source;
        origin = muzzle;
        direction = forward.normalized;
        crushing = data.sonicCrush;
        lifetime = data.sonicRange / Mathf.Max(1f, data.sonicSpeed) + 0.2f;
        material = new Material(Shader.Find("Sprites/Default"));
        rings = new LineRenderer[crushing ? 6 : 3];
        for (int i = 0; i < rings.Length; i++)
        {
            var ring = new GameObject("SoundRing_" + i).AddComponent<LineRenderer>();
            ring.transform.SetParent(transform, false);
            ring.sharedMaterial = material;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = Segments;
            ring.widthMultiplier = crushing ? 0.13f : 0.055f;
            rings[i] = ring;
        }
        HitFeedback.Instance?.PlaySonic(crushing);
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        age += Time.deltaTime;
        distance = Mathf.Min(weapon.sonicRange, age * weapon.sonicSpeed);
        foreach (var enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (enemy.IsDead || hit.Contains(enemy)) continue;
            Vector3 delta = enemy.Center - origin;
            float along = Vector3.Dot(delta, direction);
            if (along < 0f || along > weapon.sonicRange + enemy.Radius || along > distance + enemy.Radius) continue;
            // Sphere allowance lets the edge of the sound cone catch the enemy's body.
            float coneRadius = Mathf.Max(0.2f, along * Mathf.Tan(weapon.sonicHalfAngle * Mathf.Deg2Rad));
            Vector3 radial = delta - direction * along;
            if (radial.magnitude > coneRadius + enemy.Radius) continue;
            if (Blocked(delta)) continue;
            hit.Add(enemy);
            Vector3 push = delta; push.y = 0f;
            if (push.sqrMagnitude < 0.01f) push = direction;
            Vector3 impactPoint = enemy.Center;
            enemy.TakeHit(new HitInfo
            {
                damage = weapon.damage,
                crush = crushing,
                direction = push.normalized,
                point = impactPoint,
                knockback = weapon.knockback,
                stagger = crushing ? 0f : 0.65f,
                heavy = crushing,
            });
            // The lethal blast effect is emitted by the target after its fuse expires.
            HitFeedback.Instance?.PlaySonicImpact(impactPoint, push.normalized, false);
        }
        UpdateRings();
    }

    bool Blocked(Vector3 delta)
    {
        int count = Physics.RaycastNonAlloc(origin, delta.normalized, obstacles, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform obstacle = obstacles[i].transform;
            if (obstacle.IsChildOf(owner) || obstacle.GetComponentInParent<Enemy>() != null) continue;
            return true;
        }
        return false;
    }

    void UpdateRings()
    {
        Color color = crushing ? new Color(1f, 0.35f, 0.08f, 1f) : new Color(0.3f, 0.85f, 1f, 1f);
        for (int i = 0; i < rings.Length; i++)
        {
            float z = age * weapon.sonicSpeed - i * (crushing ? 0.55f : 0.3f);
            rings[i].enabled = z >= 0f && z <= weapon.sonicRange;
            if (!rings[i].enabled) continue;
            float radius = 0.16f + z * Mathf.Tan(weapon.sonicHalfAngle * Mathf.Deg2Rad);
            color.a = Mathf.Clamp01(1f - z / weapon.sonicRange) * 0.85f;
            rings[i].startColor = rings[i].endColor = color;
            for (int j = 0; j < Segments; j++)
            {
                float angle = j * Mathf.PI * 2f / Segments;
                rings[i].SetPosition(j, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, z));
            }
        }
    }

    void OnDestroy() { if (material != null) Destroy(material); }
}

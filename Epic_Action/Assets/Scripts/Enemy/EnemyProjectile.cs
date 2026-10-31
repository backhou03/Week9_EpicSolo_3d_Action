using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    public const float Speed = 9f;
    public const float Radius = 0.16f;
    public const float Lifetime = 4f;
    Vector3 direction;
    int damage;
    Material material;

    public static void Fire(Vector3 origin, Vector3 direction, int damage)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "EnemyProjectile";
        go.transform.position = origin;
        go.transform.localScale = Vector3.one * Radius * 2.6f;
        var collider = go.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        var shot = go.AddComponent<EnemyProjectile>();
        shot.direction = direction.normalized;
        shot.damage = damage;
        shot.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        shot.material.color = Color.Lerp(EnemyTelegraphFx.RangedCore, Color.white, 0.55f);
        go.GetComponent<Renderer>().sharedMaterial = shot.material;
        EnemyProjectileFx.Decorate(go);
        Destroy(go, Lifetime);
    }

    void Update()
    {
        float distance = Speed * Time.deltaTime;
        var hits = Physics.SphereCastAll(transform.position, Radius, direction, distance, ~0, QueryTriggerInteraction.Ignore);
        RaycastHit? nearest = null;
        foreach (var hit in hits)
        {
            if (hit.collider.GetComponentInParent<Enemy>() != null) continue;
            if (!nearest.HasValue || hit.distance < nearest.Value.distance) nearest = hit;
        }
        if (nearest.HasValue)
        {
            var hit = nearest.Value;
            var avatar = hit.collider.GetComponentInParent<WeaponAvatar>();
            if (avatar != null)
            {
                if (avatar.Invulnerable) PlayerHurtFx.Dodge();
                avatar.TakeDamage(damage, transform.position - direction);
            }
            EnemyProjectileFx.ReleaseTrail(gameObject);
            Destroy(gameObject);
            return;
        }
        transform.position += direction * distance;
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}

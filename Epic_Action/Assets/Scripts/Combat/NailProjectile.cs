using System;
using System.Collections.Generic;
using UnityEngine;

public class NailProjectile : MonoBehaviour
{
    readonly HashSet<Enemy> hitEnemies=new HashSet<Enemy>();
    Transform owner;
    Vector3 direction;
    float speed, remaining;
    const float Radius=0.16f;
    const float SpinSpeed=1440f;

    public static void Fire(WeaponData data,Transform owner,Vector3 point,Vector3 forward,Enemy first)
    {
        GameObject go=data.nailProjectilePrefab!=null?Instantiate(data.nailProjectilePrefab):new GameObject("PiercingNail");
        go.transform.SetPositionAndRotation(point,Quaternion.LookRotation(forward));
        var projectile=go.AddComponent<NailProjectile>();
        projectile.owner=owner;projectile.direction=forward.normalized;
        projectile.speed=data.nailProjectileSpeed;projectile.remaining=data.nailProjectileRange;
        // Sphere casts can miss colliders already overlapping the muzzle. A wall still blocks this shot.
        var overlaps=Physics.OverlapSphere(point,Radius,~0,QueryTriggerInteraction.Ignore);
        foreach(var collider in overlaps)
        {
            if(collider.transform.IsChildOf(owner)||collider.GetComponentInParent<Enemy>()!=null)continue;
            Destroy(go);return;
        }
        if(first!=null&&!first.IsDead)projectile.Hit(first,first.NailAimPoint(false));
        foreach(var collider in overlaps)
        {
            var enemy=collider.GetComponentInParent<Enemy>();
            if(enemy==null||enemy.IsDead||projectile.hitEnemies.Contains(enemy))continue;
            Vector3 onLine=point+projectile.direction*Vector3.Dot(enemy.Center-point,projectile.direction);
            projectile.Hit(enemy,onLine);
        }
        var trail=go.AddComponent<TrailRenderer>();
        trail.sharedMaterial=new Material(Shader.Find("Sprites/Default"));
        trail.time=0.04f;trail.startWidth=0.04f;trail.endWidth=0f;
        trail.startColor=new Color(0.85f,0.85f,0.8f,0.6f);trail.endColor=new Color(0.7f,0.7f,0.7f,0f);
        AttachSparks(go.transform,trail.sharedMaterial);
        Destroy(go,data.nailProjectileRange/Mathf.Max(1f,data.nailProjectileSpeed)+0.2f);
    }

    // Embers shed backwards while the nail flies, as if it were glowing from friction.
    static void AttachSparks(Transform parent,Material material)
    {
        var go=new GameObject("NailSparks");
        go.transform.SetParent(parent,false);
        go.transform.localRotation=Quaternion.Euler(0f,180f,0f);
        var ps=go.AddComponent<ParticleSystem>();
        ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;
        main.loop=true;main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.15f,0.3f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(1.5f,4f);
        main.startSize=new ParticleSystem.MinMaxCurve(0.025f,0.05f);
        main.startColor=new ParticleSystem.MinMaxGradient(new Color(1f,0.5f,0.1f),new Color(1f,0.9f,0.5f));
        main.gravityModifier=1.2f;main.maxParticles=64;
        var emission=ps.emission;emission.rateOverTime=0f;emission.rateOverDistance=8f;
        var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=20f;shape.radius=0.02f;
        var fade=new Gradient();
        fade.SetKeys(
            new[]{new GradientColorKey(Color.white,0f),new GradientColorKey(Color.white,1f)},
            new[]{new GradientAlphaKey(1f,0f),new GradientAlphaKey(1f,0.5f),new GradientAlphaKey(0f,1f)});
        var col=ps.colorOverLifetime;col.enabled=true;col.color=fade;
        var size=ps.sizeOverLifetime;size.enabled=true;
        size.size=new ParticleSystem.MinMaxCurve(1f,new AnimationCurve(new Keyframe(0f,1f),new Keyframe(1f,0f)));
        var r=go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial=material;r.renderMode=ParticleSystemRenderMode.Stretch;
        r.velocityScale=0.05f;r.lengthScale=2.5f;
        ps.Play();
    }

    void Update()
    {
        // Movement uses `direction`, so spinning the root around its forward axis only rolls the nail.
        transform.Rotate(0f,0f,SpinSpeed*Time.deltaTime,Space.Self);
        float distance=Mathf.Min(remaining,speed*Time.deltaTime);
        var hits=Physics.SphereCastAll(transform.position,Radius,direction,distance,~0,QueryTriggerInteraction.Ignore);
        Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
        foreach(var hit in hits)
        {
            if(hit.transform.IsChildOf(owner))continue;
            var enemy=hit.collider.GetComponentInParent<Enemy>();
            if(enemy!=null)
            {
                if(!enemy.IsDead&&!hitEnemies.Contains(enemy))
                {
                    // The hole follows the projectile's centre line, rather than its swept sphere's edge.
                    Vector3 linePoint=transform.position+direction*Vector3.Dot(enemy.Center-transform.position,direction);
                    Hit(enemy,linePoint);
                }
                continue;
            }
            transform.position+=direction*hit.distance;
            Destroy(gameObject);return;
        }
        transform.position+=direction*distance;
        remaining-=distance;
        if(remaining<=0f)Destroy(gameObject);
    }

    void Hit(Enemy enemy,Vector3 point)
    {
        hitEnemies.Add(enemy);
        enemy.TakePiercingNail(point,direction,Radius);
        HitFeedback.Instance?.PlayNailPierce(point,direction);
    }

    void OnDestroy()
    {
        var trail=GetComponent<TrailRenderer>();
        if(trail!=null&&trail.sharedMaterial!=null)Destroy(trail.sharedMaterial);
    }
}

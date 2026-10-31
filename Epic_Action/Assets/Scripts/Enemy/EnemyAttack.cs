using System;
using UnityEngine;

public enum EnemyAttackKind { Melee, Ranged }

[RequireComponent(typeof(Enemy))]
public class EnemyAttack : MonoBehaviour
{
    public const bool CancelTelegraphOnLightHit = false;
    public const float MeleeTelegraph = 0.35f;
    public const float RangedTelegraph = 0.8f;
    public const float MeleeRange = 2.3f;
    public const float MeleeHalfAngle = 55f;
    public const float RangedDistance = 9f;
    public const float RangedRange = 17f;
    public const float AttackCooldown = 1.6f;
    public const int MeleeDamage = 15;
    public const int RangedDamage = 10;
    const float TurnSpeed = 540f;

    [SerializeField] EnemyAttackKind kind;
    [SerializeField] float damageMultiplier = 1f;
    public void SetKind(EnemyAttackKind value) => kind = value;
    Enemy enemy;
    WeaponAvatar target;
    float remaining, readyAt;

    public EnemyAttackKind Kind => kind;
    public bool IsTelegraphing { get; private set; }
    public Vector3 AimDirection { get; private set; }
    public Vector3 AttackOrigin => transform.position + Vector3.up;
    public float TelegraphProgress => IsTelegraphing ? 1f - remaining / TelegraphDuration : 0f;
    float TelegraphDuration => kind == EnemyAttackKind.Melee ? MeleeTelegraph : RangedTelegraph;
    public event Action<EnemyAttack> TelegraphStarted;
    public event Action<EnemyAttack> TelegraphEnded;
    public event Action<EnemyAttack> AttackFired;
    public event Action<EnemyAttack> AttackCancelled;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        EnemyTelegraphFx.Attach(this);
    }

    void OnEnable()
    {
        enemy.HitReceived += OnHit;
        enemy.ExternalMovement = true;
        readyAt = Time.time + AttackCooldown;
    }

    void OnDisable()
    {
        enemy.HitReceived -= OnHit;
        CancelAttack();
        enemy.ExternalMovement = false;
        enemy.DesiredVelocity = Vector3.zero;
    }

    void OnHit(HitInfo hit)
    {
        if (!IsTelegraphing || hit.heavy || hit.crush || CancelTelegraphOnLightHit) CancelAttack();
        // A light hit pauses the windup while stagger/knockback resolves.
    }

    void Update()
    {
        enemy.DesiredVelocity = Vector3.zero;
        if (target == null) target = FindFirstObjectByType<WeaponAvatar>();
        if (enemy.IsDead || target == null || target.IsDead) { CancelAttack(); return; }
        if (enemy.IsImpaired) return;

        if (IsTelegraphing)
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0f) FireAttack();
            return;
        }

        Vector3 to = target.transform.position - transform.position;
        to.y = 0f;
        float distance = to.magnitude;
        if (distance < 0.01f) return;
        Vector3 direction = to / distance;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), TurnSpeed * Time.deltaTime);
        if (kind == EnemyAttackKind.Melee)
        {
            if (distance > MeleeRange - 0.3f) enemy.DesiredVelocity = direction;
        }
        else
        {
            if (distance > RangedDistance + 1f) enemy.DesiredVelocity = direction;
            else if (distance < RangedDistance - 1f) enemy.DesiredVelocity = -direction;
        }
        float range = kind == EnemyAttackKind.Melee ? MeleeRange : RangedRange;
        if (Time.time < readyAt || distance > range) return;
        if (Physics.Linecast(AttackOrigin, target.Center, out RaycastHit hit) && hit.collider.GetComponentInParent<WeaponAvatar>() != target) return;
        BeginTelegraph(direction);
    }

    public void BeginTelegraph(Vector3 direction)
    {
        AimDirection = direction.normalized;
        remaining = TelegraphDuration;
        IsTelegraphing = true;
        enemy.DesiredVelocity = Vector3.zero;
        TelegraphStarted?.Invoke(this);
    }

    void EndTelegraph()
    {
        IsTelegraphing = false;
        enemy.TelegraphColor = null;
        readyAt = Time.time + AttackCooldown;
        TelegraphEnded?.Invoke(this);
    }

    public void CancelAttack()
    {
        if (!IsTelegraphing) return;
        EndTelegraph();
        AttackCancelled?.Invoke(this);
    }

    public void FireAttack()
    {
        if (!IsTelegraphing) return;
        EndTelegraph();
        if (kind == EnemyAttackKind.Ranged) EnemyProjectile.Fire(AttackOrigin, AimDirection, Mathf.RoundToInt(RangedDamage * damageMultiplier));
        else if (target != null && !target.IsDead)
        {
            Vector3 to = target.Center - AttackOrigin;
            float height = Mathf.Abs(to.y);
            to.y = 0f;
            if (height < 1.3f && to.magnitude <= MeleeRange + 0.4f && Vector3.Angle(AimDirection, to) <= MeleeHalfAngle
                && (!Physics.Linecast(AttackOrigin, target.Center, out RaycastHit hit) || hit.collider.GetComponentInParent<WeaponAvatar>() == target))
            {
                if (target.Invulnerable) PlayerHurtFx.Dodge();
                target.TakeDamage(Mathf.RoundToInt(MeleeDamage * damageMultiplier), transform.position);
            }
        }
        AttackFired?.Invoke(this);
    }
}

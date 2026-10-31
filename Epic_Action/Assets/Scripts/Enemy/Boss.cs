using System;
using System.Collections.Generic;
using UnityEngine;

public enum BossPattern { Slam, Charge, Barrage, Summon }
public enum BossState { Recovery, Telegraph, Attacking, Transition, Defeated }

[RequireComponent(typeof(Enemy))]
public class Boss : MonoBehaviour
{
    // Approx. 48 sustained weapon DPS * 0.74 attack uptime * (240 - 3.2) seconds.
    public const int MaxHealth = 8400;
    public const string DisplayName = "파쇄자";
    public const float PhaseTwoRatio = 0.67f;
    public const float PhaseThreeRatio = 0.34f;
    public const float TransitionDuration = 1.6f;
    public const float SlamTelegraph = 1f;
    public const float ChargeTelegraph = 1.2f;
    public const float BarrageTelegraph = 0.9f;
    public const float SummonTelegraph = 1.3f;
    public const float SlamRadius = 4f;
    public const float SlamReach = 2.5f;
    public const float ChargeLength = 12f;
    public const float ChargeHalfWidth = 1.35f;
    public const float ChargeSpeed = 18f;
    public const float BarrageHalfAngle = 48f;
    public const float BarrageInterval = 0.42f;
    public const float BarrageRange = EnemyProjectile.Speed * EnemyProjectile.Lifetime;
    public const float PhaseTempoIncrement = 0.15f;
    public const float PhaseRecoveryReduction = 0.35f;
    public const float RecoveryDuration = 2.5f;
    public const float MinimumRecovery = 1.6f;
    public const int SlamDamage = 24;
    public const int ChargeDamage = 22;
    public const int ProjectileDamage = 10;
    public const int MaxSummons = 4;
    public const float SummonSeparation = 3f;
    public const float SummonRadius = 0.6f;
    const float TurnSpeed = 180f;
    const float GroundMarkerHeight = 0.08f;

    [SerializeField] Enemy summonTemplate;
    [SerializeField] Vector3 arenaCenter;
    [SerializeField] Vector2 arenaHalfSize = new Vector2(11f, 11f);
    static readonly BossPattern[] PhaseOne = { BossPattern.Slam, BossPattern.Charge, BossPattern.Barrage, BossPattern.Summon };
    static readonly BossPattern[] PhaseTwo = { BossPattern.Charge, BossPattern.Slam, BossPattern.Barrage, BossPattern.Slam, BossPattern.Summon };
    static readonly BossPattern[] PhaseThree = { BossPattern.Charge, BossPattern.Slam, BossPattern.Barrage, BossPattern.Charge, BossPattern.Slam, BossPattern.Summon, BossPattern.Barrage };
    readonly List<Enemy> summons = new List<Enemy>();
    readonly List<Vector3> summonPoints = new List<Vector3>();
    Enemy enemy;
    CharacterController controller;
    WeaponAvatar target;
    float remaining, telegraphDuration, shotTimer;
    int patternIndex, volleysLeft;
    bool chargeHit, defeated;
    Vector3 chargeEnd;

    public Enemy Health => enemy;
    public int Phase { get; private set; } = 1;
    public BossState State { get; private set; } = BossState.Recovery;
    public BossPattern Pattern { get; private set; }
    public bool IsTelegraphing => State == BossState.Telegraph;
    public bool IsInvulnerable => enemy != null && enemy.Invulnerable;
    public float TelegraphProgress => IsTelegraphing ? 1f - remaining / telegraphDuration : 0f;
    public float PhaseProgress => State == BossState.Transition ? 1f - remaining / TransitionDuration : 0f;
    public float HealthRatio => enemy == null ? 1f : (float)enemy.Hp / enemy.MaxHp;
    public Vector3 AimDirection { get; private set; } = Vector3.forward;
    public Vector3 AttackOrigin { get; private set; }
    public Vector3 ImpactPoint { get; private set; }
    public Vector3 ChargeEnd => chargeEnd;
    public IReadOnlyList<Vector3> SummonPoints => summonPoints;
    public event Action<Boss> TelegraphStarted;
    public event Action<Boss> TelegraphEnded;
    public event Action<Boss> AttackFired;
    public event Action<Boss> AttackCancelled;
    public event Action<Boss> AttackEnded;
    public event Action<Boss> VolleyFired;
    public event Action<Boss> PhaseTransitionStarted;
    public event Action<Boss> PhaseTransitionEnded;
    public event Action<Boss> Roared;
    public event Action<Boss> Defeated;

    // Body tint that distinguishes phases while idle; telegraph and roar colours override it.
    static readonly Color[] PhaseTints = { new Color(0.8f, 0.3f, 0.2f), new Color(0.55f, 0.12f, 0.45f) };
    Color? PhaseTint => Phase >= 2 ? PhaseTints[Mathf.Min(Phase, 3) - 2] : (Color?)null;
    float Tempo => 1f + (Phase - 1) * PhaseTempoIncrement;
    float RecoveryTime => Mathf.Max(MinimumRecovery, RecoveryDuration - (Phase - 1) * PhaseRecoveryReduction);
    BossPattern[] Sequence => Phase == 1 ? PhaseOne : Phase == 2 ? PhaseTwo : PhaseThree;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        controller = GetComponent<CharacterController>();
    }
    void OnEnable()
    {
        enemy.ExternalMovement = true;
        remaining = RecoveryDuration;
    }
    void OnDisable()
    {
        CancelAttack();
        enemy.Invulnerable = false;
        enemy.ExternalMovement = false;
        enemy.DesiredVelocity = Vector3.zero;
        enemy.TelegraphColor = null;
        ClearSummons();
    }
    void Update()
    {
        enemy.DesiredVelocity = Vector3.zero;
        if (enemy.IsDead)
        {
            if (!defeated)
            {
                defeated = true;
                CancelAttack();
                State = BossState.Defeated;
                enemy.Invulnerable = false;
                enemy.TelegraphColor = null;
                ClearSummons();
                Defeated?.Invoke(this);
            }
            return;
        }
        if (target == null) target = FindFirstObjectByType<WeaponAvatar>();
        if (target == null || target.IsDead)
        {
            // Finish the brief immunity even while the avatar is respawning.
            if (State == BossState.Transition) TickTransition();
            else if (State != BossState.Recovery) { CancelAttack(); BeginRecovery(); }
            return;
        }
        if (State == BossState.Transition) { TickTransition(); return; }
        if ((Phase == 1 && HealthRatio <= PhaseTwoRatio) || (Phase == 2 && HealthRatio <= PhaseThreeRatio))
        {
            BeginTransition();
            return;
        }
        if (State == BossState.Recovery)
        {
            remaining -= Time.deltaTime;
            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), TurnSpeed * Time.deltaTime);
            // Guaranteed stationary damage window; only approach during the last half second.
            if (remaining < 0.5f && to.magnitude > 6f) enemy.DesiredVelocity = to.normalized;
            if (remaining <= 0f) BeginTelegraph(Sequence[patternIndex++ % Sequence.Length]);
        }
        else if (IsTelegraphing)
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0f) FireAttack();
        }
        else if (State == BossState.Attacking) TickAttack();
    }
    void BeginTransition()
    {
        CancelAttack();
        Phase++;
        patternIndex = 0;
        State = BossState.Transition;
        remaining = TransitionDuration;
        enemy.Invulnerable = true;
        enemy.TelegraphColor = new Color(0.7f, 0.8f, 1f);
        PhaseTransitionStarted?.Invoke(this);
        Roared?.Invoke(this);
    }
    void TickTransition()
    {
        remaining -= Time.deltaTime;
        if (remaining > 0f) return;
        enemy.Invulnerable = false;
        enemy.TelegraphColor = PhaseTint;
        PhaseTransitionEnded?.Invoke(this);
        BeginRecovery();
    }
    void BeginTelegraph(BossPattern pattern)
    {
        Pattern = pattern;
        Vector3 to = target.transform.position - transform.position;
        to.y = 0f;
        AimDirection = to.sqrMagnitude > 0.01f ? to.normalized : transform.forward;
        transform.rotation = Quaternion.LookRotation(AimDirection);
        AttackOrigin = transform.position + Vector3.up * GroundMarkerHeight;
        ImpactPoint = AttackOrigin + AimDirection * SlamReach;
        chargeEnd = ClampToArena(transform.position + AimDirection * ChargeLength);
        // Lock the complete line, including its wall-limited endpoint, before windup.
        Vector3 charge = chargeEnd - transform.position;
        if (pattern == BossPattern.Charge && charge.sqrMagnitude > 0.01f) AimDirection = charge.normalized;
        if (pattern == BossPattern.Summon) FindSummonPoints();
        telegraphDuration = (pattern == BossPattern.Slam ? SlamTelegraph : pattern == BossPattern.Charge ? ChargeTelegraph : pattern == BossPattern.Barrage ? BarrageTelegraph : SummonTelegraph) / Tempo;
        remaining = telegraphDuration;
        State = BossState.Telegraph;
        enemy.TelegraphColor = new Color(1f, 0.4f, 0.1f);
        TelegraphStarted?.Invoke(this);
    }
    void EndTelegraph()
    {
        enemy.TelegraphColor = PhaseTint;
        TelegraphEnded?.Invoke(this);
    }
    public void CancelAttack()
    {
        bool warning = IsTelegraphing;
        bool attacking = State == BossState.Attacking;
        if (!warning && !attacking) return;
        State = BossState.Recovery;
        if (warning) EndTelegraph();
        AttackCancelled?.Invoke(this);
    }
    void FireAttack()
    {
        State = BossState.Attacking;
        EndTelegraph();
        chargeHit = false;
        remaining = ChargeLength / (ChargeSpeed * Tempo) + 0.25f;
        volleysLeft = Phase;
        shotTimer = 0f;
        if (Pattern == BossPattern.Slam) HitCircle();
        else if (Pattern == BossPattern.Summon) SpawnSummons();
        AttackFired?.Invoke(this);
        if (Pattern == BossPattern.Slam || Pattern == BossPattern.Summon) FinishAttack();
    }
    void TickAttack()
    {
        if (Pattern == BossPattern.Charge)
        {
            remaining -= Time.deltaTime;
            Vector3 before = transform.position;
            Vector3 to = chargeEnd - before;
            to.y = 0f;
            float distance = Mathf.Min(to.magnitude, ChargeSpeed * Tempo * Time.deltaTime);
            if (distance > 0f) controller.Move(to.normalized * distance);
            Vector3 after = transform.position;
            if (!chargeHit && DistanceToSegment(target.transform.position, before, after) <= ChargeHalfWidth + 0.4f
                && Mathf.Abs(target.Center.y - (transform.position.y + 1f)) < 2f)
            {
                chargeHit = true;
                HurtTarget(ChargeDamage, before);
            }
            if (remaining <= 0f || to.magnitude <= distance + 0.05f || (after - before).sqrMagnitude < 0.00001f) FinishAttack();
        }
        else if (Pattern == BossPattern.Barrage)
        {
            shotTimer -= Time.deltaTime;
            if (shotTimer > 0f) return;
            int shots = 5 + (Phase - 1) * 2;
            Vector3 origin = transform.position + Vector3.up;
            for (int i = 0; i < shots; i++)
            {
                float angle = Mathf.Lerp(-BarrageHalfAngle, BarrageHalfAngle, (float)i / (shots - 1));
                EnemyProjectile.Fire(origin, Quaternion.AngleAxis(angle, Vector3.up) * AimDirection, ProjectileDamage);
            }
            VolleyFired?.Invoke(this);
            shotTimer = BarrageInterval / Tempo;
            if (--volleysLeft <= 0) FinishAttack();
        }
    }
    void FinishAttack()
    {
        AttackEnded?.Invoke(this);
        BeginRecovery();
    }
    void BeginRecovery()
    {
        State = BossState.Recovery;
        remaining = RecoveryTime;
    }
    void HitCircle()
    {
        Vector3 to = target.Center - ImpactPoint;
        float height = to.y;
        to.y = 0f;
        if (height < 2.2f && height > -0.5f && to.magnitude <= SlamRadius + 0.4f) HurtTarget(SlamDamage, ImpactPoint);
    }
    void HurtTarget(int damage, Vector3 from)
    {
        if (target.Invulnerable) PlayerHurtFx.Dodge();
        target.TakeDamage(damage, from);
    }
    Vector3 ClampToArena(Vector3 point)
    {
        point.x = Mathf.Clamp(point.x, arenaCenter.x - arenaHalfSize.x, arenaCenter.x + arenaHalfSize.x);
        point.z = Mathf.Clamp(point.z, arenaCenter.z - arenaHalfSize.y, arenaCenter.z + arenaHalfSize.y);
        return point;
    }
    static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        point.y = a.y = b.y = 0f;
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude < 0.00001f ? 0f : Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude);
        return Vector3.Distance(point, a + ab * t);
    }
    bool CanSummon(Vector3 point)
    {
        if (target != null && Vector3.Distance(point, target.transform.position) < SummonSeparation) return false;
        return !Physics.CheckCapsule(point + Vector3.up * 0.65f, point + Vector3.up * 1.5f, SummonRadius, ~0, QueryTriggerInteraction.Ignore);
    }
    void FindSummonPoints()
    {
        summonPoints.Clear();
        summons.RemoveAll(mob => mob == null || mob.IsDead);
        int count = Mathf.Min(Phase == 1 ? 2 : 3, MaxSummons - summons.Count);
        for (int i = 0; i < 8 && summonPoints.Count < count; i++)
        {
            Vector3 point = arenaCenter + Quaternion.AngleAxis(i * 45f, Vector3.up) * Vector3.forward * 7f;
            if (CanSummon(point)) summonPoints.Add(point);
        }
    }
    void SpawnSummons()
    {
        if (summonTemplate == null) return;
        foreach (var point in summonPoints)
        {
            if (!CanSummon(point)) continue;
            var mob = Instantiate(summonTemplate, point, Quaternion.identity, transform.parent);
            mob.name = "BossSummon";
            mob.gameObject.SetActive(true);
            summons.Add(mob);
        }
    }
    public void ClearSummons()
    {
        foreach (var mob in summons) if (mob != null) Destroy(mob.gameObject);
        summons.Clear();
    }
}

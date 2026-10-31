using UnityEngine;

public struct AvatarInput
{
    // Camera-relative move direction (magnitude 0..1).
    public Vector3 wish;
    public bool jump;
    public bool attack, attackHeld;
    public bool alt, altHeld;
    // V key: melee attack for weapons that spend the mouse buttons on something else (rifle).
    public bool melee;
}

// How one weapon family moves and attacks as an avatar. The base class runs the WeaponData combo
// (WeaponSwinger poses on the avatar's Pose, WeaponHitbox on the visual); subclasses add locomotion.
public abstract class AvatarMoveset : MonoBehaviour
{
    const float AttackBuffer = 0.4f;
    // Attack assist: turn toward and close in on the nearest enemy in front of the camera.
    const float AssistRange = 6f;
    const float AssistAngle = 45f;
    const float AssistStandOff = 1.2f;
    const float AssistLungeMax = 1.5f;
    const float TurnSpeed = 720f;
    const float AttackTurnSpeed = 2400f;

    protected WeaponAvatar avatar;
    protected WeaponData weapon;
    protected Transform visual;
    protected WeaponSwinger swinger;
    protected WeaponHitbox hitbox;
    protected BladeTrail trail;
    protected SwingData currentSwing;
    protected Enemy assistTarget;
    protected float swingFacing;
    protected bool swinging;
    protected bool attackHeld;
    protected bool currentAlt;

    int comboIndex, altComboIndex;
    float lastSwingEnd = -10f;
    float bufferedAttackTime = -10f;
    float bufferedAltTime = -10f;

    // True while an attack is running (weapon switches still cut it; evolving waits).
    public virtual bool Busy => swinging;
    public virtual float ChargeProgress => 0f;
    // True while the camera looks through the weapon's sights (the HUD crosshair hides).
    public virtual bool Aiming => false;
    public virtual string Status => "";

    protected Vector3 CameraForward => Quaternion.Euler(0f, avatar.CameraRig.Yaw, 0f) * Vector3.forward;

    public virtual void Configure(WeaponAvatar owner, WeaponData data, Transform weaponVisual)
    {
        avatar = owner;
        weapon = data;
        visual = weaponVisual;
        swinger = owner.Swinger;
        swinger.SetWeapon(data);
        swinger.StrikeBegan += OnStrikeBegan;
        swinger.StrikeEnded += OnStrikeEnded;
        swinger.Finished += OnSwingFinished;
        BuildHitbox();
    }

    public virtual void Teardown()
    {
        swinger.StrikeBegan -= OnStrikeBegan;
        swinger.StrikeEnded -= OnStrikeEnded;
        swinger.Finished -= OnSwingFinished;
        swinger.Holding = false;
        enabled = false;
    }

    protected virtual void BuildHitbox()
    {
        hitbox = visual.gameObject.AddComponent<WeaponHitbox>();
        hitbox.Configure(weapon.bladeBase, weapon.bladeTip, weapon.hitRadius, weapon.tickInterval);
        hitbox.Hit += OnBladeHit;
        trail = visual.gameObject.AddComponent<BladeTrail>();
        trail.Configure(weapon.bladeBase, weapon.bladeTip, weapon.trailColor);
    }

    public void Tick(AvatarInput input, float dt)
    {
        attackHeld = input.attackHeld;
        TickAttack(input, dt);
        Locomote(input, dt);
    }

    // Planar velocity, jump and Motion offsets for this frame.
    protected abstract void Locomote(AvatarInput input, float dt);

    // Default: buffered WeaponData combo on left (combo) and right (altCombo) click.
    protected virtual void TickAttack(AvatarInput input, float dt)
    {
        if (input.attack) bufferedAttackTime = Time.unscaledTime;
        if (input.alt) bufferedAltTime = Time.unscaledTime;

        bool wantAttack = weapon.combo.Count > 0 && Time.unscaledTime - bufferedAttackTime <= AttackBuffer;
        bool wantAlt = weapon.altCombo.Count > 0 && Time.unscaledTime - bufferedAltTime <= AttackBuffer;
        if (!wantAttack && !wantAlt) return;

        // When both are buffered, the more recent press wins.
        bool alt = wantAlt && (!wantAttack || bufferedAltTime > bufferedAttackTime);
        if (!swinging || (swinger.Phase == SwingPhase.Recovery && swinger.PhaseProgress >= currentSwing.chainAt))
            StartSwing(alt);
    }

    protected virtual void StartSwing(bool alt)
    {
        bufferedAttackTime = -10f;
        bufferedAltTime = -10f;

        var list = alt ? weapon.altCombo : weapon.combo;
        int index = alt ? altComboIndex : comboIndex;
        if (Time.time - lastSwingEnd > weapon.comboResetTime || index >= list.Count) index = 0;
        currentSwing = list[index];
        if (alt) altComboIndex = index + 1;
        else comboIndex = index + 1;
        currentAlt = alt;
        swinging = true;

        assistTarget = FindAssistTarget();
        swingFacing = assistTarget != null ? YawTo(assistTarget.transform.position) : avatar.CameraRig.Yaw;
        swinger.Play(currentSwing);
        HitFeedback.Instance.PlaySwing(currentSwing.heavy);
    }

    protected virtual void OnStrikeBegan()
    {
        hitbox.SetActive(true);
        trail.Emitting = true;

        Vector3 direction = Quaternion.Euler(0f, swingFacing, 0f) * Vector3.forward;
        float distance = currentSwing.lunge;
        if (assistTarget != null && !assistTarget.IsDead)
        {
            Vector3 to = assistTarget.transform.position - transform.position;
            to.y = 0f;
            direction = to.normalized;
            distance = Mathf.Clamp(to.magnitude - AssistStandOff, 0f, currentSwing.lunge * AssistLungeMax);
        }
        avatar.Lunge(direction, distance, currentSwing.strike);
        if (currentSwing.leap > 0f) avatar.Hop(currentSwing.leap);
    }

    protected virtual void OnStrikeEnded()
    {
        hitbox.SetActive(false);
        trail.Emitting = false;
    }

    protected virtual void OnSwingFinished()
    {
        swinging = false;
        lastSwingEnd = Time.time;
    }

    protected virtual void OnBladeHit(HitContact contact) => HandleHit(contact);

    // Whether this contact splits the enemy right away (see PlayerCombat.SlicesNow).
    protected virtual bool SlicesNow(HitContact contact)
    {
        if (currentSwing.cutOnContact) return true;
        if (weapon.sliceDelay > 0f && contact.firstContact && !attackHeld) return true;
        return weapon.instantSliceSpeed > 0f && (contact.firstContact || weapon.shears) && contact.bladeSpeed >= weapon.instantSliceSpeed;
    }

    protected void HandleHit(HitContact contact)
    {
        Enemy enemy = contact.enemy;
        bool tick = !contact.firstContact;

        Vector3 dir = enemy.transform.position - transform.position;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;

        float damageScale = currentSwing.damageScale * (tick ? weapon.tickDamageScale : 1f);
        var info = new HitInfo
        {
            damage = Mathf.Max(1, Mathf.RoundToInt(weapon.damage * damageScale)),
            direction = dir,
            point = contact.point,
            knockback = weapon.knockback * currentSwing.knockbackScale * (tick ? 0.15f : 1f),
            stagger = tick ? 0.15f : currentSwing.stagger,
            heavy = !tick && currentSwing.heavy,
            tick = tick,
            slice = weapon.sliceOnKill,
            bloody = weapon.bloody,
            sliceDelay = weapon.sliceDelay,
            sliceNow = SlicesNow(contact),
            sliceRadius = weapon.sliceBurstRadius,
            cutPoint = contact.point,
            cutNormal = contact.cutNormal,
        };

        bool wasAlive = !enemy.IsDead;
        enemy.TakeHit(info);
        avatar.RecordHit(info.damage, wasAlive && enemy.IsDead);
        OnEnemyHit(enemy, tick, dir);

        if (enemy.IsDead && weapon.sliceOnKill) HitFeedback.Instance.PlaySlice(contact.point, dir, weapon.bloody);
        else if (tick) HitFeedback.Instance.PlayTick(info);
        else
        {
            HitFeedback.Instance.PlayHit(info, weapon, currentSwing, enemy.IsDead);
            if (weapon.bloody) HitFeedback.Instance.PlayBlood(contact.point, dir, 1f);
        }
    }

    protected virtual void OnEnemyHit(Enemy enemy, bool tick, Vector3 direction) { }

    // ---- Helpers for subclasses -----------------------------------------

    // Turns toward the swing facing while attacking, otherwise toward the move direction.
    protected void UpdateFacing(Vector3 wish, float dt, float turnScale = 1f)
    {
        if (swinging)
        {
            float turn = 0f;
            if (currentSwing.bodyYaw != 0f)
            {
                if (swinger.Phase == SwingPhase.Strike) turn = currentSwing.bodyYaw * currentSwing.strikeCurve.Evaluate(swinger.PhaseProgress);
                else if (swinger.Phase == SwingPhase.Recovery) turn = currentSwing.bodyYaw;
            }
            FaceYaw(swingFacing + turn, AttackTurnSpeed * turnScale, dt);
        }
        else if (wish.sqrMagnitude > 0.01f)
        {
            FaceYaw(Mathf.Atan2(wish.x, wish.z) * Mathf.Rad2Deg, TurnSpeed * turnScale, dt);
        }
    }

    protected void FaceYaw(float yaw, float degreesPerSecond, float dt)
    {
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, yaw, 0f), degreesPerSecond * dt);
    }

    protected void Accelerate(Vector3 target, float acceleration, float dt)
    {
        avatar.PlanarVelocity = Vector3.MoveTowards(avatar.PlanarVelocity, target, acceleration * dt);
    }

    // Eases the Motion offset toward a pose (identity = the attack pose alone).
    protected void EaseMotion(Vector3 position, Quaternion rotation, Vector3 scale, float sharpness, float dt)
    {
        float k = 1f - Mathf.Exp(-sharpness * dt);
        var m = avatar.Motion;
        m.localPosition = Vector3.Lerp(m.localPosition, position, k);
        m.localRotation = Quaternion.Slerp(m.localRotation, rotation, k);
        m.localScale = Vector3.Lerp(m.localScale, scale, k);
    }

    protected void RelaxMotion(float dt) => EaseMotion(Vector3.zero, Quaternion.identity, Vector3.one, 20f, dt);

    protected Enemy FindAssistTarget()
    {
        Vector3 forward = CameraForward;
        Enemy best = null;
        float bestDistance = AssistRange;
        foreach (var enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (enemy.IsDead) continue;
            Vector3 to = enemy.transform.position - transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance < 0.01f || distance >= bestDistance || Vector3.Angle(forward, to) > AssistAngle) continue;
            best = enemy;
            bestDistance = distance;
        }
        return best;
    }

    protected float YawTo(Vector3 point)
    {
        Vector3 to = point - transform.position;
        return Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
    }
}

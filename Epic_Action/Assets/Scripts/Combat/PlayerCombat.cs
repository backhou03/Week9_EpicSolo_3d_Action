using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
    enum State { Idle, Charging, Swinging, Transforming }

    const float UpgradeDuration = 0.8f;
    const float UpgradeSwapAt = 0.6f;

    // Attack assist: turn toward and close in on the nearest enemy in front of the camera.
    const float AssistRange = 6f;
    const float AssistAngle = 45f;
    const float AssistStandOff = 1.2f;
    const float AssistLungeMax = 1.5f;
    // Distance a chainsaw tick shoves the player further into the enemy.
    const float ChainsawPush = 0.12f;

    // Enemies the blades overlapped this recently when the jaw snaps shut count as caught between them.
    const float SnapCatchWindow = 0.3f;

    [SerializeField] InputActionAsset actions;
    [SerializeField] PlayerController player;
    [SerializeField] ThirdPersonCamera cameraRig;
    [SerializeField] WeaponSwinger swinger;
    [SerializeField] float pickupRange = 2.2f;
    [SerializeField] float attackBuffer = 0.4f;
    [SerializeField] float pickupRespawnDelay = 1.5f;
    [SerializeField] float pickupSwapDelay = 0.3f;

    InputAction attack, altAttack, interact, skill;
    State state;
    WeaponData weapon;
    WeaponPickup source;
    GameObject weaponVisual;
    WeaponHitbox hitbox;
    ChainsawDriver chainsaw;
    BladeTrail trail;
    ShearsDriver shears;
    NailgunAttack nailgun;
    Enemy assistTarget;
    SwingData currentSwing;
    readonly List<Enemy> caught = new List<Enemy>();
    float chargeElapsed;
    bool sonicSpent;
    bool currentSnip;
    bool snipResolved;
    float jawFrom;
    float swingFacing;
    int comboIndex;
    int altComboIndex;
    int hitCount;
    float lastSwingEnd = -10f;
    float bufferedAttackTime = -10f;
    float bufferedAltTime = -10f;

    public WeaponData Weapon => weapon;
    public string WeaponStatus => nailgun != null ? nailgun.Status : "";
    public bool IsCharging => state == State.Charging;
    public float ChargeProgress => weapon != null && weapon.sonicChargeTime > 0f
        ? Mathf.Clamp01(chargeElapsed / weapon.sonicChargeTime) : 0f;

    void OnEnable()
    {
        var map = actions.FindActionMap("Player", true);
        attack = map.FindAction("Attack", true);
        interact = map.FindAction("Interact", true);
        skill = map.FindAction("Skill", true);
        altAttack = map.FindAction("AltAttack", true);
        map.Enable();

        interact.performed += OnInteract;
        skill.performed += OnSkill;
        swinger.StrikeBegan += OnStrikeBegan;
        swinger.StrikeEnded += OnStrikeEnded;
        swinger.Finished += OnSwingFinished;
    }

    void OnDisable()
    {
        if (nailgun != null) nailgun.Cancel();
        interact.performed -= OnInteract;
        skill.performed -= OnSkill;
        swinger.StrikeBegan -= OnStrikeBegan;
        swinger.StrikeEnded -= OnStrikeEnded;
        swinger.Finished -= OnSwingFinished;
    }

    void Update()
    {
        if (chainsaw != null) chainsaw.Rev = state == State.Swinging ? 1f : 0.3f;
        UpdateWeight();
        UpdateSawing();
        UpdateJaw();
        UpdateBodyTurn();

        if (weapon != null && weapon.nailgun)
        {
            bufferedAttackTime = bufferedAltTime = -10f;
            nailgun.Tick(attack.WasPressedThisFrame(), altAttack.WasPressedThisFrame(), state == State.Idle);
            return;
        }

        if (weapon != null && weapon.sonic && weapon.sonicChargeTime > 0f)
        {
            UpdateSonicCharge();
            return;
        }

        if (attack.WasPressedThisFrame()) bufferedAttackTime = Time.unscaledTime;
        if (altAttack.WasPressedThisFrame()) bufferedAltTime = Time.unscaledTime;
        if (weapon == null) return;

        bool wantAttack = Time.unscaledTime - bufferedAttackTime <= attackBuffer;
        bool wantAlt = weapon.altCombo.Count > 0 && Time.unscaledTime - bufferedAltTime <= attackBuffer;
        if (!wantAttack && !wantAlt) return;

        // When both are buffered, the more recent press wins.
        bool alt = wantAlt && (!wantAttack || bufferedAltTime > bufferedAttackTime);
        if (state == State.Idle)
        {
            StartSwing(alt);
        }
        else if (state == State.Swinging && swinger.Phase == SwingPhase.Recovery && swinger.PhaseProgress >= currentSwing.chainAt)
        {
            StartSwing(alt);
        }
    }

    void UpdateSonicCharge()
    {
        bufferedAttackTime = bufferedAltTime = -10f;
        if (state == State.Idle && attack.WasPressedThisFrame())
        {
            state = State.Charging;
            chargeElapsed = 0f;
            player.SpeedScale = weapon.swingMoveScale;
        }
        if (state != State.Charging) return;

        player.FaceYaw(cameraRig.Yaw);
        if (!attack.IsPressed())
        {
            weaponVisual.transform.localPosition = weapon.gripLocalPosition;
            if (ChargeProgress >= 1f) StartSwing(false);
            else
            {
                state = State.Idle;
                player.SpeedScale = weapon.holdMoveScale;
                player.ReleaseFacing();
            }
            chargeElapsed = 0f;
            return;
        }
        chargeElapsed = Mathf.Min(weapon.sonicChargeTime, chargeElapsed + Time.unscaledDeltaTime);
        float shake = 0.012f * ChargeProgress;
        weaponVisual.transform.localPosition = weapon.gripLocalPosition + new Vector3(
            Mathf.Sin(Time.unscaledTime * 79f), Mathf.Sin(Time.unscaledTime * 97f), 0f) * shake;
    }

    // Holding attack while the blade is buried in an enemy keeps the strike frozen so the saw keeps cutting.
    void UpdateSawing()
    {
        bool saw = weapon != null && weapon.sliceDelay > 0f && state == State.Swinging
            && swinger.Phase == SwingPhase.Strike && attack.IsPressed() && hitbox != null && hitbox.Touching;
        swinger.Holding = saw;
    }

    // A swing that turns the body (spin slash): the body follows the strike curve, then stays turned.
    void UpdateBodyTurn()
    {
        if (state != State.Swinging || currentSwing == null || currentSwing.bodyYaw == 0f) return;

        float turn = 0f;
        switch (swinger.Phase)
        {
            case SwingPhase.Strike: turn = currentSwing.bodyYaw * currentSwing.strikeCurve.Evaluate(swinger.PhaseProgress); break;
            case SwingPhase.Recovery: turn = currentSwing.bodyYaw; break;
        }
        player.FaceYaw(swingFacing + turn);
    }

    // Slashes stay open for the entire attack; snips open, close, then relax.
    void UpdateJaw()
    {
        if (shears == null || weapon == null) return;

        if (state == State.Swinging && !currentSnip)
        {
            shears.Jaw = currentSwing.jawWindup;
            return;
        }

        float jaw = weapon.restJaw;
        if (state == State.Swinging)
        {
            float u = swinger.PhaseProgress;
            switch (swinger.Phase)
            {
                case SwingPhase.Windup: jaw = Mathf.LerpUnclamped(jawFrom, currentSwing.jawWindup, currentSwing.windupCurve.Evaluate(u)); break;
                case SwingPhase.Strike: jaw = Mathf.LerpUnclamped(currentSwing.jawWindup, currentSwing.jawStrike, currentSwing.strikeCurve.Evaluate(u)); break;
                case SwingPhase.Recovery: jaw = Mathf.LerpUnclamped(currentSwing.jawStrike, weapon.restJaw, currentSwing.recoveryCurve.Evaluate(u)); break;
            }
        }

        shears.Jaw = jaw;
    }

    // The blades just met: whatever they overlapped during this swing is cut along the plane they lie in.
    void OnShearsSnapped()
    {
        if (weapon == null || state != State.Swinging || !currentSnip || snipResolved) return;
        snipResolved = true;

        if (hitbox != null)
        {
            hitbox.GetRecentlyTouched(SnapCatchWindow, caught);
            foreach (var enemy in caught)
            {
                HandleHit(new HitContact
                {
                    enemy = enemy,
                    point = enemy.Center,
                    cutNormal = weaponVisual.transform.forward,
                    firstContact = true,
                    bladeSpeed = float.MaxValue,
                });
            }
            caught.Clear();
            if (weapon == null) return;
        }

        HitFeedback.Instance.PlaySnip(weaponVisual.transform.TransformPoint(weapon.bladeTip));
    }

    // A heavy weapon drags the body toward its centre of mass: strongest during the strike, fading in recovery.
    void UpdateWeight()
    {
        if (weapon == null || (weapon.weightPull <= 0f && weapon.weightLean <= 0f)) return;

        float strength = 0f;
        if (state == State.Swinging && !currentSnip)
        {
            switch (swinger.Phase)
            {
                case SwingPhase.Windup: strength = 0.3f; break;
                case SwingPhase.Strike: strength = 1f; break;
                case SwingPhase.Recovery: strength = 1f - swinger.PhaseProgress; break;
            }
        }

        Vector3 toBlade = weaponVisual.transform.TransformPoint((weapon.bladeBase + weapon.bladeTip) * 0.5f) - transform.position;
        toBlade.y = 0f;
        Vector3 direction = toBlade.sqrMagnitude > 0.01f ? toBlade.normalized : Vector3.zero;
        player.SetWeight(direction * (weapon.weightPull * strength), direction, weapon.weightLean * strength);
    }

    void StartSwing(bool alt)
    {
        bufferedAttackTime = -10f;
        bufferedAltTime = -10f;

        var list = alt ? weapon.altCombo : weapon.combo;
        int index = alt ? altComboIndex : comboIndex;
        if (Time.time - lastSwingEnd > weapon.comboResetTime || index >= list.Count) index = 0;
        currentSwing = list[index];
        if (alt) altComboIndex = index + 1;
        else comboIndex = index + 1;
        state = State.Swinging;
        currentSnip = weapon.shears && alt;
        snipResolved = false;
        jawFrom = shears != null ? shears.Jaw : 0f;

        assistTarget = FindAssistTarget();
        swingFacing = assistTarget != null ? YawTo(assistTarget) : cameraRig.Yaw;
        player.FaceYaw(swingFacing);
        player.SpeedScale = weapon.swingMoveScale;
        swinger.Play(currentSwing);
        if (shears != null && !currentSnip) shears.Jaw = currentSwing.jawWindup;
        if (!currentSnip && !weapon.sonic) HitFeedback.Instance.PlaySwing(currentSwing.heavy);
    }

    Enemy FindAssistTarget()
    {
        Vector3 forward = Quaternion.Euler(0f, cameraRig.Yaw, 0f) * Vector3.forward;
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

    float YawTo(Enemy enemy)
    {
        Vector3 to = enemy.transform.position - transform.position;
        return Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
    }

    void OnStrikeBegan()
    {
        if (weapon.sonic)
        {
            Vector3 forward = Quaternion.Euler(0f, swingFacing, 0f) * Vector3.forward;
            if (sonicSpent) return;
            SonicWave.Emit(weapon, transform, weaponVisual.transform.TransformPoint(weapon.sonicMuzzle), forward);
            sonicSpent = weapon.sonicSingleUse;
            return;
        }
        hitbox.SetActive(true);
        if (trail != null) trail.Emitting = !currentSnip;
        if (currentSnip) return;

        Vector3 direction = Quaternion.Euler(0f, cameraRig.Yaw, 0f) * Vector3.forward;
        float distance = currentSwing.lunge;
        if (assistTarget != null && !assistTarget.IsDead)
        {
            Vector3 to = assistTarget.transform.position - transform.position;
            to.y = 0f;
            direction = to.normalized;
            distance = Mathf.Clamp(to.magnitude - AssistStandOff, 0f, currentSwing.lunge * AssistLungeMax);
        }
        player.Lunge(direction, distance, currentSwing.strike);
        if (currentSwing.leap > 0f) player.Hop(currentSwing.leap);
    }

    void OnStrikeEnded()
    {
        // Resolve the close even when a frame steps past the final strike pose.
        if (currentSnip && shears != null)
        {
            shears.Jaw = weapon.jawClosed;
            OnShearsSnapped();
        }
        if (hitbox != null) hitbox.SetActive(false);
        if (trail != null) trail.Emitting = false;
    }

    void OnSwingFinished()
    {
        if (sonicSpent)
        {
            Break();
            return;
        }
        state = State.Idle;
        lastSwingEnd = Time.time;
        player.SpeedScale = weapon.holdMoveScale;
        player.ReleaseFacing();
    }

    // Whether this contact splits the enemy right away. A chainsaw swing cuts at once when the button was already
    // released at contact (a tap); with the button still held it saws instead.
    bool SlicesNow(HitContact contact)
    {
        if (currentSnip || currentSwing.cutOnContact) return true;
        if (weapon.sliceDelay > 0f && contact.firstContact && !attack.IsPressed()) return true;
        return weapon.instantSliceSpeed > 0f && (contact.firstContact || weapon.shears) && contact.bladeSpeed >= weapon.instantSliceSpeed;
    }

    void HandleBladeHit(HitContact contact)
    {
        // Snips collect overlaps and deal damage only when the jaws close.
        if (currentSnip) return;
        HandleHit(contact);
    }

    void HandleHit(HitContact contact)
    {
        if (weapon == null) return;

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

        enemy.TakeHit(info);
        if (chainsaw != null) chainsaw.NotifyContact();
        if (tick && weapon.chainsaw && !enemy.IsDead) player.Lunge(dir, ChainsawPush, weapon.tickInterval);

        if (enemy.IsDead && weapon.sliceOnKill)
        {
            HitFeedback.Instance.PlaySlice(contact.point, dir, weapon.bloody);
        }
        else if (tick)
        {
            HitFeedback.Instance.PlayTick(info);
        }
        else
        {
            HitFeedback.Instance.PlayHit(info, weapon, currentSwing, enemy.IsDead);
            if (weapon.bloody) HitFeedback.Instance.PlayBlood(contact.point, dir, 1f);
        }

        if (!tick && weapon.maxHits > 0 && ++hitCount >= weapon.maxHits) Break();
    }

    void OnInteract(InputAction.CallbackContext ctx)
    {
        if (state != State.Idle || (nailgun != null && nailgun.Busy)) return;
        var pickup = WeaponPickup.FindNearest(transform.position, pickupRange);
        if (pickup == null) return;

        // Picking up another weapon swaps: the one in hand goes back to its pickup.
        if (weapon != null) Unequip(pickupSwapDelay);
        pickup.Take();
        Equip(pickup.Data);
        player.SpeedScale = weapon.holdMoveScale;
        source = pickup;
    }

    void OnSkill(InputAction.CallbackContext ctx)
    {
        if (state != State.Idle || weapon == null || weapon.upgradeTo == null || (nailgun != null && nailgun.Busy)) return;
        StartCoroutine(UpgradeRoutine());
    }

    void Equip(WeaponData data)
    {
        ClearWeaponVisual();

        weapon = data;
        hitCount = 0;
        sonicSpent = false;
        chargeElapsed = 0f;
        comboIndex = 0;
        altComboIndex = 0;
        player.TurnScale = data.turnScale;

        weaponVisual = Instantiate(data.prefab, swinger.transform);
        weaponVisual.transform.localPosition = data.gripLocalPosition;
        weaponVisual.transform.localRotation = Quaternion.Euler(data.gripLocalEuler);
        weaponVisual.transform.localScale = Vector3.one * data.heldScale;

        hitbox = weaponVisual.AddComponent<WeaponHitbox>();
        hitbox.Configure(data.bladeBase, data.bladeTip, data.hitRadius, data.tickInterval);
        hitbox.Hit += HandleBladeHit;

        if (data.chainsaw) chainsaw = weaponVisual.AddComponent<ChainsawDriver>();

        trail = weaponVisual.AddComponent<BladeTrail>();
        if (data.shears)
        {
            // Each blade rides on its own half; the cut plane is the plane the blades lie in.
            shears = weaponVisual.AddComponent<ShearsDriver>();
            shears.Configure(data.jawClosed, data.restJaw);
            shears.Snapped += OnShearsSnapped;
            hitbox.ClearBlades();
            hitbox.AddBlade(shears.HalfA, data.bladeBase, data.bladeTip);
            if (data.mirrorBlade) hitbox.AddBlade(shears.HalfB, MirrorX(data.bladeBase), MirrorX(data.bladeTip));
            hitbox.CutPlane = weaponVisual.transform;
            trail.Configure(shears.HalfA, data.bladeTip, shears.HalfB, MirrorX(data.bladeTip), data.trailColor);
        }
        else
        {
            trail.Configure(data.bladeBase, data.bladeTip, data.trailColor);
        }

        swinger.SetWeapon(data);
        if (data.nailgun)
        {
            nailgun = gameObject.AddComponent<NailgunAttack>();
            nailgun.Configure(data, player, cameraRig, weaponVisual.transform, Break);
        }
    }

    static Vector3 MirrorX(Vector3 v) => new Vector3(-v.x, v.y, v.z);

    void ClearWeaponVisual()
    {
        if (nailgun != null)
        {
            nailgun.Cancel();
            nailgun.enabled = false;
            Destroy(nailgun);
            nailgun = null;
        }
        shears = null;
        if (weaponVisual != null) Destroy(weaponVisual);
        weaponVisual = null;
        hitbox = null;
        chainsaw = null;
        trail = null;
    }

    IEnumerator UpgradeRoutine()
    {
        state = State.Transforming;
        player.SpeedScale = 0.4f;
        HitFeedback.Instance.PlayUpgrade();

        Transform t = weaponVisual.transform;
        Vector3 basePos = t.localPosition;
        float baseScale = weapon.heldScale;
        for (float time = 0f; time < UpgradeSwapAt; time += Time.deltaTime)
        {
            float u = time / UpgradeSwapAt;
            t.localScale = Vector3.one * (baseScale * (1f + 0.5f * u * u));
            t.localPosition = basePos + Random.insideUnitSphere * (0.015f + 0.03f * u);
            yield return null;
        }

        WeaponData next = weapon.upgradeTo;
        Equip(next);
        HitFeedback.Instance.PlayUpgradeFlash(weaponVisual.transform.position + weaponVisual.transform.up * 0.4f);

        // Pop-in: overshoot then settle.
        float remaining = UpgradeDuration - UpgradeSwapAt;
        for (float time = 0f; time < remaining; time += Time.deltaTime)
        {
            float u = time / remaining;
            float pop = u < 0.35f ? Mathf.Lerp(0.5f, 1.2f, u / 0.35f) : Mathf.Lerp(1.2f, 1f, (u - 0.35f) / 0.65f);
            weaponVisual.transform.localScale = Vector3.one * (next.heldScale * pop);
            yield return null;
        }
        weaponVisual.transform.localScale = Vector3.one * next.heldScale;

        player.SpeedScale = weapon.holdMoveScale;
        state = State.Idle;
    }

    void Break()
    {
        HitFeedback.Instance.PlayBreak(weaponVisual.transform.position + weaponVisual.transform.up * 0.5f);
        Unequip(pickupRespawnDelay);
    }

    // Drops the weapon in hand; its source pickup comes back after `respawnDelay`.
    void Unequip(float respawnDelay)
    {
        ClearWeaponVisual();
        swinger.Cancel();
        swinger.SetWeapon(null);
        weapon = null;
        currentSwing = null;
        sonicSpent = false;
        chargeElapsed = 0f;
        state = State.Idle;
        player.SpeedScale = 1f;
        player.TurnScale = 1f;
        player.SetWeight(Vector3.zero, Vector3.zero, 0f);
        player.ReleaseFacing();

        if (source != null) source.Respawn(respawnDelay);
        source = null;
    }
}

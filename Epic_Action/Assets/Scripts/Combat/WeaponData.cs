using System.Collections.Generic;
using UnityEngine;

public enum SwingPhase { None, Windup, Strike, Recovery }

[System.Serializable]
public class SwingData
{
    [Header("Timing (sec)")]
    public float windup = 0.1f;
    public float strike = 0.08f;
    public float recovery = 0.2f;
    [Tooltip("Recovery progress (0-1) after which the next attack input can chain.")]
    public float chainAt = 0.45f;

    [Header("Pose (hand pivot, player local)")]
    public Vector3 windupPosition;
    public Vector3 windupEuler;
    public Vector3 strikePosition;
    public Vector3 strikeEuler;

    [Header("Curves (0-1 in, 0-1 out; may overshoot)")]
    public AnimationCurve windupCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 3f), new Keyframe(1f, 1f, 0f, 0f));
    public AnimationCurve strikeCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 3f, 0f));
    public AnimationCurve recoveryCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 0f, 0f));

    [Header("Feel (multipliers on WeaponData)")]
    public float damageScale = 1f;
    public float knockbackScale = 1f;
    public float hitStopScale = 1f;
    public float shakeScale = 1f;
    public float stagger = 0.35f;
    [Header("Shears jaw (degrees each half opens beyond its model pose; negative closes)")]
    public float jawWindup;
    public float jawStrike;
    [Header("Body")]
    [Tooltip("Degrees the body turns (clockwise) during the strike; 360 = a full spin slash.")]
    public float bodyYaw;
    [Tooltip("The blades cut whatever they touch right away, whatever their speed (shears swung open).")]
    public bool cutOnContact;
    [Tooltip("Height (m) of the hop the strike starts with; 0 = stay on the ground.")]
    public float leap;
    public float lunge = 0.6f;
    public bool heavy;

    public float Duration(SwingPhase phase)
    {
        switch (phase)
        {
            case SwingPhase.Windup: return windup;
            case SwingPhase.Strike: return strike;
            case SwingPhase.Recovery: return recovery;
            default: return 0f;
        }
    }

    public void Evaluate(SwingPhase phase, float u, Vector3 fromPos, Vector3 fromEuler, WeaponData weapon, out Vector3 pos, out Vector3 euler)
    {
        switch (phase)
        {
            case SwingPhase.Windup:
                {
                    float k = windupCurve.Evaluate(u);
                    pos = Vector3.LerpUnclamped(fromPos, windupPosition, k);
                    euler = Vector3.LerpUnclamped(fromEuler, windupEuler, k);
                    break;
                }
            case SwingPhase.Strike:
                {
                    float k = strikeCurve.Evaluate(u);
                    pos = Vector3.LerpUnclamped(windupPosition, strikePosition, k);
                    euler = Vector3.LerpUnclamped(windupEuler, strikeEuler, k);
                    break;
                }
            default:
                {
                    float k = recoveryCurve.Evaluate(u);
                    pos = Vector3.LerpUnclamped(strikePosition, weapon.restPosition, k);
                    euler = Vector3.LerpUnclamped(strikeEuler, weapon.restEuler, k);
                    break;
                }
        }
    }
}

[CreateAssetMenu(menuName = "Epic Action/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Info (HUD)")]
    public string displayName;
    [TextArea(2, 8)] public string description;

    [Header("Visual")]
    public GameObject prefab;
    public float heldScale = 1f;
    public Vector3 gripLocalPosition;
    public Vector3 gripLocalEuler;
    [Tooltip("Hand pivot pose while not swinging.")]
    public Vector3 restPosition = new Vector3(0.5f, 0.95f, 0.45f);
    public Vector3 restEuler = new Vector3(30f, 0f, -15f);

    [Header("Hit detection (weapon prefab local)")]
    public Vector3 bladeBase;
    public Vector3 bladeTip = Vector3.up;
    public float hitRadius = 0.2f;

    [Header("Combat")]
    public int damage = 6;
    public float knockback = 5f;
    [Tooltip("0 = never breaks. Otherwise the weapon breaks on this many enemy hits.")]
    public int maxHits;
    public float comboResetTime = 0.9f;
    public List<SwingData> combo = new List<SwingData>();
    [Tooltip("Played by the alternate attack button (right mouse); empty = none.")]
    public List<SwingData> altCombo = new List<SwingData>();

    [Header("Nailgun")]
    public bool nailgun;
    public bool nailPiercing;
    public float nailAttachRange = 6f;
    public float nailDetachDelay = 0.5f;
    public float nailChargeTime = 0.45f;
    public float nailProjectileSpeed = 75f;
    public float nailProjectileRange = 22f;
    public Vector3 nailMuzzle;
    public GameObject nailProjectilePrefab;

    [Header("Megaphone")]
    public bool sonic;
    public bool sonicCrush;
    public float sonicChargeTime;
    public bool sonicSingleUse;
    public float sonicRange = 5f;
    public float sonicHalfAngle = 25f;
    public float sonicSpeed = 18f;
    public Vector3 sonicMuzzle = new Vector3(0f, 0.27f, 0.4f);

    [Header("Rifle (weapon avatar)")]
    public bool rifle;
    public Vector3 rifleMuzzle;
    [Tooltip("Rear aperture centre and front post tip in model space; the aiming view looks along this line.")]
    public Vector3 rifleSightRear;
    public Vector3 rifleSightFront;
    [Tooltip("Seconds between shots while the attack button is held.")]
    public float fireInterval = 0.09f;
    public float fireRange = 30f;

    [Header("Chainsaw")]
    [Tooltip("Attach ChainsawDriver (chain loop, engine sound) to the weapon visual.")]
    public bool chainsaw;
    [Tooltip("0 = one hit per enemy per swing. Otherwise re-hit enemies still touching the blade at this interval (sec).")]
    public float tickInterval;
    [Tooltip("Damage of a continuous-cut tick relative to a first-contact hit.")]
    public float tickDamageScale = 0.25f;
    [Tooltip("A killing blow splits the enemy along the blade.")]
    public bool sliceOnKill;
    [Tooltip("0 = only a killing blow splits the enemy. Otherwise the enemy splits after the blade has sawn it for this many seconds in total.")]
    public float sliceDelay;
    [Tooltip("When a saw cut (sliceDelay) completes, other enemies within this radius (m) are cut at once; 0 = off.")]
    public float sliceBurstRadius;
    [Tooltip("0 = off. A first contact where the blade moves at least this fast (m/s) splits the enemy immediately.")]
    public float instantSliceSpeed;

    [Header("Weight")]
    [Tooltip("Player move speed multiplier while holding the weapon.")]
    public float holdMoveScale = 1f;
    [Tooltip("Player move speed multiplier while swinging.")]
    public float swingMoveScale = 0.3f;
    [Tooltip("Player turn speed multiplier while holding the weapon.")]
    public float turnScale = 1f;
    [Tooltip("Speed (m/s) at which a swing drags the body toward the blade's centre of mass.")]
    public float weightPull;
    [Tooltip("Degrees the body leans toward the blade's centre of mass during a swing.")]
    public float weightLean;

    [Header("Shears")]
    [Tooltip("Attach ShearsDriver (jaw open/close) and use the two halves as blades.")]
    public bool shears;
    [Tooltip("Red blood effects and red cut faces.")]
    public bool bloody;
    [Tooltip("Jaw angle at which the two blades meet.")]
    public float jawClosed = -27f;
    [Tooltip("Jaw angle while holding the weapon.")]
    public float restJaw;
    [Tooltip("Second blade = bladeBase/bladeTip mirrored in x, riding on HalfB.")]
    public bool mirrorBlade;

    [Header("Feedback")]
    public float hitStop = 0.06f;
    public float shake = 0.15f;
    public float fovPunch = 1.5f;
    public Color trailColor = new Color(1f, 1f, 1f, 0.8f);

    [Header("Skill")]
    public WeaponData upgradeTo;
}

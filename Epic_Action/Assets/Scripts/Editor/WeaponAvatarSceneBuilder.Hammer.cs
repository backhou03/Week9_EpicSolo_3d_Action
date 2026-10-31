using UnityEditor;
using UnityEngine;

public static partial class WeaponAvatarSceneBuilder
{
    const string HammerPath = "Assets/Weapons/Heavy_Hammer/Heavy_Hammer.obj";
    const float HammerScale = 0.4f;
    // Model faces point along +-X; a 90 degree yaw turns them into the swing direction (forward/back).
    const float HammerGripYaw = 90f;
    const float HammerHeadHeight = 2.35f;
    // Trail ribbon runs along the handle so the swept arc reads from the side.
    const float HammerTrailHalfSpan = 0.45f;
    const float HammerHitRadius = 0.5f;

    static WeaponData Hammer()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(HammerPath);
        if (model == null) throw new System.Exception("Missing " + HammerPath);
        var d = GameSceneBuilder.LoadOrCreate<WeaponData>(AvatarDir + "/Avatar_Hammer.asset");
        d.displayName = "중량 망치";
        d.description = "올려치기 → 내려찍기 → 홀드 회전 후 내려찍기 · E 가속 돌진 · Q 거대 회전 15초";
        d.prefab = model;
        d.hammer = true;
        d.rifle = d.chainsaw = d.shears = false;
        d.heldScale = HammerScale;
        d.gripLocalPosition = Vector3.zero;
        d.gripLocalEuler = new Vector3(0f, HammerGripYaw, 0f);
        d.restPosition = new Vector3(0f, 0.15f, 0f);
        d.restEuler = Vector3.zero;
        d.bladeBase = new Vector3(0f, HammerHeadHeight - HammerTrailHalfSpan, 0f);
        d.bladeTip = new Vector3(0f, HammerHeadHeight + HammerTrailHalfSpan, 0f);
        d.hitRadius = HammerHitRadius;
        d.damage = 12;
        d.knockback = 7f;
        d.maxHits = 0;
        d.comboResetTime = 1.2f;
        d.swingMoveScale = 0.75f;
        d.tickInterval = 0f;
        d.sliceOnKill = false;
        d.hitStop = 0.07f;
        d.shake = 0.28f;
        d.fovPunch = 2.2f;
        d.trailColor = new Color(1f, 0.7f, 0.25f, 0.85f);
        d.upgradeTo = null;
        d.combo.Clear();
        d.altCombo.Clear();
        // Pose: hand pivot with the handle along +Y; X pitch swings the head forward. Hit lands at the end of windup.
        d.combo.Add(GameSceneBuilder.Swing(0.14f, 0.12f, 0.16f,
            new Vector3(0f, 1.3f, 0.2f), new Vector3(115f, 0f, 0f),
            new Vector3(0f, 1.3f, 0.4f), new Vector3(15f, 0f, 0f),
            lunge: 0f, damage: 1f, shake: 1.2f, heavy: true));
        d.combo.Add(GameSceneBuilder.Swing(0.16f, 0.08f, 0.2f,
            new Vector3(0f, 1.2f, -0.1f), new Vector3(-40f, 0f, 0f),
            new Vector3(0f, 1.1f, 0.55f), new Vector3(100f, 0f, 0f),
            lunge: 0f, damage: 1.5f, shake: 1.8f, heavy: true));
        d.combo.Add(GameSceneBuilder.Swing(0.12f, 0.2f, 0.35f,
            new Vector3(0f, 1.7f, 0f), new Vector3(-25f, 0f, 0f),
            new Vector3(0f, 0.7f, 1.2f), new Vector3(105f, 0f, 0f),
            lunge: 0f, damage: 1.8f, shake: 2.2f, heavy: true));
        foreach (var swing in d.combo)
        {
            // Quick cock-back with a little overshoot, a fall that keeps accelerating, a recovery that lifts off the floor.
            swing.windupCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 1.5f), new Keyframe(0.75f, 1.06f, 0.6f, 0.1f), new Keyframe(1f, 1f, -0.6f, 0f));
            swing.strikeCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0.2f), new Keyframe(1f, 1f, 2.8f, 0f));
            swing.recoveryCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0.6f), new Keyframe(1f, 1f, 1.5f, 0f));
        }
        return d;
    }
}

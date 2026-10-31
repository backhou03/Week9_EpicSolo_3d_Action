using UnityEngine;

public static partial class WeaponAvatarSceneBuilder
{
    static WeaponData EvolvedShears(WeaponData d)
    {
        d.heldScale = 2.3f;
        d.hitRadius = 0.3f;
        d.damage = 14;
        d.knockback = 4f;
        d.hitStop = 0.035f;
        d.swingMoveScale = 0.6f;
        d.comboResetTime = 0.65f;
        d.tickInterval = 0f;
        d.restPosition = new Vector3(0f, d.bladeTip.magnitude * d.heldScale, 0f);
        d.restEuler = new Vector3(0f, 90f, 180f);
        d.combo.Clear();
        d.altCombo.Clear();
        AddShearsCombo(d, 55f, 1.6f);
        return d;
    }

    static void AddShearsCombo(WeaponData d, float open, float lunge)
    {
        // Cuts pull back, snap forward and squeeze the blades shut; the torso follows each sweep.
        d.combo.Add(GameSceneBuilder.Swing(0.07f, 0.09f, 0.16f,
            new Vector3(0f, 1f, -0.3f), new Vector3(90f, 110f, 0f),
            new Vector3(0f, 1f, 0.35f), new Vector3(90f, -70f, 0f),
            lunge: lunge, stagger: 0.24f, knockback: 0.55f, hitStop: 0.8f, shake: 1.1f, chainAt: 0.25f,
            jawWindup: open + 10f, jawStrike: open * 0.35f, bodyYaw: -14f, cutOnContact: true));
        d.combo.Add(GameSceneBuilder.Swing(0.11f, 0.12f, 0.18f,
            new Vector3(0f, 1f, -0.3f), new Vector3(90f, -110f, 0f),
            new Vector3(0f, 1f, 0.4f), new Vector3(90f, 70f, 0f),
            lunge: lunge * 0.8f, stagger: 0.3f, damage: 1.15f, knockback: 0.65f, hitStop: 1f, shake: 1.3f, chainAt: 0.3f,
            jawWindup: open + 10f, jawStrike: open * 0.3f, bodyYaw: 14f, cutOnContact: true));
        // Finisher: raised high with the jaw yawning wide, then dropped and bitten shut.
        d.combo.Add(GameSceneBuilder.Swing(0.17f, 0.15f, 0.28f,
            new Vector3(0f, 1.25f, -0.55f), new Vector3(90f, 130f, 0f),
            new Vector3(0f, 0.9f, 0.6f), new Vector3(90f, -100f, 0f),
            lunge: lunge * 1.25f, stagger: 0.65f, damage: 1.9f, knockback: 1.6f, hitStop: 2f, shake: 2.2f, heavy: true, chainAt: 0.7f,
            jawWindup: open + 30f, jawStrike: 6f, bodyYaw: -26f, cutOnContact: true));
        d.combo[0].strikeCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0.4f), new Keyframe(1f, 1f, 4f, 0f));
        d.combo[1].strikeCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0.4f), new Keyframe(1f, 1f, 4f, 0f));
        d.combo[2].windupCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        d.combo[2].strikeCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0.2f), new Keyframe(1f, 1f, 5f, 0f));

        d.altCombo.Add(GameSceneBuilder.Swing(0.10f, 0.08f, 0.10f,
            new Vector3(0f, 1f, 0.2f), new Vector3(90f, 0f, 0f),
            new Vector3(0f, 1f, 0.2f), new Vector3(90f, 0f, 0f),
            lunge: 1.2f, stagger: 0.35f, damage: 1.1f, knockback: 0.35f, hitStop: 0.8f, jawWindup: open, jawStrike: -30f));
        d.altCombo.Add(GameSceneBuilder.Swing(0.09f, 0.08f, 0.12f,
            new Vector3(0f, 1.1f, 0.2f), new Vector3(0f, 90f, 90f),
            new Vector3(0f, 1.1f, 0.2f), new Vector3(0f, 90f, 90f),
            lunge: 0.8f, stagger: 0.4f, damage: 1.2f, knockback: 0.4f, hitStop: 0.9f, jawWindup: open, jawStrike: -30f));
        d.altCombo.Add(GameSceneBuilder.Swing(0.14f, 0.10f, 0.24f,
            new Vector3(0f, 1.05f, 0.2f), new Vector3(45f, 90f, 90f),
            new Vector3(0f, 1.05f, 0.2f), new Vector3(45f, 90f, 90f),
            lunge: 0.8f, stagger: 0.65f, damage: 1.65f, knockback: 1.4f, hitStop: 1.3f, heavy: true, jawWindup: open + 5f, jawStrike: -30f));
    }
}

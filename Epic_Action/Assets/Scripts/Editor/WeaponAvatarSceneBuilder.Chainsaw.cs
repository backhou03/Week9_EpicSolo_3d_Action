using UnityEngine;

public static partial class WeaponAvatarSceneBuilder
{
    static WeaponData Chainsaw(WeaponData d)
    {
        d.heldScale = 2.2f;
        d.hitRadius = 0.65f;
        d.damage = 22;
        // XY is the blade plane: +Y faces forward, -X chain runs along the floor, +Z faces right.
        d.restPosition = new Vector3(0f, 0.2f * d.heldScale, -0.55f);
        d.restEuler = new Vector3(0f, 90f, 90f);
        d.swingMoveScale = 0.55f;
        d.comboResetTime = 1.1f;
        d.tickInterval = 0.1f;
        d.tickDamageScale = 0.35f;
        d.combo.Clear();
        d.altCombo.Clear();
        // Air attack starts behind: halfway through the body turn the diagonal blade crosses the front.
        d.altCombo.Add(GameSceneBuilder.Swing(0.12f, 0.29f, 0.22f,
            new Vector3(0f, 1.2f, -0.45f), new Vector3(55f, 180f, -35f),
            new Vector3(0f, 0.45f, -0.45f), new Vector3(115f, 180f, 35f),
            lunge: 1.2f, damage: 1.15f, shake: 1.2f, bodyYaw: 360f));
        d.altCombo[0].strikeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        // Plant the edge low, then kick hard off the ground into a diagonal spinning cut.
        // Leap and recovery are sized so the landing meets the end of recovery; the eased curve lets the flip finish before the cut spins.
        d.combo.Add(GameSceneBuilder.Swing(0.16f, 0.30f, 0.27f,
            new Vector3(0f, 1.1f, -0.3f), new Vector3(0f, 90f, -20f),
            new Vector3(0f, 0.55f, 0.1f), new Vector3(110f, 65f, 30f),
            lunge: 1.6f, leap: 1.0f, shake: 1.3f, chainAt: 0.45f, bodyYaw: 360f));
        d.combo[0].strikeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        d.combo.Add(GameSceneBuilder.Swing(0.10f, 0.22f, 0.18f,
            new Vector3(0f, 0.65f, -0.3f), new Vector3(90f, 110f, 0f),
            new Vector3(0f, 0.65f, 0.1f), new Vector3(90f, -110f, 0f),
            lunge: 1.1f, damage: 1.15f, shake: 0.8f, chainAt: 0.4f, bodyYaw: -300f));
        d.combo[1].strikeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        // Give the windup bounce time to rise before driving the chain edge down ahead.
        d.combo.Add(GameSceneBuilder.Swing(0.45f, 0.22f, 0.38f,
            new Vector3(0f, d.restPosition.y, -0.6f), new Vector3(0f, 90f, -30f),
            new Vector3(0f, 0.115f * d.heldScale, 0.15f), new Vector3(0f, 90f, 90f),
            lunge: 1.2f, stagger: 0.6f, damage: 1.6f, knockback: 1.8f, hitStop: 1.5f, shake: 1.8f, heavy: true, chainAt: 0.8f));
        // Plant the edge during the fall, then keep it down for the rest of the strike.
        d.combo[2].strikeCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 4f),
            new Keyframe(0.6f, 1f, 0f, 0f), new Keyframe(1f, 1f, 0f, 0f));
        return d;
    }
}

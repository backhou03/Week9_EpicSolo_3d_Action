using System;
using UnityEngine;

public class WeaponSwinger : MonoBehaviour
{
    public event Action StrikeBegan;
    public event Action StrikeEnded;
    public event Action Finished;

    public SwingPhase Phase { get; private set; }
    public float PhaseProgress { get; private set; }
    // Freezes the pose in the middle of the strike (blade buried in an enemy).
    public bool Holding { get; set; }

    WeaponData weapon;
    SwingData swing;
    float timer;
    Vector3 fromPos, fromEuler;
    Vector3 pos, euler;

    public void SetWeapon(WeaponData data)
    {
        weapon = data;
        Phase = SwingPhase.None;
        if (data == null) return;
        pos = data.restPosition;
        euler = data.restEuler;
        Apply();
    }

    public void Play(SwingData data)
    {
        swing = data;
        fromPos = pos;
        fromEuler = euler;
        timer = 0f;
        Phase = SwingPhase.Windup;
        PhaseProgress = 0f;
    }

    public void Cancel()
    {
        Holding = false;
        Phase = SwingPhase.None;
    }

    void Update()
    {
        if (weapon == null) return;

        if (Phase == SwingPhase.None)
        {
            UpdateIdle();
            return;
        }

        if (Holding && Phase == SwingPhase.Strike) return;

        timer += Time.deltaTime;
        while (timer >= swing.Duration(Phase))
        {
            timer -= swing.Duration(Phase);
            if (Phase == SwingPhase.Windup)
            {
                Phase = SwingPhase.Strike;
                StrikeBegan?.Invoke();
            }
            else if (Phase == SwingPhase.Strike)
            {
                Phase = SwingPhase.Recovery;
                StrikeEnded?.Invoke();
                // A snip may break and unequip the weapon while resolving its final hit.
                if (weapon == null || Phase == SwingPhase.None) return;
            }
            else
            {
                Phase = SwingPhase.None;
                Finished?.Invoke();
                if (weapon == null) return;
                UpdateIdle();
                return;
            }
        }

        PhaseProgress = Mathf.Clamp01(timer / swing.Duration(Phase));
        swing.Evaluate(Phase, PhaseProgress, fromPos, fromEuler, weapon, out pos, out euler);
        Apply();
    }

    void UpdateIdle()
    {
        float blend = 1f - Mathf.Exp(-12f * Time.deltaTime);
        Vector3 targetPos = weapon.restPosition + Vector3.up * (Mathf.Sin(Time.time * 2f) * 0.012f);
        pos = Vector3.Lerp(pos, targetPos, blend);
        euler = Vector3.Lerp(euler, weapon.restEuler, blend);
        Apply();
    }

    void Apply()
    {
        transform.localPosition = pos;
        transform.localRotation = Quaternion.Euler(euler);
    }
}

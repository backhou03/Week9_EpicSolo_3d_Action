using UnityEngine;

public struct HitInfo
{
    public int damage;
    public bool crush;
    public Vector3 direction;
    public Vector3 point;
    public float knockback;
    public float stagger;
    public bool heavy;
    // Continuous-cut hit while the blade stays inside an enemy (not the first contact of a swing).
    public bool tick;
    // A killing blow with this info splits the enemy along the plane (cutPoint, cutNormal).
    public bool slice;
    // > 0: the enemy also splits once the blade has sawn it for this many seconds in total, not only on a killing blow.
    public float sliceDelay;
    // Splits the enemy right now, whatever its HP (a blade fast enough to cut through).
    public bool sliceNow;
    // > 0: when a saw cut completes, every other enemy within this radius is cut at once too.
    public float sliceRadius;
    // Red blood: the cut faces of the pieces are red.
    public bool bloody;
    public Vector3 cutPoint;
    public Vector3 cutNormal;
}

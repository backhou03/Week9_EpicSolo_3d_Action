using System;
using UnityEngine;

// Opens and closes the shears: the two halves turn about the pivot's Z axis in opposite directions.
// Jaw is how far each half is opened beyond its model pose (negative = closing); the model's own pose
// opens the blades about 27 degrees each side.
public class ShearsDriver : MonoBehaviour
{
    // Jaw must have been at least this open and then reach the closed angle within SnapWindow to count as a snip.
    const float SnapOpen = 14f;
    const float SnapWindow = 0.12f;

    public event Action Snapped;

    public Transform HalfA { get; private set; }
    public Transform HalfB { get; private set; }

    float jaw;
    float closedJaw = -27f;
    float lastOpenTime = -10f;
    bool snapReady;

    public float Jaw
    {
        get => jaw;
        set
        {
            jaw = Mathf.Max(value, closedJaw);
            if (HalfA == null) return;
            // The imported halves are mirrored: a positive Z turn closes HalfA and opens HalfB.
            HalfA.localRotation = Quaternion.Euler(0f, 0f, -jaw);
            HalfB.localRotation = Quaternion.Euler(0f, 0f, jaw);
        }
    }

    public void Configure(float jawClosed, float startJaw)
    {
        closedJaw = jawClosed;
        Jaw = startJaw;
    }

    void Awake()
    {
        HalfA = transform.Find("HalfA");
        HalfB = transform.Find("HalfB");
        if (HalfA == null || HalfB == null) Debug.LogWarning("ShearsDriver: HalfA/HalfB children not found; the blades will not move.");
    }

    // Runs after PlayerCombat.Update has set this frame's jaw.
    void LateUpdate()
    {
        if (jaw >= closedJaw + SnapOpen)
        {
            lastOpenTime = Time.time;
            snapReady = true;
        }
        else if (snapReady && jaw <= closedJaw + 2f)
        {
            snapReady = false;
            if (Time.time - lastOpenTime <= SnapWindow) Snapped?.Invoke();
        }
    }
}

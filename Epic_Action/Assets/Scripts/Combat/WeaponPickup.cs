using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponPickup : MonoBehaviour
{
    static readonly List<WeaponPickup> all = new List<WeaponPickup>();

    [SerializeField] WeaponData data;
    [SerializeField] Transform visual;
    [SerializeField] float spinSpeed = 90f;
    [SerializeField] float bobHeight = 0.15f;
    [SerializeField] float bobSpeed = 2f;

    Vector3 visualBasePos;
    Vector3 visualBaseScale;

    public WeaponData Data => data;
    public bool Available { get; private set; } = true;

    public static WeaponPickup FindNearest(Vector3 position, float range)
    {
        WeaponPickup best = null;
        float bestSqr = range * range;
        foreach (var p in all)
        {
            if (!p.Available) continue;
            Vector3 d = p.transform.position - position;
            d.y = 0f;
            if (d.sqrMagnitude < bestSqr)
            {
                bestSqr = d.sqrMagnitude;
                best = p;
            }
        }
        return best;
    }

    void Awake()
    {
        visualBasePos = visual.localPosition;
        visualBaseScale = visual.localScale;
    }

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    void Update()
    {
        if (!Available) return;
        visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        visual.localPosition = visualBasePos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
    }

    public void Take()
    {
        Available = false;
        visual.gameObject.SetActive(false);
    }

    public void Respawn(float delay)
    {
        StartCoroutine(RespawnRoutine(delay));
    }

    IEnumerator RespawnRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        Available = true;
        visual.gameObject.SetActive(true);

        const float duration = 0.35f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float u = t / duration;
            float pop = u < 0.7f ? Mathf.Lerp(0f, 1.25f, u / 0.7f) : Mathf.Lerp(1.25f, 1f, (u - 0.7f) / 0.3f);
            visual.localScale = visualBaseScale * pop;
            yield return null;
        }
        visual.localScale = visualBaseScale;
    }
}

using UnityEngine;

// Basic playable warnings. Presentation can disable this component and consume Boss events instead.
[RequireComponent(typeof(Boss))]
public class BossTelegraph : MonoBehaviour
{
    public const float LineWidth = 0.12f;
    Boss boss;
    Material material;
    readonly LineRenderer[] lines = new LineRenderer[Boss.MaxSummons];
    void Awake()
    {
        boss = GetComponent<Boss>();
        material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.color = new Color(1f, 0.35f, 0.08f);
        for (int i = 0; i < lines.Length; i++)
        {
            var go = new GameObject("BossWarning" + i);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.widthMultiplier = LineWidth;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            lines[i] = line;
        }
    }
    void LateUpdate()
    {
        Hide();
        if (!boss.IsTelegraphing) return;
        if (boss.Pattern == BossPattern.Slam) Circle(lines[0], boss.ImpactPoint, Boss.SlamRadius);
        else if (boss.Pattern == BossPattern.Summon)
        {
            for (int i = 0; i < boss.SummonPoints.Count; i++) Circle(lines[i], boss.SummonPoints[i] + Vector3.up * 0.08f, Boss.SummonRadius);
        }
        else if (boss.Pattern == BossPattern.Charge)
        {
            Vector3 side = Vector3.Cross(Vector3.up, boss.AimDirection) * Boss.ChargeHalfWidth;
            Vector3 a = boss.AttackOrigin, b = boss.ChargeEnd + Vector3.up * 0.08f;
            Show(lines[0], new[] { a - side, b - side, b + side, a + side, a - side });
        }
        else
        {
            var points = new Vector3[19];
            points[0] = boss.AttackOrigin;
            for (int i = 0; i < 17; i++)
                points[i + 1] = boss.AttackOrigin + Quaternion.AngleAxis(Mathf.Lerp(-Boss.BarrageHalfAngle, Boss.BarrageHalfAngle, i / 16f), Vector3.up) * boss.AimDirection * Boss.BarrageRange;
            points[18] = boss.AttackOrigin;
            Show(lines[0], points);
        }
    }
    static void Circle(LineRenderer line, Vector3 centre, float radius)
    {
        var points = new Vector3[49];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = i * Mathf.PI * 2f / (points.Length - 1);
            points[i] = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        }
        Show(line, points);
    }
    static void Show(LineRenderer line, Vector3[] points)
    {
        line.enabled = true;
        line.positionCount = points.Length;
        line.SetPositions(points);
    }
    void Hide() { foreach (var line in lines) if (line != null) line.enabled = false; }
    void OnDisable() => Hide();
    void OnDestroy() { if (material != null) Destroy(material); }
}

using UnityEngine;

// Look of a weapon swap facility: glowing ring, floating crystal, light column and an [F] prompt. Selection UI is untouched.
[RequireComponent(typeof(WeaponSwapFacility))]
public class FacilityFx : MonoBehaviour
{
    static readonly Color Idle = new Color(0.2f, 0.9f, 0.95f);
    static readonly Color UseColor = new Color(1f, 0.95f, 0.6f);
    const float NearRange = 9f;
    const float KickDecay = 2.2f;

    WeaponSwapFacility facility;
    WeaponAvatar avatar;
    Transform crystal, prompt, root;
    Renderer ring, range, column, crystalRenderer;
    Light lamp;
    Material crystalMaterial;
    float kick, phase;

    void Start()
    {
        facility = GetComponent<WeaponSwapFacility>();
        avatar = FindFirstObjectByType<WeaponAvatar>();
        phase = Random.value * 6.28f;
        Vector3 floor = new Vector3(transform.position.x, 0f, transform.position.z);
        root = new GameObject("FacilityFx").transform;
        root.SetParent(transform.parent, false);
        root.position = floor;
        ring = Flat("Ring", 3f, 0.05f);
        range = Flat("Range", StageRange * 2f, 0.04f);
        column = StageFx.MakeQuad("Column", EnemyTelegraphFx.GlowMaterial, floor + Vector3.up * 3f, Quaternion.identity).GetComponent<Renderer>();
        column.transform.SetParent(root, true); column.transform.localScale = new Vector3(1.4f, 6f, 1f);
        var crystalGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(crystalGo.GetComponent<Collider>());
        crystal = crystalGo.transform; crystal.SetParent(root, true); crystal.localScale = Vector3.one * 0.38f;
        crystalRenderer = crystalGo.GetComponent<Renderer>();
        crystalMaterial = new Material(Shader.Find("Sprites/Default"));
        crystalRenderer.sharedMaterial = crystalMaterial; crystalRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var lampGo = new GameObject("Light"); lampGo.transform.SetParent(root, false); lampGo.transform.position = floor + Vector3.up * 2f;
        lamp = lampGo.AddComponent<Light>(); lamp.type = LightType.Point; lamp.range = 7f; lamp.shadows = LightShadows.None; lamp.color = Idle;
        var label = new GameObject("Prompt"); label.transform.SetParent(root, false); label.transform.position = floor + Vector3.up * 2.9f;
        var text = label.AddComponent<TextMesh>();
        text.text = "[F] WEAPON SWAP"; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
        text.fontSize = 48; text.characterSize = 0.06f; text.color = Color.white;
        prompt = label.transform;
        facility.Opened += OnOpened;
        facility.WeaponReplaced += OnReplaced;
    }

    const float StageRange = WeaponSwapFacility.InteractionRadius;

    Renderer Flat(string name, float diameter, float height)
    {
        var go = StageFx.MakeQuad(name, StageFx.RingMaterial, root.position + Vector3.up * height, Quaternion.Euler(90f, 0f, 0f));
        go.transform.SetParent(root, true); go.transform.localScale = new Vector3(diameter, diameter, 1f);
        return go.GetComponent<Renderer>();
    }

    void OnDestroy()
    {
        if (crystalMaterial != null) Destroy(crystalMaterial);
        if (facility == null) return;
        facility.Opened -= OnOpened;
        facility.WeaponReplaced -= OnReplaced;
    }

    void OnOpened(WeaponAvatar user)
    {
        kick = Mathf.Max(kick, 0.6f);
        Vector3 floor = root.position;
        StageFx.Pulse(floor, Idle, 3.5f, 0.5f);
        StageFx.Burst(floor + Vector3.up * 1.6f, Idle, 16, 2.5f, 0.25f, 0.7f, -0.1f);
        StageFx.Play(ProceduralSfx.Snip, floor, 0.4f, 1.6f);
    }

    void OnReplaced(WeaponData data)
    {
        kick = 1f;
        Vector3 floor = root.position;
        Vector3 top = floor + Vector3.up * 1.7f;
        StageFx.Pulse(floor, UseColor, 5f, 0.7f);
        StageFx.Beam(floor, UseColor, 10f, 1.6f, 0.7f);
        StageFx.Burst(top, UseColor, 50, 6f, 0.35f, 1.0f, 0.3f);
        StageFx.Flash(top, UseColor, 5f, 12f, 0.6f);
        StageFx.Play(ProceduralSfx.Upgrade, floor, 0.8f, 1.35f);
        if (avatar != null) StageFx.Burst(avatar.transform.position + Vector3.up, UseColor, 30, 4f, 0.3f, 0.8f, -0.1f);
    }

    void Update()
    {
        float now = Time.time + phase;
        kick = Mathf.MoveTowards(kick, 0f, Time.unscaledDeltaTime * KickDecay);
        float distance = avatar != null ? Vector3.Distance(avatar.transform.position, root.position) : 99f;
        bool inRange = facility.InRange;
        float near = 1f - Mathf.Clamp01((distance - WeaponSwapFacility.InteractionRadius) / NearRange);
        Color tone = Color.Lerp(Idle, UseColor, kick);
        float pulse = 0.5f + 0.5f * Mathf.Sin(now * 3f);
        StageFx.Tint(ring, Alpha(tone, 0.45f + 0.35f * pulse + 0.4f * kick));
        StageFx.Tint(range, Alpha(tone, (0.15f + 0.3f * near) * (inRange ? 1.4f : 1f)));
        StageFx.Tint(column, Alpha(tone, 0.12f + 0.18f * near + 0.5f * kick));
        crystalMaterial.color = Alpha(Color.Lerp(tone, Color.white, 0.3f + kick * 0.5f), 0.85f);
        crystal.position = root.position + Vector3.up * (1.7f + Mathf.Sin(now * 2f) * 0.12f + kick * 0.3f);
        crystal.rotation = Quaternion.Euler(35f, now * (80f + 500f * kick), 35f);
        crystal.localScale = Vector3.one * (0.38f + 0.18f * kick);
        lamp.color = tone; lamp.intensity = 1.2f + 1.2f * near + 5f * kick;
        bool showPrompt = inRange && (avatar == null || !avatar.InteractionOpen);
        prompt.gameObject.SetActive(showPrompt);
        if (showPrompt && Camera.main != null) prompt.rotation = Camera.main.transform.rotation;
    }

    static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
}

using UnityEngine;

// Resolution-scaled combat bar, using the same IMGUI palette and Hangul font as WeaponHud.
public class AvatarHud : MonoBehaviour
{
    [SerializeField] WeaponAvatar avatar;
    Font font;
    GUIStyle titleStyle, bodyStyle, keyStyle;
    WeaponData lastWeapon;
    int lastSlot = -1;
    float specialUntil, noticeUntil, bossTrail = 1f, bossTrailHold, lastBossRatio = 1f;
    string acquisitionNotice;
    static readonly Color Gold = new Color(1f, 0.85f, 0.4f);
    static readonly Color Cyan = new Color(0.4f, 0.85f, 1f);
    static readonly Color[] BossPhaseColors = { new Color(0.95f, 0.45f, 0.12f), new Color(0.9f, 0.15f, 0.1f), new Color(0.75f, 0.2f, 0.95f) };
    public static float HudScale => Mathf.Min(Screen.height / 720f, Screen.width / 960f);
    public static float BarTop => Screen.height - 116f * HudScale;

    void Start() { if (avatar != null) avatar.WeaponAcquired += OnWeaponAcquired; }
    void OnWeaponAcquired(WeaponData data)
    {
        acquisitionNotice = data.displayName + " 획득 · F 시설에서 장착";
        noticeUntil = Time.unscaledTime + 5f;
    }
    void Update()
    {
        if (avatar == null || avatar.Weapon == null) return;
        if (lastWeapon != null && !avatar.IsDead && (lastWeapon != avatar.Weapon || lastSlot != avatar.ActiveSlot))
            specialUntil = Time.unscaledTime + 1.6f;
        lastWeapon = avatar.Weapon;
        lastSlot = avatar.ActiveSlot;
    }
    void OnDestroy()
    {
        if (avatar != null) avatar.WeaponAcquired -= OnWeaponAcquired;
        if (font != null) Destroy(font);
    }
    void OnGUI()
    {
        if (avatar == null || avatar.Weapon == null) return;
        EnsureStyles();
        float s = HudScale, x = (Screen.width - 652f * s) * 0.5f, y = BarTop;
        var moveset = avatar.Moveset;
        bool canAct = !avatar.IsDead && !avatar.InteractionOpen && moveset != null;
        bool free = canAct && !moveset.Busy;
        DrawHealth(new Rect(x, y - 30f * s, 652f * s, 22f * s));
        if (moveset == null || !moveset.Aiming) DrawCrosshair();
        float dash = avatar.DashCooldownRemaining, skill = avatar.SkillCooldownRemaining;
        DrawTile(new Rect(x, y, 104f * s, 100f * s), "RMB", "대시", dash > 0f ? $"{dash:F1}초" : "준비",
            moveset != null ? moveset.DashCooldownProgress : 0f, canAct && dash <= 0f, Cyan);
        x += 112f * s;
        DrawTile(new Rect(x, y, 148f * s, 100f * s), "E", moveset != null ? moveset.SkillName : "스킬",
            skill > 0f ? $"{skill:F1}초" : free ? "사용 가능" : "대기",
            moveset != null ? moveset.SkillCooldownProgress : 0f, free && skill <= 0f, Cyan);
        x += 156f * s;
        float ultimateLeft = moveset != null ? moveset.UltimateTimeRemaining : 0f;
        Color ultimateColor = Gold;
        if (avatar.UltimateReady && !avatar.IsDead)
            ultimateColor = Color.Lerp(Gold, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f));
        DrawTile(new Rect(x, y, 120f * s, 100f * s), "Q", "궁극기",
            ultimateLeft > 0f ? $"지속 {ultimateLeft:F1}초" : avatar.UltimateReady ? "사용 가능" : $"{avatar.UltimateGauge:P0}",
            avatar.UltimateGauge, canAct && (avatar.UltimateReady || ultimateLeft > 0f), ultimateColor);
        x += 128f * s;
        for (int i = 0; i < 2; i++)
        {
            var data = i < avatar.Slots.Length ? avatar.SlotWeapon(i) : null;
            bool active = i == avatar.ActiveSlot;
            DrawTile(new Rect(x, y, 124f * s, 100f * s), (i + 1).ToString(), ShortName(data),
                active && Time.unscaledTime < specialUntil ? "특수스킬!" : active ? "장착 중" : "교체",
                active ? 1f : 0f, active, Gold);
            x += 132f * s;
        }
        x = (Screen.width - 652f * s) * 0.5f;
        string status = moveset != null ? moveset.Status : "";
        if (moveset != null && moveset.ChargeProgress > 0f && !avatar.Weapon.hammer)
            status += moveset.ChargeProgress >= 1f ? " · 충전 완료! 놓아서 발사" : $" · 충전 {moveset.ChargeProgress:P0}";
        Label(new Rect(x, y - 58f * s, 652f * s, 24f * s), status, bodyStyle);
        if (!avatar.InteractionOpen)
        {
            string prompt = "";
            foreach (var facility in FindObjectsByType<WeaponSwapFacility>(FindObjectsSortMode.None))
                if (facility.InRange) { prompt = $"[F] 무기 교체 시설 · 슬롯 {avatar.ActiveSlot + 1} 교체"; break; }
            Label(new Rect(x, y - 86f * s, 652f * s, 24f * s), prompt, bodyStyle);
        }
        if (Time.unscaledTime < noticeUntil)
            Label(new Rect(x, y - (avatar.InteractionOpen ? 300f : 114f) * s, 652f * s, 28f * s), acquisitionNotice, titleStyle);
        var stage = FindFirstObjectByType<StageManager>();
        if (stage != null)
        {
            Label(new Rect(Screen.width * 0.15f, 16f * s, Screen.width * 0.7f, 48f * s), stage.Status, bodyStyle);
            if (stage.ActiveBoss != null) DrawBossHealth(stage.ActiveBoss, s);
            else if (stage.ActiveMidBoss != null) DrawMidBossHealth(stage.ActiveMidBoss, stage.ActiveArena == 1 ? "중간보스 · 건워든" : "중간보스 · 해머로드", s);
            if (stage.RunCleared)
            {
                var clear = new Rect(Screen.width * 0.25f, Screen.height * 0.25f, Screen.width * 0.5f, 100f * s);
                Fill(clear, new Color(0.05f, 0.06f, 0.09f, 0.9f));
                Label(clear, "스테이지 클리어!\n보스를 처치했습니다.", titleStyle);
            }
        }
    }
    public static string ShortName(WeaponData data)
    {
        if (data == null) return "빈 슬롯";
        if (data.shears) return "가위";
        if (data.chainsaw) return "전기톱";
        if (data.rifle) return "소총";
        if (data.hammer) return "망치";
        return data.displayName;
    }
    void DrawTile(Rect rect, string key, string name, string status, float fill, bool highlighted, Color accent)
    {
        float s = HudScale;
        Fill(rect, highlighted ? accent : new Color(0.2f, 0.23f, 0.3f, 0.9f));
        var inner = new Rect(rect.x + 2f * s, rect.y + 2f * s, rect.width - 4f * s, rect.height - 4f * s);
        Fill(inner, new Color(0.05f, 0.06f, 0.09f, 0.94f));
        float height = inner.height * Mathf.Clamp01(fill);
        Fill(new Rect(inner.x, inner.yMax - height, inner.width, height), new Color(accent.r, accent.g, accent.b, highlighted ? 0.22f : 0.09f));
        Label(new Rect(rect.x, rect.y + 5f * s, rect.width, 26f * s), key, keyStyle);
        Label(new Rect(rect.x + 5f * s, rect.y + 33f * s, rect.width - 10f * s, 30f * s), name, titleStyle);
        Label(new Rect(rect.x + 3f * s, rect.y + 70f * s, rect.width - 6f * s, 23f * s), status, bodyStyle);
    }
    void DrawHealth(Rect rect)
    {
        Fill(rect, new Color(0.08f, 0.08f, 0.1f, 0.9f));
        Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01((float)avatar.Hp / WeaponAvatar.MaxHp), rect.height),
            avatar.Invulnerable ? Gold : new Color(0.25f, 0.8f, 0.35f));
        Label(rect, avatar.IsDead ? "사망 · 2초 후 리스폰" : $"HP {avatar.Hp} / {WeaponAvatar.MaxHp}", bodyStyle);
    }
    void DrawBossHealth(Boss boss, float scale)
    {
        float ratio = Mathf.Clamp01(boss.HealthRatio);
        if (Event.current.type == EventType.Repaint)
        {
            // White trail holds briefly after a hit, then drains to the real value.
            float dt = Time.unscaledDeltaTime;
            if (ratio > bossTrail) bossTrail = ratio;
            else if (ratio < lastBossRatio) bossTrailHold = 0.4f;
            else if ((bossTrailHold -= dt) <= 0f) bossTrail = Mathf.MoveTowards(bossTrail, ratio, 0.5f * dt);
            lastBossRatio = ratio;
        }
        Color phaseColor = BossPhaseColors[Mathf.Clamp(boss.Phase - 1, 0, 2)];
        Color fill = boss.IsInvulnerable ? Cyan : phaseColor;
        var rect = new Rect((Screen.width - 560f * scale) * 0.5f, 84f * scale, 560f * scale, 22f * scale);
        titleStyle.normal.textColor = fill;
        Label(new Rect(rect.x, rect.y - 28f * scale, rect.width, 26f * scale), $"{Boss.DisplayName} · PHASE {boss.Phase} / 3", titleStyle);
        titleStyle.normal.textColor = Gold;
        float edge = 3f * scale;
        Fill(new Rect(rect.x - edge, rect.y - edge, rect.width + 2f * edge, rect.height + 2f * edge), new Color(0f, 0f, 0f, 0.9f));
        Fill(rect, new Color(0.08f, 0.08f, 0.1f, 0.95f));
        Fill(new Rect(rect.x, rect.y, rect.width * bossTrail, rect.height), new Color(1f, 1f, 1f, 0.85f));
        var bar = new Rect(rect.x, rect.y, rect.width * ratio, rect.height);
        Fill(bar, fill);
        Fill(new Rect(bar.x, bar.y, bar.width, bar.height * 0.35f), new Color(1f, 1f, 1f, 0.22f));
        // Notches mark where the next phase begins.
        foreach (float mark in new[] { Boss.PhaseTwoRatio, Boss.PhaseThreeRatio })
        {
            float nx = rect.x + rect.width * mark;
            Fill(new Rect(nx - scale, rect.y - edge, 2f * scale, rect.height + 2f * edge), mark < ratio ? new Color(1f, 1f, 1f, 0.9f) : new Color(0f, 0f, 0f, 0.8f));
        }
        Label(rect, boss.IsInvulnerable ? "포효 · 무적" : $"{boss.Health.Hp} / {boss.Health.MaxHp}", bodyStyle);
    }
    void DrawMidBossHealth(Enemy enemy, string title, float scale)
    {
        float ratio = Mathf.Clamp01((float)enemy.Hp / enemy.MaxHp);
        if (Event.current.type == EventType.Repaint)
        {
            float dt = Time.unscaledDeltaTime;
            if (ratio > bossTrail) bossTrail = ratio;
            else if (ratio < lastBossRatio) bossTrailHold = 0.4f;
            else if ((bossTrailHold -= dt) <= 0f) bossTrail = Mathf.MoveTowards(bossTrail, ratio, 0.5f * dt);
            lastBossRatio = ratio;
        }
        var rect = new Rect((Screen.width - 460f * scale) * 0.5f, 84f * scale, 460f * scale, 18f * scale);
        Label(new Rect(rect.x, rect.y - 28f * scale, rect.width, 26f * scale), title, titleStyle);
        float edge = 3f * scale;
        Fill(new Rect(rect.x - edge, rect.y - edge, rect.width + 2f * edge, rect.height + 2f * edge), new Color(0f, 0f, 0f, 0.9f));
        Fill(rect, new Color(0.08f, 0.08f, 0.1f, 0.95f));
        Fill(new Rect(rect.x, rect.y, rect.width * bossTrail, rect.height), new Color(1f, 1f, 1f, 0.85f));
        var bar = new Rect(rect.x, rect.y, rect.width * ratio, rect.height);
        Fill(bar, new Color(0.9f, 0.5f, 0.12f));
        Fill(new Rect(bar.x, bar.y, bar.width, bar.height * 0.35f), new Color(1f, 1f, 1f, 0.22f));
        Label(rect, $"{enemy.Hp} / {enemy.MaxHp}", bodyStyle);
    }
    static void Label(Rect rect, string text, GUIStyle style) => GUI.Label(rect, text, style);
    static void Fill(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
    static void DrawCrosshair()
    {
        float scale = Screen.height / 720f;
        float gap = 5f * scale, length = 9f * scale, thickness = 2f * scale;
        float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
        DrawBar(new Rect(cx - gap - length, cy - thickness * 0.5f, length, thickness), scale);
        DrawBar(new Rect(cx + gap, cy - thickness * 0.5f, length, thickness), scale);
        DrawBar(new Rect(cx - thickness * 0.5f, cy - gap - length, thickness, length), scale);
        DrawBar(new Rect(cx - thickness * 0.5f, cy + gap, thickness, length), scale);
        GUI.color = Color.white;
    }

    static void DrawBar(Rect rect, float outline)
    {
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(rect.x - outline, rect.y - outline, rect.width + 2f * outline, rect.height + 2f * outline), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
    }



    void EnsureStyles()
    {
        float s = HudScale;
        if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 16);
        if (bodyStyle == null)
        {
            bodyStyle = new GUIStyle { font = font, alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = Color.white } };
            titleStyle = new GUIStyle(bodyStyle) { fontStyle = FontStyle.Bold, normal = { textColor = Gold } };
            keyStyle = new GUIStyle(bodyStyle) { fontStyle = FontStyle.Bold, normal = { textColor = Cyan } };
        }
        bodyStyle.fontSize = Mathf.Max(1, Mathf.RoundToInt(14f * s));
        titleStyle.fontSize = Mathf.Max(1, Mathf.RoundToInt(17f * s));
        keyStyle.fontSize = Mathf.Max(1, Mathf.RoundToInt(19f * s));
    }
}

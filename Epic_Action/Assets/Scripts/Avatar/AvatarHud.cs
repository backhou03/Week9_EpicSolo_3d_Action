using System.Text;
using UnityEngine;

// Bottom-right panel for the weapon avatar (weapon, number-key slots, controls), drawn with IMGUI like WeaponHud.
public class AvatarHud : MonoBehaviour
{
    const string Controls = "WASD 이동 · Shift 질주 · Space 점프 · 좌클릭 공격 · 우클릭 대시 · E 스킬 · Q 궁극기 · 1·2 교체 · F 예비 교체";

    [SerializeField] WeaponAvatar avatar;

    Font font;
    GUIStyle titleStyle, bodyStyle, boxStyle;
    Texture2D background;
    int styledForHeight;
    readonly StringBuilder text = new StringBuilder();

    void OnDestroy()
    {
        if (font != null) Destroy(font);
        if (background != null) Destroy(background);
    }

    void OnGUI()
    {
        if (avatar == null || avatar.Weapon == null) return;
        EnsureStyles();
        if (avatar.Moveset == null || !avatar.Moveset.Aiming) DrawCrosshair();

        WeaponData weapon = avatar.Weapon;
        string title = weapon.displayName;

        text.Clear();
        var slots = avatar.Slots;
        for (int i = 0; i < slots.Length; i++)
        {
            WeaponData held = avatar.SlotWeapon(i);
            if (held == null) continue;
            string label = (i + 1) + " " + held.displayName;
            text.Append(held == weapon ? "[" + label + "]" : label).Append("  ");
        }
        text.Append("\nF 예비 (다음 →): ");
        foreach (var reserve in avatar.ReserveWeapons)
            if (reserve != null) text.Append(reserve.displayName).Append("  ");
        text.Append('\n').Append(Controls);
        text.Append('\n').Append($"E 스킬 쿨다운 {avatar.SkillCooldownRemaining:F1}초 · Q 궁극기 {avatar.UltimateGauge:P0} ({avatar.UltimateGauge:F2})");
        var moveset = avatar.Moveset;
        if (moveset != null)
        {
            if (!string.IsNullOrEmpty(moveset.Status)) text.Append('\n').Append(moveset.Status);
            float progress = moveset.ChargeProgress;
            if (progress > 0f && !weapon.hammer) text.Append(progress >= 1f ? "\n충전 완료! 버튼을 놓으면 발사" : $"\n충전 중 {progress:P0} · 도중에 놓으면 취소");
        }
        string body = text.ToString();

        float scale = Screen.height / 720f;
        float margin = 16f * scale, pad = 12f * scale, width = 470f * scale;
        float titleHeight = titleStyle.CalcHeight(new GUIContent(title), width - 2f * pad);
        float bodyHeight = bodyStyle.CalcHeight(new GUIContent(body), width - 2f * pad);
        float height = titleHeight + bodyHeight + 3f * pad;

        var rect = new Rect(Screen.width - width - margin, Screen.height - height - margin, width, height);
        GUI.Box(rect, GUIContent.none, boxStyle);
        GUI.Label(new Rect(rect.x + pad, rect.y + pad, width - 2f * pad, titleHeight), title, titleStyle);
        GUI.Label(new Rect(rect.x + pad, rect.y + 2f * pad + titleHeight, width - 2f * pad, bodyHeight), body, bodyStyle);
    }

    // Four ticks around the screen centre, which is where the rifle's aim ray points.
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
        if (styledForHeight == Screen.height && titleStyle != null) return;
        styledForHeight = Screen.height;
        float scale = Screen.height / 720f;

        // The built-in font has no Hangul; fall back to an OS font.
        if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 16);
        if (background == null)
        {
            background = new Texture2D(1, 1);
            background.SetPixel(0, 0, new Color(0.05f, 0.06f, 0.09f, 0.72f));
            background.Apply();
        }

        boxStyle = new GUIStyle { normal = { background = background } };
        titleStyle = new GUIStyle { font = font, fontSize = Mathf.RoundToInt(22f * scale), fontStyle = FontStyle.Bold, wordWrap = true, normal = { textColor = new Color(1f, 0.85f, 0.4f) } };
        bodyStyle = new GUIStyle { font = font, fontSize = Mathf.RoundToInt(15f * scale), wordWrap = true, normal = { textColor = Color.white } };
    }
}

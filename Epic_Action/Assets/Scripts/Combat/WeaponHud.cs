using UnityEngine;

// Bottom-right panel describing the weapon in hand (name, controls, traits), drawn with IMGUI.
public class WeaponHud : MonoBehaviour
{
    const string BareHandsName = "맨손";
    const string BareHandsText = "E: 근처 무기 줍기 (일반 자 / 일반 가위 / 일반 확성기 / 일반 네일건)";

    [SerializeField] PlayerCombat combat;

    Font font;
    GUIStyle titleStyle, bodyStyle, boxStyle;
    Texture2D background;
    int styledForHeight;

    void OnDestroy()
    {
        if (font != null) Destroy(font);
        if (background != null) Destroy(background);
    }

    void OnGUI()
    {
        if (combat == null) return;
        EnsureStyles();

        WeaponData weapon = combat.Weapon;
        if (weapon != null && weapon.nailgun && weapon.nailPiercing) DrawNailReticle();
        string title = weapon != null && !string.IsNullOrEmpty(weapon.displayName) ? weapon.displayName : BareHandsName;
        string body = weapon != null ? weapon.description : BareHandsText;

        if (!string.IsNullOrEmpty(combat.WeaponStatus)) body += "\n" + combat.WeaponStatus;

        if (combat.IsCharging)
        {
            float progress = combat.ChargeProgress;
            body += progress >= 1f ? "\n충전 완료! 좌클릭을 놓으면 발사" : $"\n충전 중 {progress:P0} · 도중에 놓으면 취소";
        }

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

    void DrawNailReticle()
    {
        float scale = Screen.height / 720f;
        float x = Screen.width * 0.5f, y = Screen.height * 0.5f;
        Color previous = GUI.color;
        GUI.color = new Color(0.55f, 0.95f, 1f, 0.9f);
        GUI.DrawTexture(new Rect(x - 9f * scale, y - scale, 5f * scale, 2f * scale), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + 4f * scale, y - scale, 5f * scale, 2f * scale), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - scale, y - 9f * scale, 2f * scale, 5f * scale), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - scale, y + 4f * scale, 2f * scale, 5f * scale), Texture2D.whiteTexture);
        GUI.color = previous;
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

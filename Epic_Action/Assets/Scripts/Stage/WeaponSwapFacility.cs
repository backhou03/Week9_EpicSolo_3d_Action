using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponSwapFacility : MonoBehaviour
{
    public const float InteractionRadius = 3f;
    public event Action<WeaponAvatar> Opened;
    public event Action<WeaponData> WeaponReplaced;
    WeaponAvatar avatar;
    bool open;
    Font font;
    GUIStyle buttonStyle, panelStyle, headingStyle;
    Vector2 cardsScroll;
    public bool InRange => avatar != null && !avatar.IsDead && Vector3.Distance(transform.position, avatar.transform.position) <= InteractionRadius;
    void Start() => avatar = FindFirstObjectByType<WeaponAvatar>();
    void Update()
    {
        if (!InRange) { Close(); return; }
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            if (open) Close();
            else { open = true; avatar.InteractionOpen = true; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; Opened?.Invoke(avatar); }
        }
    }
    void Close()
    {
        if (!open) return;
        open = false;
        avatar.InteractionOpen = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    void LateUpdate()
    {
        if (open) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
    void OnDisable() => Close();
    void OnDestroy() { if (font != null) Destroy(font); }
    void OnGUI()
    {
        if (!InRange) return;
        if (!open) return;
        if (font == null)
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 16);
            buttonStyle = new GUIStyle(GUI.skin.button) { font = font, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            panelStyle = new GUIStyle { font = font, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            headingStyle = new GUIStyle(panelStyle) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.85f, 0.4f) } };
        }
        float scale = AvatarHud.HudScale;
        buttonStyle.fontSize = Mathf.Max(1, Mathf.RoundToInt(15f * scale));
        panelStyle.fontSize = Mathf.Max(1, Mathf.RoundToInt(14f * scale));
        headingStyle.fontSize = Mathf.Max(1, Mathf.RoundToInt(18f * scale));
        var panel = new Rect((Screen.width - 700f * scale) * 0.5f, AvatarHud.BarTop - 270f * scale, 700f * scale, 180f * scale);
        Color previous = GUI.color;
        GUI.color = new Color(0.05f, 0.06f, 0.09f, 0.96f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = previous;
        GUI.Label(new Rect(panel.x, panel.y + 8f * scale, panel.width, 28f * scale), "무기 교체 시설 · F 닫기", headingStyle);
        string targets = "";
        for (int i = 0; i < avatar.Slots.Length && i < 2; i++)
            targets += (i == avatar.ActiveSlot ? "▶ " : "") + $"[{i + 1}] {AvatarHud.ShortName(avatar.SlotWeapon(i))}" + (i == avatar.ActiveSlot ? " (교체 대상)" : " (유지)") + "    ";
        GUI.Label(new Rect(panel.x, panel.y + 38f * scale, panel.width, 24f * scale), targets, panelStyle);
        GUI.Label(new Rect(panel.x, panel.y + 62f * scale, panel.width, 22f * scale), "카드 선택 시 대상 슬롯 교체 · 나간 무기는 보유 · 대상 변경: F 닫기 후 1 / 2", panelStyle);
        var viewport = new Rect(panel.x + 10f * scale, panel.y + 90f * scale, panel.width - 20f * scale, 82f * scale);
        cardsScroll = GUI.BeginScrollView(viewport, cardsScroll, new Rect(0, 0, Mathf.Max(viewport.width, avatar.OwnedWeapons.Count * 168f * scale), 60f * scale));
        int card = 0;
        bool previousEnabled = GUI.enabled;
        foreach (var data in avatar.OwnedWeapons)
        {
            bool carried = Array.IndexOf(avatar.Slots, data) >= 0;
            GUI.enabled = previousEnabled && !carried;
            string label = data.displayName + (carried ? $"\n슬롯 {Array.IndexOf(avatar.Slots, data) + 1} 장착 중" : $"\n슬롯 {avatar.ActiveSlot + 1}에 장착");
            if (GUI.Button(new Rect(card++ * 168f * scale, 0, 160f * scale, 60f * scale), label, buttonStyle) && avatar.ReplaceCurrentWeapon(data))
            {
                WeaponReplaced?.Invoke(data);
                Close();
            }
            GUI.enabled = previousEnabled;
        }
        GUI.EndScrollView();
    }
}

using System.IO;
using UnityEditor;
using UnityEngine;

// Builds only assets. It never opens a scene, changes selection, or rebuilds the user's scene.
[InitializeOnLoad]
public static class MegaphoneWeaponSetup
{
    const string Folder = "Assets/Data/Megaphones";
    const string PickupPath = "Assets/Resources/Weapons/MegaphonePickup.prefab";

    static MegaphoneWeaponSetup() { EditorApplication.update += EnsureAssets; }

    static void EnsureAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= EnsureAssets;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath) != null) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MegaphoneNormal/MegaphoneNormal_Horn/MegaphoneNormal_Horn.obj") == null) return;
        Build();
    }

    [MenuItem("Tools/Epic Action/Build Megaphone Assets")]
    public static void Build()
    {
        MegaphoneNormalSetup.Build();
        ScrapSonicSetup.Build();
        Directory.CreateDirectory(Folder);
        Directory.CreateDirectory("Assets/Resources/Weapons");
        AssetDatabase.Refresh();
        var normalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MegaphoneNormal/Generated/MegaphoneNormal.prefab");
        var evolvedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ScrapSonic/Generated/ScrapSonic.prefab");
        var evolved = MakeData("EvolvedMegaphone", evolvedPrefab, true, null);
        var normal = MakeData("NormalMegaphone", normalPrefab, false, evolved);
        var pickup = new GameObject("WeaponPickup_Megaphone");
        try
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(normalPrefab, pickup.transform);
            visual.name = "Visual";
            visual.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            visual.transform.localScale = Vector3.one * normal.heldScale;
            var component = pickup.AddComponent<WeaponPickup>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("data").objectReferenceValue = normal;
            serialized.FindProperty("visual").objectReferenceValue = visual.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(pickup, PickupPath);
        }
        finally { Object.DestroyImmediate(pickup); }
        AssetDatabase.SaveAssets();
        Debug.Log("Megaphone weapons ready: normal push wave / evolved crush wave. Pickup spawns at (0,0,4).");
    }

    static WeaponData MakeData(string name, GameObject prefab, bool evolved, WeaponData upgrade)
    {
        string path = Folder + "/" + name + ".asset";
        var data = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<WeaponData>();
            AssetDatabase.CreateAsset(data, path);
        }
        data.prefab = prefab;
        data.displayName = evolved ? "진화 확성기" : "일반 확성기";
        data.description = evolved
            ? "좌클릭 1.2초 유지 후 놓기: 차지 음파 발사 (전방 8m)\n맞은 적은 0.4초 뒤 폭발 · 발사 1회 후 파괴\n충전 도중 놓으면 취소\nE: 다른 무기 줍기 (교체)"
            : "좌클릭: 작은 음파 발사 (전방 4.5m, 맞은 적 넉백)\nQ: 진화 확성기로 진화\nE: 다른 무기 줍기 (교체)";
        data.heldScale = evolved ? 1.4f : 1.7f;
        data.gripLocalPosition = Vector3.zero;
        data.gripLocalEuler = Vector3.zero;
        data.restPosition = new Vector3(0.45f, 1f, 0.45f);
        data.restEuler = Vector3.zero;
        data.damage = 0;
        data.knockback = evolved ? 0f : 18f;
        data.sonic = true;
        data.sonicCrush = evolved;
        data.sonicChargeTime = evolved ? 1.2f : 0f;
        data.sonicSingleUse = evolved;
        data.sonicRange = evolved ? 8f : 4.5f;
        data.sonicHalfAngle = evolved ? 40f : 20f;
        data.sonicSpeed = evolved ? 24f : 18f;
        data.sonicMuzzle = new Vector3(0f, 0.27f, evolved ? 0.62f : 0.4f);
        data.maxHits = 0;
        data.hitStop = 0f;
        data.holdMoveScale = evolved ? 0.85f : 1f;
        data.swingMoveScale = evolved ? 0.4f : 0.75f;
        data.comboResetTime = 0.8f;
        data.upgradeTo = upgrade;
        data.combo.Clear();
        data.altCombo.Clear();
        data.combo.Add(new SwingData
        {
            windup = evolved ? 0.08f : 0.1f,
            strike = 0.08f,
            recovery = evolved ? 0.55f : 0.25f,
            chainAt = 0.85f,
            windupPosition = data.restPosition,
            windupEuler = Vector3.zero,
            strikePosition = data.restPosition + new Vector3(0f, 0.02f, evolved ? -0.2f : -0.09f),
            strikeEuler = new Vector3(evolved ? -10f : -4f, 0f, 0f),
            lunge = 0f,
            heavy = evolved,
        });
        EditorUtility.SetDirty(data);
        return data;
    }
}

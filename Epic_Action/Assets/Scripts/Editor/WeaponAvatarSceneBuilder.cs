using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

// Builds the scene where the weapon itself is the player. WeaponData is cloned into Assets/Data/Avatar and re-posed
// around the avatar's body (the originals stay as they are for the human-held scene).
public static partial class WeaponAvatarSceneBuilder
{
    const string ScenePath = "Assets/Scenes/WeaponAvatarScene.unity";
    const string DataDir = "Assets/Data";
    const string AvatarDir = "Assets/Data/Avatar";
    const string InputPath = "Assets/InputSystem_Actions.inputactions";
    const string RiflePath = "Assets/Weapons/M4_Casual/M4_Casual.obj";

    [MenuItem("Tools/Epic Action/Build Weapon Avatar Scene (resets Avatar WeaponData)")]
    public static void Build()
    {
        EditorSceneManager.SaveOpenScenes();
        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
        if (actions == null) throw new System.Exception("Missing " + InputPath);
        GameSceneBuilder.EnsureFolder(AvatarDir);

        var slots = BuildData();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var ground = GameSceneBuilder.MakeLit("Ground", new Color(0.62f, 0.7f, 0.58f), 0.1f, true);
        var wall = GameSceneBuilder.MakeLit("Wall", new Color(0.45f, 0.5f, 0.58f), 0.2f, false);

        var root = new GameObject("GameRoot").transform;
        var camRig = GameSceneBuilder.BuildCamera(actions);
        var avatar = BuildAvatar(root, actions, camRig, new[] { slots[0], slots[1] });

        var hudGo = new GameObject("AvatarHud");
        hudGo.transform.SetParent(root);
        GameSceneBuilder.Set(hudGo.AddComponent<AvatarHud>(), "avatar", avatar);

        var fxGo = new GameObject("HitFeedback");
        fxGo.transform.SetParent(root);
        GameSceneBuilder.Set(fxGo.AddComponent<HitFeedback>(), "cameraRig", camRig);

        BuildStage(root, avatar, slots, ground, wall);
        GameSceneBuilder.Set(camRig, "target", avatar.transform);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("Epic Action: weapon avatar scene built at " + ScenePath);
    }

    static WeaponAvatar BuildAvatar(Transform root, InputActionAsset actions, ThirdPersonCamera camRig, WeaponData[] slots)
    {
        var go = new GameObject("WeaponAvatar");
        go.transform.SetParent(root);
        go.transform.position = Vector3.zero;

        var cc = go.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 0.9f, 0f);

        var pose = new GameObject("Pose");
        pose.transform.SetParent(go.transform, false);
        var swinger = pose.AddComponent<WeaponSwinger>();
        var motion = new GameObject("Motion");
        motion.transform.SetParent(pose.transform, false);

        var avatar = go.AddComponent<WeaponAvatar>();
        GameSceneBuilder.Set(avatar, "actions", actions);
        GameSceneBuilder.Set(avatar, "cameraRig", camRig);
        GameSceneBuilder.Set(avatar, "swinger", swinger);
        GameSceneBuilder.Set(avatar, "motion", motion.transform);

        var so = new SerializedObject(avatar);
        var list = so.FindProperty("slots");
        list.arraySize = slots.Length;
        for (int i = 0; i < slots.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return avatar;
    }

    // ---- Data ------------------------------------------------------------

    static WeaponData[] BuildData()
    {
        var shears = EvolvedShears(Clone(DataDir + "/EvolvedShears.asset", "Avatar_EvolvedShears"));
        var chainsaw = Chainsaw(Clone(DataDir + "/ChainsawRuler.asset", "Avatar_ChainsawRuler"));
        var rifle = Rifle();
        var hammer = Hammer();
        foreach (var d in new[] { shears, chainsaw, rifle, hammer }) GameSceneBuilder.Save(d);
        return new[] { shears, chainsaw, rifle, hammer };
    }

    // Copies the original weapon; as an avatar it never breaks.
    static WeaponData Clone(string sourcePath, string name)
    {
        var source = AssetDatabase.LoadAssetAtPath<WeaponData>(sourcePath);
        if (source == null) throw new System.Exception("Missing " + sourcePath + " (build the original weapons first).");
        var d = GameSceneBuilder.LoadOrCreate<WeaponData>(AvatarDir + "/" + name + ".asset");
        EditorUtility.CopySerialized(source, d);
        d.name = name;
        d.maxHits = 0;
        d.upgradeTo = null;
        d.gripLocalPosition = Vector3.zero;
        d.gripLocalEuler = Vector3.zero;
        return d;
    }

    // Bounds (model space turned by `rotation`, unit scale) of the vertices of the OBJ objects ("o" lines) whose
    // name contains `partName`. Read from the file because Unity imports the whole OBJ as one mesh; the importer
    // mirrors x, so this does too.
    static Bounds PartBounds(string objPath, string partName, Quaternion rotation)
    {
        bool inPart = false, found = false;
        Bounds b = default;
        foreach (string line in System.IO.File.ReadLines(objPath))
        {
            if (line.StartsWith("o "))
            {
                inPart = line.Substring(2).Contains(partName);
                continue;
            }
            if (!inPart || !line.StartsWith("v ")) continue;
            var p = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            Vector3 v = rotation * new Vector3(-float.Parse(p[1], inv), float.Parse(p[2], inv), float.Parse(p[3], inv));
            if (found) b.Encapsulate(v);
            else b = new Bounds(v, Vector3.zero);
            found = true;
        }
        if (!found) throw new System.Exception("Missing model part " + partName + " in " + objPath);
        return b;
    }

    // Renderer bounds of the model in its own space (unit scale).
    static Bounds ModelBounds(GameObject prefab)
    {
        var temp = Object.Instantiate(prefab);
        try
        {
            temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            temp.transform.localScale = Vector3.one;
            var renderers = temp.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }
        finally
        {
            Object.DestroyImmediate(temp);
        }
    }
}

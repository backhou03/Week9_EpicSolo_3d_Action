using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

// Builds the scene where the weapon itself is the player. WeaponData is cloned into Assets/Data/Avatar and re-posed
// around the avatar's body (the originals stay as they are for the human-held scene).
public static class WeaponAvatarSceneBuilder
{
    const string ScenePath = "Assets/Scenes/WeaponAvatarScene.unity";
    const string DataDir = "Assets/Data";
    const string AvatarDir = "Assets/Data/Avatar";
    const string InputPath = "Assets/InputSystem_Actions.inputactions";
    const string RiflePath = "Assets/Weapons/M4_Casual/M4_Casual.obj";
    const string PistolPath = "Assets/Weapons/LowPoly_Pistol/LowPoly_Pistol.obj";
    const string PistolPrefabPath = "Assets/Weapons/LowPoly_Pistol/LowPoly_Pistol_Forward.prefab";
    const int EnemyCount = 7;

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
        var enemyMat = GameSceneBuilder.MakeLit("Enemy", new Color(0.9f, 0.22f, 0.2f), 0.4f, false);
        var visorMat = GameSceneBuilder.MakeLit("Visor", new Color(0.05f, 0.05f, 0.08f), 0.9f, false);
        var cutMat = GameSceneBuilder.MakeLit("CutFace", new Color(1f, 0.93f, 0.55f), 0.2f, false);
        var bloodMat = GameSceneBuilder.MakeLit("BloodCut", new Color(0.62f, 0.02f, 0.04f), 0.35f, false);

        var root = new GameObject("GameRoot").transform;
        GameSceneBuilder.BuildWorld(root, ground, wall);
        var camRig = GameSceneBuilder.BuildCamera(actions);
        var avatar = BuildAvatar(root, actions, camRig, slots);

        var hudGo = new GameObject("AvatarHud");
        hudGo.transform.SetParent(root);
        GameSceneBuilder.Set(hudGo.AddComponent<AvatarHud>(), "avatar", avatar);

        var fxGo = new GameObject("HitFeedback");
        fxGo.transform.SetParent(root);
        GameSceneBuilder.Set(fxGo.AddComponent<HitFeedback>(), "cameraRig", camRig);

        for (int i = 0; i < EnemyCount; i++) GameSceneBuilder.BuildEnemy(root, i, enemyMat, visorMat, cutMat, bloodMat);
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
        var chainsaw = Chainsaw(Clone(DataDir + "/ChainsawRuler.asset", "Avatar_ChainsawRuler"));
        var normalRuler = NormalRuler(Clone(DataDir + "/NormalRuler.asset", "Avatar_NormalRuler"));
        var normalShears = NormalShears(Clone(DataDir + "/NormalShears.asset", "Avatar_NormalShears"));
        var evolvedShears = EvolvedShears(Clone(DataDir + "/EvolvedShears.asset", "Avatar_EvolvedShears"));
        var pistol = Pistol();
        var rifle = Rifle();

        // Each slot starts at the first weapon; kills or damage unlock Q to evolve it.
        normalRuler.upgradeTo = chainsaw;
        normalShears.upgradeTo = evolvedShears;
        pistol.upgradeTo = rifle;

        var all = new List<WeaponData> { chainsaw, normalRuler, normalShears, evolvedShears, pistol, rifle };
        foreach (var d in all)
        {
            d.evolveKills = 5;
            d.evolveDamage = 150;
            GameSceneBuilder.Save(d);
        }
        return new[] { normalRuler, normalShears, pistol };
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

    // Poses are (Pose position, euler) in avatar space; the weapon's blade axis is its local +Y.
    // A horizontal sweep at waist height: euler (90, yaw, 0).

    static WeaponData NormalRuler(WeaponData d)
    {
        d.heldScale = 5f;
        d.hitRadius = 0.35f;
        d.restPosition = Vector3.zero;
        d.restEuler = Vector3.zero;
        d.combo.Clear();
        d.altCombo.Clear();
        // Falls forward onto the enemy, then a flat sweep.
        d.combo.Add(GameSceneBuilder.Swing(0.12f, 0.08f, 0.22f,
            new Vector3(0f, 0.1f, -0.2f), new Vector3(-35f, 0f, 0f),
            new Vector3(0f, 0.1f, 0.2f), new Vector3(95f, 0f, 0f), lunge: 0.8f));
        d.combo.Add(GameSceneBuilder.Swing(0.10f, 0.09f, 0.24f,
            new Vector3(0f, 0.9f, 0f), new Vector3(90f, 110f, 0f),
            new Vector3(0f, 0.9f, 0f), new Vector3(90f, -110f, 0f), lunge: 0.6f));
        return d;
    }

    // Stands on its chain edge with the bar pointing forward.
    static WeaponData Chainsaw(WeaponData d)
    {
        d.heldScale = 2.2f;
        d.hitRadius = 0.65f;
        // XY is the blade plane: +Y faces forward, -X chain runs along the floor, +Z faces right.
        d.restPosition = new Vector3(0f, 0.115f * d.heldScale, -0.7f);
        d.restEuler = new Vector3(0f, 90f, 90f);
        d.swingMoveScale = 0.4f;
        d.combo.Clear();
        d.altCombo.Clear();
        // Air attack starts behind: halfway through the body turn the diagonal blade crosses the front.
        d.altCombo.Add(GameSceneBuilder.Swing(0.12f, 0.29f, 0.22f,
            new Vector3(0f, 1.2f, -0.45f), new Vector3(55f, 180f, -35f),
            new Vector3(0f, 0.45f, -0.45f), new Vector3(115f, 180f, 35f),
            lunge: 1f, bodyYaw: 360f));
        d.altCombo[0].strikeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        // Plant the edge low, then kick hard off the ground into a diagonal spinning cut.
        d.combo.Add(GameSceneBuilder.Swing(0.16f, 0.30f, 0.30f,
            new Vector3(0f, d.restPosition.y, -0.3f), new Vector3(80f, -65f, -8f),
            new Vector3(0f, d.restPosition.y, -0.3f), new Vector3(110f, 65f, 30f),
            lunge: 1.4f, leap: 1.8f, chainAt: 0.8f, bodyYaw: 360f));
        d.combo[0].strikeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        d.combo.Add(GameSceneBuilder.Swing(0.11f, 0.12f, 0.22f,
            new Vector3(0f, d.restPosition.y, -0.3f), new Vector3(90f, -110f, 0f),
            new Vector3(0f, d.restPosition.y, -0.3f), new Vector3(90f, 110f, 0f), lunge: 0.7f, leap: 0.6f));
        d.combo.Add(GameSceneBuilder.Swing(0.13f, 0.14f, 0.24f,
            new Vector3(0f, d.restPosition.y, -0.3f), new Vector3(0f, 90f, 90f),
            new Vector3(0f, 1.1f, -0.3f), new Vector3(0f, 90f, -25f),
            lunge: 0.8f, stagger: 0.45f, damage: 1.2f, leap: 1.5f));
        // Give the windup bounce time to rise before driving the chain edge down ahead.
        d.combo.Add(GameSceneBuilder.Swing(0.45f, 0.22f, 0.38f,
            new Vector3(0f, d.restPosition.y, -0.6f), new Vector3(0f, 90f, -30f),
            new Vector3(0f, 0.115f * d.heldScale, 0.15f), new Vector3(0f, 90f, 90f),
            lunge: 1.2f, stagger: 0.6f, damage: 1.6f, knockback: 1.8f, hitStop: 1.5f, shake: 1.8f, heavy: true, chainAt: 0.8f));
        // Plant the edge during the fall, then keep it down for the rest of the strike.
        d.combo[3].strikeCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 4f),
            new Keyframe(0.6f, 1f, 0f, 0f), new Keyframe(1f, 1f, 0f, 0f));
        return d;
    }

    // Same stance as the evolved shears, on a smaller pair (about 1.1 m tall) with shorter lunges.
    static WeaponData NormalShears(WeaponData d)
    {
        d.heldScale = 7.5f;
        d.hitRadius = 0.25f;
        d.restPosition = new Vector3(0f, d.bladeTip.magnitude * d.heldScale, 0f);
        d.restEuler = new Vector3(0f, 90f, 180f);
        d.combo.Clear();
        d.altCombo.Clear();
        AddShearsCombo(d, 50f, 1.1f);
        return d;
    }

    // Stands on its blade tips like two legs (blades pointing down, spread front/back); the pivot is at hip height.
    static WeaponData EvolvedShears(WeaponData d)
    {
        d.heldScale = 2.3f;
        d.hitRadius = 0.3f;
        d.restPosition = new Vector3(0f, d.bladeTip.magnitude * d.heldScale, 0f);
        d.restEuler = new Vector3(0f, 90f, 180f);
        d.combo.Clear();
        d.altCombo.Clear();
        AddShearsCombo(d, 55f, 1.6f);
        return d;
    }

    // Left: open-bladed slashes (cut whatever they touch). Right: snips in the horizontal, vertical and diagonal planes.
    static void AddShearsCombo(WeaponData d, float open, float lunge)
    {
        d.combo.Add(GameSceneBuilder.Swing(0.09f, 0.10f, 0.20f,
            new Vector3(0f, 1f, 0f), new Vector3(90f, 110f, 0f),
            new Vector3(0f, 1f, 0f), new Vector3(90f, -70f, 0f),
            lunge: lunge, jawWindup: open, jawStrike: open, cutOnContact: true));
        d.combo.Add(GameSceneBuilder.Swing(0.09f, 0.10f, 0.20f,
            new Vector3(0f, 1f, 0f), new Vector3(90f, -110f, 0f),
            new Vector3(0f, 1f, 0f), new Vector3(90f, 70f, 0f),
            lunge: lunge, jawWindup: open, jawStrike: open, cutOnContact: true));
        d.combo.Add(GameSceneBuilder.Swing(0.14f, 0.30f, 0.40f,
            new Vector3(0f, 1f, 0f), new Vector3(90f, 130f, 0f),
            new Vector3(0f, 1f, 0f), new Vector3(90f, 0f, 0f),
            lunge: lunge * 1.4f, stagger: 0.6f, damage: 1.5f, knockback: 1.6f, hitStop: 1.5f, shake: 1.6f, heavy: true, chainAt: 0.8f,
            jawWindup: open, jawStrike: open, bodyYaw: 360f, leap: 1f, cutOnContact: true));
        d.combo[2].strikeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        d.altCombo.Add(GameSceneBuilder.Swing(0.10f, 0.07f, 0.20f,
            new Vector3(0f, 1f, 0.2f), new Vector3(90f, 0f, 0f),
            new Vector3(0f, 1f, 0.2f), new Vector3(90f, 0f, 0f),
            lunge: 0f, jawWindup: open, jawStrike: -30f));
        d.altCombo.Add(GameSceneBuilder.Swing(0.10f, 0.07f, 0.20f,
            new Vector3(0f, 1.1f, 0.2f), new Vector3(0f, 90f, 90f),
            new Vector3(0f, 1.1f, 0.2f), new Vector3(0f, 90f, 90f),
            lunge: 0f, jawWindup: open, jawStrike: -30f));
        d.altCombo.Add(GameSceneBuilder.Swing(0.12f, 0.07f, 0.26f,
            new Vector3(0f, 1.05f, 0.2f), new Vector3(45f, 90f, 90f),
            new Vector3(0f, 1.05f, 0.2f), new Vector3(45f, 90f, 90f),
            lunge: 0f, stagger: 0.45f, damage: 1.4f, knockback: 1.4f, hitStop: 1.3f, jawWindup: open + 5f, jawStrike: -30f));
    }

    // M4 rifle (model: +Y up, muzzle +Z, about 1 unit long). Lies level with the muzzle forward, resting on its
    // lowest point; the stock (-Z end) is the melee "blade".
    static WeaponData Rifle()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(RiflePath);
        if (model == null) throw new System.Exception("Missing " + RiflePath);
        Bounds b = ModelBounds(model);

        var d = GameSceneBuilder.LoadOrCreate<WeaponData>(AvatarDir + "/Avatar_Rifle.asset");
        d.displayName = "M4 소총";
        d.description = "";
        d.prefab = model;
        d.heldScale = 1.6f;
        d.gripLocalPosition = Vector3.zero;
        d.gripLocalEuler = Vector3.zero;
        d.restPosition = new Vector3(0f, -b.min.y * d.heldScale, 0f);
        d.restEuler = Vector3.zero;
        d.bladeBase = new Vector3(0f, b.center.y, b.min.z * 0.4f);
        d.bladeTip = new Vector3(0f, b.center.y, b.min.z);
        d.hitRadius = 0.3f;
        d.rifle = true;
        d.rifleMuzzle = new Vector3(0f, b.center.y + b.extents.y * 0.3f, b.max.z);
        Bounds aperture = PartBounds(RiflePath, "Rear_Aperture", Quaternion.identity);
        Bounds post = PartBounds(RiflePath, "Front_Sight_Post", Quaternion.identity);
        d.rifleSightRear = aperture.center;
        d.rifleSightFront = new Vector3(post.center.x, post.max.y, post.center.z);
        d.fireInterval = 0.09f;
        d.fireRange = 30f;
        d.rifleJumpShots = 4;
        d.rifleJumpHeight = 2.4f;
        d.rifleStandPivot = Vector3.zero;
        d.rifleStandPitch = -78f;
        d.rifleAimAssist = true;
        d.damage = 6;
        d.knockback = 3f;
        d.maxHits = 0;
        d.comboResetTime = 0.9f;
        d.swingMoveScale = 0.4f;
        d.hitStop = 0.05f;
        d.shake = 0.15f;
        d.fovPunch = 1.2f;
        d.trailColor = new Color(1f, 0.9f, 0.6f, 0.7f);
        d.upgradeTo = null;
        AddStockCombo(d);
        return d;
    }

    // Pistol (model muzzle -Z, about 0.2 m long), wrapped in a prefab turned to muzzle +Z so it poses like the rifle.
    // The back of the slide is the melee "blade".
    static WeaponData Pistol()
    {
        var rotation = Quaternion.Euler(0f, 180f, 0f);
        var model = PistolPrefab(rotation);
        Bounds b = ModelBounds(model);

        var d = GameSceneBuilder.LoadOrCreate<WeaponData>(AvatarDir + "/Avatar_Pistol.asset");
        d.displayName = "권총";
        d.description = "";
        d.prefab = model;
        d.heldScale = 7f;
        d.gripLocalPosition = Vector3.zero;
        d.gripLocalEuler = Vector3.zero;
        d.restPosition = new Vector3(0f, -b.min.y * d.heldScale, 0f);
        d.restEuler = Vector3.zero;
        d.bladeBase = new Vector3(0f, b.center.y, b.min.z * 0.4f);
        d.bladeTip = new Vector3(0f, b.center.y, b.min.z);
        d.hitRadius = 0.3f;
        d.rifle = true;
        d.rifleMuzzle = PartBounds(PistolPath, "13_Muzzle_Rim", rotation).center;
        Bounds rear = PartBounds(PistolPath, "17_Rear_Sight", rotation);
        Bounds front = PartBounds(PistolPath, "16_Front_Sight", rotation);
        d.rifleSightRear = rear.center;
        d.rifleSightFront = new Vector3(front.center.x, front.max.y, front.center.z);
        d.fireInterval = 0.4f;
        d.fireRange = 20f;
        d.rifleJumpShots = 1;
        d.rifleJumpHeight = 1.2f;
        Bounds mag = PartBounds(PistolPath, "07_Magazine_Base", rotation);
        d.rifleStandPivot = new Vector3(mag.center.x, mag.min.y, mag.center.z);
        d.rifleStandPitch = 0f;
        d.rifleAimAssist = false;
        d.damage = 12;
        d.knockback = 5f;
        d.maxHits = 0;
        d.comboResetTime = 0.9f;
        d.swingMoveScale = 0.4f;
        d.hitStop = 0.08f;
        d.shake = 0.25f;
        d.fovPunch = 1.8f;
        d.trailColor = new Color(1f, 0.9f, 0.6f, 0.7f);
        d.upgradeTo = null;
        AddStockCombo(d);
        return d;
    }

    // An empty root holding the pistol model turned by `rotation`.
    static GameObject PistolPrefab(Quaternion rotation)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(PistolPath);
        if (model == null) throw new System.Exception("Missing " + PistolPath);
        var root = new GameObject("LowPoly_Pistol_Forward");
        try
        {
            var child = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            child.transform.localRotation = rotation;
            return PrefabUtility.SaveAsPrefabAsset(root, PistolPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // Right click: a horizontal stock swing, then the stock rises and slams down ahead.
    static void AddStockCombo(WeaponData d)
    {
        d.combo.Clear();
        d.altCombo.Clear();
        d.altCombo.Add(GameSceneBuilder.Swing(0.08f, 0.10f, 0.20f,
            new Vector3(0f, 0.9f, 0f), new Vector3(0f, 90f, 0f),
            new Vector3(0f, 0.9f, 0f), new Vector3(0f, 270f, 0f), lunge: 0.8f, knockback: 1.6f));
        d.altCombo.Add(GameSceneBuilder.Swing(0.12f, 0.09f, 0.26f,
            new Vector3(0f, 0.9f, 0f), new Vector3(60f, 180f, 0f),
            new Vector3(0f, 0.6f, 0f), new Vector3(-40f, 180f, 0f),
            lunge: 1f, stagger: 0.55f, damage: 1.6f, knockback: 2f, hitStop: 1.4f, shake: 1.5f, heavy: true, chainAt: 0.7f));
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

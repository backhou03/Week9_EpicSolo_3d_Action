using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

public static class GameSceneBuilder
{
    const string DataDir = "Assets/Data";
    const string MaterialDir = "Assets/Materials";
    const string RulerDir = "Assets/RulerWeaponFinal/Generated";
    const string ChainsawDir = "Assets/RulerChainsaw";
    const string ShearsDir = "Assets/PinkingShearsFinal/Generated";
    const string InputPath = "Assets/InputSystem_Actions.inputactions";
    const int EnemyCount = 7;

    // Rebuilds the scene objects and resets both WeaponData assets to the values below.
    [MenuItem("Tools/Epic Action/Build Scene (resets WeaponData)")]
    public static void Build()
    {
        EnsureFolder(DataDir);
        EnsureFolder(MaterialDir);

        var normalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RulerDir + "/Ruler_Normal_30cm.prefab");
        var weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RulerDir + "/Ruler_Weapon_Final.prefab");
        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
        var chainsawPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChainsawDir + "/Generated/RulerChainsaw.prefab");
        if (normalPrefab == null || weaponPrefab == null) throw new System.Exception("Run Tools > Ruler Weapon > Build Prefabs first.");
        if (chainsawPrefab == null) throw new System.Exception("Run Tools > Ruler Chainsaw > Build Prefab first.");
        var normalShearsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShearsDir + "/PinkingShears_Normal.prefab");
        var evolvedShearsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShearsDir + "/PinkingShears_Evolved.prefab");
        if (normalShearsPrefab == null || evolvedShearsPrefab == null) throw new System.Exception("Run Tools > Pinking Shears Final > Build Prefabs first.");
        if (actions == null) throw new System.Exception("Missing " + InputPath);

        // ChainsawDriver reads the chain vertices at runtime.
        EnableRead(ChainsawDir + "/RulerChainsaw_Chain/RulerChainsaw_Chain.obj");

        var chainsaw = BuildChainsaw(chainsawPrefab);
        var upgraded = BuildUpgradedRuler(weaponPrefab, chainsaw);
        var normal = BuildNormalRuler(normalPrefab, upgraded);
        var evolvedShears = BuildEvolvedShears(evolvedShearsPrefab);
        var normalShears = BuildNormalShears(normalShearsPrefab, evolvedShears);

        var ground = MakeLit("Ground", new Color(0.62f, 0.7f, 0.58f), 0.1f, true);
        var wall = MakeLit("Wall", new Color(0.45f, 0.5f, 0.58f), 0.2f, false);
        var playerMat = MakeLit("Player", new Color(0.2f, 0.55f, 0.95f), 0.5f, false);
        var enemyMat = MakeLit("Enemy", new Color(0.9f, 0.22f, 0.2f), 0.4f, false);
        var visorMat = MakeLit("Visor", new Color(0.05f, 0.05f, 0.08f), 0.9f, false);
        var cutMat = MakeLit("CutFace", new Color(1f, 0.93f, 0.55f), 0.2f, false);
        var bloodMat = MakeLit("BloodCut", new Color(0.62f, 0.02f, 0.04f), 0.35f, false);

        var existing = GameObject.Find("GameRoot");
        if (existing != null) Object.DestroyImmediate(existing);
        var root = new GameObject("GameRoot").transform;

        BuildWorld(root, ground, wall);
        var camRig = BuildCamera(actions);
        var player = BuildPlayer(root, actions, camRig, playerMat, visorMat);

        var hudGo = new GameObject("WeaponHud");
        hudGo.transform.SetParent(root);
        Set(hudGo.AddComponent<WeaponHud>(), "combat", player.GetComponent<PlayerCombat>());

        var fxGo = new GameObject("HitFeedback");
        fxGo.transform.SetParent(root);
        Set(fxGo.AddComponent<HitFeedback>(), "cameraRig", camRig);

        BuildPickup(root, "WeaponPickup_Ruler", new Vector3(3f, 0f, 6f), 3f, normal, normalPrefab);
        BuildPickup(root, "WeaponPickup_Shears", new Vector3(-3f, 0f, 6f), 3f, normalShears, normalShearsPrefab);
        for (int i = 0; i < EnemyCount; i++) BuildEnemy(root, i, enemyMat, visorMat, cutMat, bloodMat);

        Set(camRig, "target", player.transform);

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Epic Action: scene built and saved.");
    }

    // ---- Weapons ---------------------------------------------------------

    static WeaponData BuildNormalRuler(GameObject prefab, WeaponData upgradeTo)
    {
        var d = LoadOrCreate<WeaponData>(DataDir + "/NormalRuler.asset");
        d.prefab = prefab;
        d.heldScale = 2.5f;
        d.displayName = "일반 자";
        d.description = "좌클릭: 베기 2연타 (내려찍기 → 가로 베기)\nQ: 무기 자로 진화\nE: 다른 무기 줍기 (교체)";
        d.gripLocalPosition = Vector3.zero;
        d.gripLocalEuler = Vector3.zero;
        d.restPosition = new Vector3(0.5f, 0.95f, 0.45f);
        d.restEuler = new Vector3(30f, 0f, -15f);
        d.bladeBase = Vector3.zero;
        d.bladeTip = new Vector3(0f, 0.32f, 0f);
        d.hitRadius = 0.25f;
        d.damage = 6;
        d.knockback = 5f;
        d.maxHits = 0;
        d.comboResetTime = 0.9f;
        d.hitStop = 0.05f;
        d.shake = 0.12f;
        d.fovPunch = 1f;
        d.upgradeTo = upgradeTo;
        d.combo.Clear();
        d.combo.Add(Swing(0.10f, 0.07f, 0.20f,
            new Vector3(0.5f, 1.3f, 0.15f), new Vector3(-70f, 0f, -10f),
            new Vector3(0.4f, 0.95f, 0.7f), new Vector3(105f, 0f, 0f), lunge: 0.6f));
        d.combo.Add(Swing(0.09f, 0.08f, 0.22f,
            new Vector3(0.8f, 1f, 0.3f), new Vector3(90f, 70f, 0f),
            new Vector3(0.2f, 1f, 0.6f), new Vector3(90f, -75f, 0f), lunge: 0.5f));
        Save(d);
        return d;
    }

    static WeaponData BuildChainsaw(GameObject prefab)
    {
        var d = LoadOrCreate<WeaponData>(DataDir + "/ChainsawRuler.asset");
        d.prefab = prefab;
        d.heldScale = 2.4f;
        d.displayName = "전기톱";
        d.description = "좌클릭: 3연타 (가로, 가로, 대각선 강타)\n닿을 때 버튼을 뗐으면 닿은 적이 즉시 두 동강\n닿을 때 꾹 누르고 있으면 0.15초 썰고, 반경 3m의 적이 한꺼번에 썰림\n첫 접촉 30회에 파괴\nE: 다른 무기 줍기 (교체)";
        d.gripLocalPosition = Vector3.zero;
        d.gripLocalEuler = Vector3.zero;
        d.restPosition = new Vector3(0.5f, 0.95f, 0.45f);
        d.restEuler = new Vector3(30f, 0f, -15f);
        d.bladeBase = new Vector3(0f, 0.15f, 0f);
        d.bladeTip = new Vector3(0f, 1.2f, 0f);
        d.hitRadius = 0.4f;
        d.trailColor = new Color(1f, 0.6f, 0.15f, 0.85f);
        d.damage = 12;
        d.knockback = 7f;
        d.maxHits = 30;
        d.comboResetTime = 0.9f;
        d.chainsaw = true;
        d.tickInterval = 0.05f;
        d.tickDamageScale = 0.1f;
        d.sliceOnKill = true;
        d.sliceDelay = 0.15f;
        d.sliceBurstRadius = 3f;
        d.instantSliceSpeed = 50f;
        d.holdMoveScale = 0.8f;
        d.swingMoveScale = 0.15f;
        d.turnScale = 0.6f;
        d.weightPull = 2.5f;
        d.weightLean = 12f;
        d.hitStop = 0.05f;
        d.shake = 0.2f;
        d.fovPunch = 2f;
        d.upgradeTo = null;
        d.combo.Clear();
        d.combo.Add(Swing(0.20f, 0.16f, 0.42f,
            new Vector3(0.9f, 1f, 0.2f), new Vector3(90f, 105f, 0f),
            new Vector3(0f, 1f, 0.7f), new Vector3(90f, -125f, 0f), lunge: 0.7f));
        d.combo.Add(Swing(0.20f, 0.16f, 0.42f,
            new Vector3(0f, 1f, 0.5f), new Vector3(90f, -120f, 0f),
            new Vector3(0.9f, 1f, 0.5f), new Vector3(90f, 110f, 0f), lunge: 0.7f));
        d.combo.Add(Swing(0.31f, 0.16f, 0.63f,
            new Vector3(0.9f, 1.7f, -0.1f), new Vector3(-45f, 0f, -40f),
            new Vector3(-0.1f, 0.95f, 0.9f), new Vector3(110f, 0f, 40f),
            lunge: 1.2f, stagger: 0.6f, damage: 1.6f, knockback: 1.8f, hitStop: 1.5f, shake: 1.8f, heavy: true, chainAt: 0.8f));
        Save(d);
        return d;
    }

    static WeaponData BuildUpgradedRuler(GameObject prefab, WeaponData upgradeTo)
    {
        var d = LoadOrCreate<WeaponData>(DataDir + "/UpgradedRuler.asset");
        d.prefab = prefab;
        d.heldScale = 1.8f;
        d.displayName = "무기 자";
        d.description = "좌클릭: 3연타 (대각선 → 가로 → 대각선 강타)\n맞은 적을 한 방에 처치\n첫 접촉 10회에 파괴\nQ: 전기톱으로 진화\nE: 다른 무기 줍기 (교체)";
        d.gripLocalPosition = Vector3.zero;
        d.gripLocalEuler = Vector3.zero;
        d.restPosition = new Vector3(0.5f, 0.95f, 0.45f);
        d.restEuler = new Vector3(30f, 0f, -15f);
        d.bladeBase = new Vector3(0f, -0.05f, 0f);
        d.bladeTip = new Vector3(0f, 1.03f, 0f);
        d.hitRadius = 0.3f;
        d.trailColor = new Color(0.85f, 0.95f, 1f, 0.8f);
        d.damage = 30;
        d.knockback = 8f;
        d.maxHits = 10;
        d.comboResetTime = 0.9f;
        d.hitStop = 0.08f;
        d.shake = 0.3f;
        d.fovPunch = 2.5f;
        d.chainsaw = false;
        d.tickInterval = 0f;
        d.sliceOnKill = false;
        d.upgradeTo = upgradeTo;
        d.combo.Clear();
        d.combo.Add(Swing(0.11f, 0.07f, 0.20f,
            new Vector3(0.9f, 1.45f, 0.1f), new Vector3(-40f, 0f, -35f),
            new Vector3(0.1f, 1f, 0.7f), new Vector3(100f, 0f, 35f), lunge: 0.8f, hitStop: 1.2f, shake: 1.2f));
        d.combo.Add(Swing(0.10f, 0.08f, 0.22f,
            new Vector3(0.9f, 1f, 0.2f), new Vector3(90f, 112f, 0f),
            new Vector3(0f, 1f, 0.7f), new Vector3(90f, -125f, 0f), lunge: 0.6f, hitStop: 1.2f, shake: 1.2f));
        d.combo.Add(Swing(0.22f, 0.09f, 0.45f,
            new Vector3(-0.3f, 1.7f, -0.1f), new Vector3(-45f, 0f, 40f),
            new Vector3(0.5f, 0.95f, 0.9f), new Vector3(105f, 0f, -40f),
            lunge: 1.2f, stagger: 0.6f, damage: 1.8f, knockback: 2f, hitStop: 1.8f, shake: 2f, heavy: true, chainAt: 0.8f));
        Save(d);
        return d;
    }

    // ---- Shears ----------------------------------------------------------
    // Poses are (hand pivot position, euler). The euler values orient the shears so that the blade axis and the
    // normal of the plane the blades lie in point the intended way (see PlayerCombat / ShearsDriver):
    //   horizontal plane (cuts at the waist): euler (90, yaw, 0)
    //   vertical plane   (splits left/right): euler (0, 90, roll)
    //   diagonal plane   (right-up to left-down): euler (45, 90, roll); mirrored: (-45, 90, roll)

    static WeaponData BuildNormalShears(GameObject prefab, WeaponData upgradeTo)
    {
        var d = LoadOrCreate<WeaponData>(DataDir + "/NormalShears.asset");
        d.prefab = prefab;
        d.heldScale = 3.45f;
        d.displayName = "일반 가위";
        d.description = "좌클릭: 날을 펼친 채 베기 3연타 (닿는 적이 두 동강)\n우클릭: 가위질 3연타 (날이 닫히는 순간 날 사이의 적이 두 동강)\nQ: 진화 가위로 진화\nE: 다른 무기 줍기 (교체)";
        // The hand holds the handle (about 7 cm below the pivot in model units).
        d.gripLocalPosition = new Vector3(0f, 0.07f * d.heldScale * 1.35f, 0f);
        d.gripLocalEuler = Vector3.zero;
        d.restPosition = new Vector3(0.5f, 0.95f, 0.45f);
        d.restEuler = new Vector3(30f, 0f, -15f);
        d.bladeBase = new Vector3(0.015f, 0.03f, 0f);
        d.bladeTip = new Vector3(0.059f, 0.138f, 0f);
        d.hitRadius = 0.12f;
        d.damage = 7;
        d.knockback = 4f;
        d.maxHits = 0;
        d.comboResetTime = 0.8f;
        d.tickInterval = 0f;
        d.sliceOnKill = true;
        d.instantSliceSpeed = 6f;
        d.shears = true;
        d.bloody = true;
        d.mirrorBlade = true;
        d.jawClosed = -23f;
        d.restJaw = 8f;
        d.hitStop = 0.05f;
        d.shake = 0.12f;
        d.fovPunch = 1f;
        d.trailColor = new Color(1f, 0.85f, 0.5f, 0.7f);
        d.upgradeTo = upgradeTo;
        d.combo.Clear();
        d.altCombo.Clear();
        // Left: open-bladed slashes (the blades stay spread and cut whatever they touch).
        d.combo.Add(Swing(0.08f, 0.09f, 0.18f,
            new Vector3(0.6f, 1f, 0f), new Vector3(90f, 100f, 0f),
            new Vector3(0.1f, 1f, 0.7f), new Vector3(90f, -60f, 0f),
            lunge: 0.7f, jawWindup: 45f, jawStrike: 45f, cutOnContact: true));
        d.combo.Add(Swing(0.08f, 0.09f, 0.18f,
            new Vector3(-0.5f, 1f, 0f), new Vector3(90f, -100f, 0f),
            new Vector3(0.1f, 1f, 0.7f), new Vector3(90f, 60f, 0f),
            lunge: 0.7f, jawWindup: 45f, jawStrike: 45f, cutOnContact: true));
        d.combo.Add(Swing(0.10f, 0.09f, 0.24f,
            new Vector3(0.7f, 1.5f, 0.1f), new Vector3(45f, 90f, 16f),
            new Vector3(0.1f, 0.95f, 0.8f), new Vector3(45f, 90f, 115f),
            lunge: 0.9f, stagger: 0.45f, damage: 1.4f, knockback: 1.4f, hitStop: 1.3f, jawWindup: 45f, jawStrike: 45f, cutOnContact: true));
        // Right: snips (the jaw snaps shut on whatever is between the blades).
        d.altCombo.Add(Swing(0.09f, 0.07f, 0.18f,
            new Vector3(0.2f, 1f, 0.65f), new Vector3(90f, 0f, 0f),
            new Vector3(0.2f, 1f, 0.65f), new Vector3(90f, 0f, 0f),
            lunge: 0f, jawWindup: 45f, jawStrike: -30f));
        d.altCombo.Add(Swing(0.09f, 0.07f, 0.18f,
            new Vector3(0.2f, 1.1f, 0.65f), new Vector3(0f, 90f, 90f),
            new Vector3(0.2f, 1.1f, 0.65f), new Vector3(0f, 90f, 90f),
            lunge: 0f, jawWindup: 45f, jawStrike: -30f));
        d.altCombo.Add(Swing(0.10f, 0.07f, 0.24f,
            new Vector3(0.2f, 1.05f, 0.65f), new Vector3(45f, 90f, 90f),
            new Vector3(0.2f, 1.05f, 0.65f), new Vector3(45f, 90f, 90f),
            lunge: 0f, stagger: 0.45f, damage: 1.4f, knockback: 1.4f, hitStop: 1.3f, jawWindup: 50f, jawStrike: -30f));
        Save(d);
        return d;
    }

    static WeaponData BuildEvolvedShears(GameObject prefab)
    {
        var d = LoadOrCreate<WeaponData>(DataDir + "/EvolvedShears.asset");
        d.prefab = prefab;
        d.heldScale = 2.07f;
        d.displayName = "진화 가위";
        d.description = "좌클릭: 날을 펼친 회전베기 3연타 (전진 베기 → 반대 베기 → 도약 360° 회전), 닿는 적이 두 동강\n우클릭: 가위질 3연타 (날이 닫히는 순간 날 사이의 적이 두 동강)\n첫 접촉 30회에 파괴\nE: 다른 무기 줍기 (교체)";
        d.gripLocalPosition = new Vector3(0f, 0.25f * d.heldScale * 1.35f, 0f);
        d.gripLocalEuler = Vector3.zero;
        d.restPosition = new Vector3(0.5f, 0.95f, 0.45f);
        d.restEuler = new Vector3(30f, 0f, -15f);
        d.bladeBase = new Vector3(0.05f, 0.1f, 0f);
        d.bladeTip = new Vector3(0.31f, 0.61f, 0.01f);
        d.hitRadius = 0.2f;
        d.damage = 14;
        d.knockback = 6f;
        d.maxHits = 30;
        d.comboResetTime = 0.9f;
        d.tickInterval = 0f;
        d.sliceOnKill = true;
        d.instantSliceSpeed = 6f;
        d.shears = true;
        d.bloody = true;
        d.mirrorBlade = true;
        d.jawClosed = -27f;
        d.restJaw = 10f;
        d.holdMoveScale = 0.9f;
        d.swingMoveScale = 0.25f;
        d.turnScale = 0.8f;
        d.weightPull = 1f;
        d.weightLean = 6f;
        d.hitStop = 0.06f;
        d.shake = 0.25f;
        d.fovPunch = 2f;
        d.trailColor = new Color(1f, 0.35f, 0.3f, 0.8f);
        d.upgradeTo = null;
        d.combo.Clear();
        d.altCombo.Clear();
        // Left, Riven-Q style: a forward slash, the reverse slash, then a leaping 360-degree spin slash, all with the
        // blades spread open (they cut whatever they touch).
        d.combo.Add(Swing(0.11f, 0.12f, 0.22f,
            new Vector3(0.7f, 1f, -0.1f), new Vector3(90f, 110f, 0f),
            new Vector3(0f, 1f, 0.7f), new Vector3(90f, -70f, 0f),
            lunge: 2.2f, jawWindup: 55f, jawStrike: 55f, cutOnContact: true));
        d.combo.Add(Swing(0.11f, 0.12f, 0.22f,
            new Vector3(-0.7f, 1f, -0.1f), new Vector3(90f, -110f, 0f),
            new Vector3(0f, 1f, 0.7f), new Vector3(90f, 70f, 0f),
            lunge: 2.2f, jawWindup: 55f, jawStrike: 55f, cutOnContact: true));
        d.combo.Add(Swing(0.14f, 0.30f, 0.40f,
            new Vector3(0.6f, 1f, -0.2f), new Vector3(90f, 130f, 0f),
            new Vector3(0f, 1f, 0.5f), new Vector3(90f, 0f, 0f),
            lunge: 3f, stagger: 0.6f, damage: 1.5f, knockback: 1.6f, hitStop: 1.5f, shake: 1.6f, heavy: true, chainAt: 0.8f,
            jawWindup: 55f, jawStrike: 55f, bodyYaw: 360f, leap: 1f, cutOnContact: true));
        // A steady turn rate for the spin slash (the default curve speeds up toward the end).
        d.combo[2].strikeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        // Right: snips (horizontal, vertical, diagonal planes).
        d.altCombo.Add(Swing(0.11f, 0.07f, 0.24f,
            new Vector3(0.2f, 1f, 0.65f), new Vector3(90f, 0f, 0f),
            new Vector3(0.2f, 1f, 0.65f), new Vector3(90f, 0f, 0f),
            lunge: 0f, jawWindup: 50f, jawStrike: -30f));
        d.altCombo.Add(Swing(0.11f, 0.07f, 0.24f,
            new Vector3(0.2f, 1.1f, 0.65f), new Vector3(0f, 90f, 90f),
            new Vector3(0.2f, 1.1f, 0.65f), new Vector3(0f, 90f, 90f),
            lunge: 0f, jawWindup: 50f, jawStrike: -30f));
        d.altCombo.Add(Swing(0.17f, 0.08f, 0.35f,
            new Vector3(0.2f, 1.05f, 0.65f), new Vector3(45f, 90f, 90f),
            new Vector3(0.2f, 1.05f, 0.65f), new Vector3(45f, 90f, 90f),
            lunge: 0f, stagger: 0.6f, damage: 1.5f, knockback: 1.6f, hitStop: 1.5f, shake: 1.6f, heavy: true, chainAt: 0.8f,
            jawWindup: 55f, jawStrike: -30f));
        Save(d);
        return d;
    }

    internal static SwingData Swing(float windup, float strike, float recovery,
        Vector3 windupPos, Vector3 windupEuler, Vector3 strikePos, Vector3 strikeEuler,
        float lunge, float stagger = 0.35f, float damage = 1f, float knockback = 1f,
        float hitStop = 1f, float shake = 1f, bool heavy = false, float chainAt = 0.45f,
        float jawWindup = 0f, float jawStrike = 0f, float bodyYaw = 0f, float leap = 0f, bool cutOnContact = false)
    {
        return new SwingData
        {
            windup = windup, strike = strike, recovery = recovery, chainAt = chainAt,
            windupPosition = windupPos, windupEuler = windupEuler,
            strikePosition = strikePos, strikeEuler = strikeEuler,
            lunge = lunge, stagger = stagger,
            damageScale = damage, knockbackScale = knockback,
            hitStopScale = hitStop, shakeScale = shake, heavy = heavy,
            jawWindup = jawWindup, jawStrike = jawStrike,
            bodyYaw = bodyYaw, leap = leap, cutOnContact = cutOnContact,
        };
    }

    // ---- Scene objects ---------------------------------------------------

    internal static void BuildWorld(Transform root, Material ground, Material wall)
    {
        var floor = Box(root, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(80f, 1f, 80f), ground);
        floor.GetComponent<Renderer>().sharedMaterial = ground;

        Box(root, "Wall_N", new Vector3(0f, 1.5f, 40.5f), new Vector3(82f, 3f, 1f), wall);
        Box(root, "Wall_S", new Vector3(0f, 1.5f, -40.5f), new Vector3(82f, 3f, 1f), wall);
        Box(root, "Wall_E", new Vector3(40.5f, 1.5f, 0f), new Vector3(1f, 3f, 82f), wall);
        Box(root, "Wall_W", new Vector3(-40.5f, 1.5f, 0f), new Vector3(1f, 3f, 82f), wall);

        Box(root, "Pillar_1", new Vector3(-8f, 2f, 10f), new Vector3(2f, 4f, 2f), wall);
        Box(root, "Pillar_2", new Vector3(10f, 1f, -6f), new Vector3(3f, 2f, 3f), wall);
        Box(root, "Pillar_3", new Vector3(-14f, 1.5f, -12f), new Vector3(2.5f, 3f, 2.5f), wall);
        Box(root, "Pillar_4", new Vector3(16f, 2f, 14f), new Vector3(2f, 4f, 2f), wall);
    }

    internal static ThirdPersonCamera BuildCamera(InputActionAsset actions)
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }
        cam.transform.SetParent(null);
        var rig = cam.GetComponent<ThirdPersonCamera>();
        if (rig == null) rig = cam.gameObject.AddComponent<ThirdPersonCamera>();
        Set(rig, "actions", actions);
        return rig;
    }

    static GameObject BuildPlayer(Transform root, InputActionAsset actions, ThirdPersonCamera camRig, Material body, Material visor)
    {
        var player = new GameObject("Player");
        player.transform.SetParent(root);
        player.transform.position = Vector3.zero;

        var cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.5f;
        cc.center = new Vector3(0f, 1f, 0f);

        MakeBody(player.transform, body, visor);

        var hand = new GameObject("HandPivot");
        hand.transform.SetParent(player.transform, false);
        hand.transform.localPosition = new Vector3(0.5f, 0.95f, 0.45f);
        var swinger = hand.AddComponent<WeaponSwinger>();

        var controller = player.AddComponent<PlayerController>();
        Set(controller, "actions", actions);
        Set(controller, "cameraRig", camRig);

        var combat = player.AddComponent<PlayerCombat>();
        Set(combat, "actions", actions);
        Set(combat, "player", controller);
        Set(combat, "cameraRig", camRig);
        Set(combat, "swinger", swinger);
        return player;
    }

    static void BuildPickup(Transform root, string name, Vector3 position, float visualScale, WeaponData data, GameObject prefab)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root);
        go.transform.position = position;

        var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, go.transform);
        visual.name = "Visual";
        visual.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        visual.transform.localScale = Vector3.one * visualScale;

        var pickup = go.AddComponent<WeaponPickup>();
        Set(pickup, "data", data);
        Set(pickup, "visual", visual.transform);
    }

    internal static void BuildEnemy(Transform root, int index, Material body, Material visor, Material cut, Material blood)
    {
        float angle = index * Mathf.PI * 2f / EnemyCount;
        float radius = 12f + (index % 3) * 4f;
        var go = new GameObject("Enemy_" + index);
        go.transform.SetParent(root);
        go.transform.position = new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);

        var cc = go.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.5f;
        cc.center = new Vector3(0f, 1f, 0f);

        var visual = MakeBody(go.transform, body, visor);
        var enemy = go.AddComponent<Enemy>();
        Set(enemy, "visual", visual.transform);
        Set(enemy, "bodyRenderer", visual.GetComponent<Renderer>());
        Set(enemy, "cutMaterial", cut);
        Set(enemy, "bloodMaterial", blood);
    }

    static GameObject MakeBody(Transform parent, Material body, Material visor)
    {
        var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.name = "Body";
        Object.DestroyImmediate(capsule.GetComponent<Collider>());
        capsule.transform.SetParent(parent, false);
        capsule.transform.localPosition = new Vector3(0f, 1f, 0f);
        capsule.GetComponent<Renderer>().sharedMaterial = body;

        var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eye.name = "Visor";
        Object.DestroyImmediate(eye.GetComponent<Collider>());
        eye.transform.SetParent(capsule.transform, false);
        eye.transform.localPosition = new Vector3(0f, 0.55f, 0.38f);
        eye.transform.localScale = new Vector3(0.7f, 0.3f, 0.35f);
        eye.GetComponent<Renderer>().sharedMaterial = visor;
        return capsule;
    }

    static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    // ---- Asset helpers ---------------------------------------------------

    internal static Material MakeLit(string name, Color color, float smoothness, bool checker)
    {
        string path = MaterialDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        if (checker)
        {
            m.SetTexture("_BaseMap", MakeChecker());
            m.SetTextureScale("_BaseMap", new Vector2(40f, 40f));
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Texture2D MakeChecker()
    {
        string path = MaterialDir + "/GroundChecker.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "GroundChecker", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = ((x / 32) + (y / 32)) % 2 == 0 ? new Color(1f, 1f, 1f) : new Color(0.82f, 0.82f, 0.82f);
        tex.SetPixels(pixels);
        tex.Apply();
        AssetDatabase.CreateAsset(tex, path);
        return tex;
    }

    internal static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    internal static void Save(Object o) => EditorUtility.SetDirty(o);

    static void EnableRead(string modelPath)
    {
        var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null) throw new System.Exception("Missing model " + modelPath);
        if (importer.isReadable) return;
        importer.isReadable = true;
        importer.SaveAndReimport();
    }

    internal static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'), System.IO.Path.GetFileName(path));
    }

    internal static void Set(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        if (prop == null) throw new System.Exception(target.GetType().Name + " has no serialized field '" + field + "'");
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}

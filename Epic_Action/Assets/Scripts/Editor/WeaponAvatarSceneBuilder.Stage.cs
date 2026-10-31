using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class WeaponAvatarSceneBuilder
{
    public const float RoadWidth = 12f;
    public const float ArenaWidth = 28f;
    const float Tile = 2f;
    static void BuildStage(Transform root, WeaponAvatar avatar, WeaponData[] weapons, Material floor, Material wall)
    {
        var stageRoot = new GameObject("Stage").transform;
        stageRoot.SetParent(root, false);
        var manager = stageRoot.gameObject.AddComponent<StageManager>();
        stageRoot.gameObject.AddComponent<StageFx>();
        stageRoot.gameObject.AddComponent<StageGuideFx>();
        var cells = new HashSet<Vector2Int>();
        AddArea(cells, -6, 6, -20, 8);
        AddArea(cells, -14, 14, 8, 36);
        AddArea(cells, -6, 6, 36, 58);
        AddArea(cells, -34, 34, 48, 58);
        AddArea(cells, -34, -22, 58, 68);
        AddArea(cells, 22, 34, 58, 68);
        AddArea(cells, -42, -14, 68, 96);
        AddArea(cells, 14, 42, 68, 96);
        AddArea(cells, -34, -22, 96, 118);
        AddArea(cells, 22, 34, 96, 118);
        AddArea(cells, -34, 34, 108, 118);
        AddArea(cells, -6, 6, 118, 130);
        AddArea(cells, -14, 14, 130, 158);
        BuildRoad(stageRoot, cells, floor, wall);
        avatar.transform.position = new Vector3(0, 0, -16);
        var doorMaterial = GameSceneBuilder.MakeLit("StageDoor", new Color(0.8f, 0.3f, 0.1f), 0.3f, false);
        var entrances = new StageDoor[4];
        var exits = new StageDoor[4];
        var centers = new[] { new Vector3(0,0,22), new Vector3(-28,0,82), new Vector3(28,0,82), new Vector3(0,0,144) };
        for (int i = 0; i < centers.Length; i++)
        {
            entrances[i] = MakeDoor(stageRoot, "Arena" + i + "_Entrance", centers[i] + Vector3.back * 14f, doorMaterial);
            exits[i] = MakeDoor(stageRoot, "Arena" + i + "_Exit", centers[i] + Vector3.forward * 14f, doorMaterial);
            var trigger = MakeTrigger(stageRoot, "Arena" + i + "_Entry", centers[i] + Vector3.back * 10f, new Vector3(28, 4, 2), manager);
            trigger.arena = i;
            if (i == 1 || i == 2) trigger.route = i - 1;
        }
        for (int i = 0; i < 2; i++)
        {
            float x = i == 0 ? -28 : 28;
            MakeTrigger(stageRoot, "Route" + i + "_Choice", new Vector3(x,0,63), new Vector3(12,4,2), manager).route = i;
            Sign(stageRoot, new Vector3(x,3,57), i == 0 ? "A: RIFLE" : "B: HAMMER");
        }
        MakeTrigger(stageRoot, "ForkCheckpoint", new Vector3(0,0,44), new Vector3(12,4,2), manager);
        var focus = new GameObject("MapRevealFocus").transform;
        focus.SetParent(stageRoot, false); focus.position = new Vector3(0,0,65);
        GameSceneBuilder.Set(manager, "avatar", avatar);
        GameSceneBuilder.Set(manager, "mapFocus", focus);
        SetObjects(manager, "entrances", entrances);
        SetObjects(manager, "exits", exits);
        SetObjects(manager, "rewards", new[] { weapons[2], weapons[3] });

        BuildEnemies(stageRoot, avatar);
        var waves = stageRoot.GetComponentInChildren<EnemyWaveSpawner>();
        SetBounds(waves, centers[0], new Vector2(12,12));
        GameSceneBuilder.Set(manager, "waveSpawner", waves);
        var source = new SerializedObject(waves);
        Enemy melee = (Enemy)source.FindProperty("meleeTemplate").objectReferenceValue;
        Enemy ranged = (Enemy)source.FindProperty("rangedTemplate").objectReferenceValue;
        var groups = new GameObject[3];
        for (int i = 0; i < 3; i++)
        {
            groups[i] = new GameObject(i == 2 ? "BossRoom" : "MidBoss" + i);
            groups[i].transform.SetParent(stageRoot, false);
            groups[i].SetActive(false);
            var boss = SpawnEnemy(melee, groups[i].transform, centers[i + 1], i == 2 ? Boss.MaxHealth : StageManager.MidBossHp, i == 2 ? 2.2f : 1.55f);
            // Bosses and midbosses take plain damage only, so slice/crush can't one-shot them.
            var health = new SerializedObject(boss);
            health.FindProperty("healthOnly").boolValue = true;
            health.ApplyModifiedPropertiesWithoutUndo();
            if (i == 2)
            {
                boss.name = "Boss_Crusher";
                Object.DestroyImmediate(boss.GetComponent<EnemyAttack>());
                var controller = boss.gameObject.AddComponent<Boss>();
                GameSceneBuilder.Set(controller, "summonTemplate", melee);
                var settings = new SerializedObject(controller);
                settings.FindProperty("arenaCenter").vector3Value = centers[i + 1];
                settings.ApplyModifiedPropertiesWithoutUndo();
                boss.gameObject.AddComponent<BossTelegraph>();
                GameSceneBuilder.Set(manager, "boss", controller);
                continue;
            }
            var so = new SerializedObject(boss.GetComponent<EnemyAttack>());
            so.FindProperty("damageMultiplier").floatValue = MidBoss.DamageMultiplier;
            so.ApplyModifiedPropertiesWithoutUndo();
            boss.gameObject.AddComponent<MidBoss>();
            for (int j = 0; j < 2; j++) SpawnEnemy(j % 2 == 0 ? melee : ranged, groups[i].transform, centers[i + 1] + new Vector3((j-1)*5,0,6), 45, 1f);
        }
        SetObjects(manager, "enemyGroups", groups);
        var approach = MakeTrigger(stageRoot, "RoadEncounter_Approach", new Vector3(0,0,-8), new Vector3(12,4,2), manager);
        approach.encounter = MakeEncounter(stageRoot, avatar, melee, ranged, new Vector3(0,0,-3), new Vector2(4,6));
        var merge = MakeTrigger(stageRoot, "RoadEncounter_Merge", new Vector3(0,0,121), new Vector3(12,4,2), manager);
        merge.encounter = MakeEncounter(stageRoot, avatar, melee, ranged, new Vector3(0,0,124), new Vector2(4,4));
        var facilityMaterial = GameSceneBuilder.MakeLit("Facility", new Color(0.08f, 0.2f, 0.24f), 0.6f, false);
        foreach (var position in new[] { new Vector3(4,0,-14), new Vector3(4,0,43), new Vector3(-32,0,65), new Vector3(32,0,65), new Vector3(-32,0,102), new Vector3(32,0,102), new Vector3(4,0,125) })
        {
            var go = StageBox(stageRoot, "WeaponSwapFacility", position + Vector3.up * 0.45f, new Vector3(1.3f,0.9f,1.3f), facilityMaterial);
            go.AddComponent<WeaponSwapFacility>();
            go.AddComponent<FacilityFx>();
        }
        Sign(stageRoot, new Vector3(0,3,-12), "FORWARD / STAGE 1");
    }
    static void AddArea(HashSet<Vector2Int> cells, int x0, int x1, int z0, int z1)
    {
        for (int x = x0 / 2; x < x1 / 2; x++) for (int z = z0 / 2; z < z1 / 2; z++) cells.Add(new Vector2Int(x,z));
    }
    static void BuildRoad(Transform root, HashSet<Vector2Int> cells, Material floor, Material wall)
    {
        var directions = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        foreach (var cell in cells)
        {
            var p = new Vector3(cell.x * Tile + 1, 0, cell.y * Tile + 1);
            StageBox(root, "Road", p + Vector3.down * 0.5f, new Vector3(Tile,1,Tile), floor);
            foreach (var d in directions)
            {
                if (cells.Contains(cell + d)) continue;
                StageBox(root, "RoadWall", p + new Vector3(d.x,3,d.y), d.x == 0 ? new Vector3(2.5f,6,0.5f) : new Vector3(0.5f,6,2.5f), wall);
            }
        }
    }
    static GameObject StageBox(Transform root, string name, Vector3 position, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(root, false); go.transform.position = position; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material; return go;
    }
    static StageDoor MakeDoor(Transform root, string name, Vector3 position, Material material)
    {
        var go = StageBox(root, name, position + Vector3.up * 3f, new Vector3(28,6,0.6f), material);
        return go.AddComponent<StageDoor>();
    }
    static StageTrigger MakeTrigger(Transform root, string name, Vector3 position, Vector3 size, StageManager manager)
    {
        var go = new GameObject(name); go.transform.SetParent(root, false); go.transform.position = position;
        var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.center = Vector3.up * 4; box.size = new Vector3(size.x,8f,size.z);
        var trigger = go.AddComponent<StageTrigger>(); trigger.manager = manager; return trigger;
    }
    static Enemy SpawnEnemy(Enemy template, Transform root, Vector3 position, int hp, float scale)
    {
        var enemy = Object.Instantiate(template, root); enemy.transform.position = position; enemy.transform.localScale = Vector3.one * scale;
        var so = new SerializedObject(enemy); so.FindProperty("maxHp").intValue = hp; so.ApplyModifiedPropertiesWithoutUndo();
        enemy.gameObject.SetActive(true); return enemy;
    }
    static EnemyWaveSpawner MakeEncounter(Transform root, WeaponAvatar avatar, Enemy melee, Enemy ranged, Vector3 center, Vector2 size)
    {
        var go = new GameObject("RoadEncounter"); go.transform.SetParent(root, false);
        var spawner = go.AddComponent<EnemyWaveSpawner>();
        GameSceneBuilder.Set(spawner, "target", avatar); GameSceneBuilder.Set(spawner, "meleeTemplate", melee); GameSceneBuilder.Set(spawner, "rangedTemplate", ranged);
        SetBounds(spawner, center, size); return spawner;
    }
    static void SetBounds(EnemyWaveSpawner spawner, Vector3 center, Vector2 size)
    {
        var so = new SerializedObject(spawner); so.FindProperty("arenaCenter").vector3Value = center; so.FindProperty("arenaHalfSize").vector2Value = size; so.ApplyModifiedPropertiesWithoutUndo();
    }
    static void SetObjects(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target); var list = so.FindProperty(field); list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; so.ApplyModifiedPropertiesWithoutUndo();
    }
    static void Sign(Transform root, Vector3 position, string label)
    {
        var go = new GameObject("RouteSign"); go.transform.SetParent(root, false); go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0,180,0);
        var text = go.AddComponent<TextMesh>(); text.text = label; text.characterSize = 0.35f; text.fontSize = 36; text.anchor = TextAnchor.MiddleCenter;
    }
}

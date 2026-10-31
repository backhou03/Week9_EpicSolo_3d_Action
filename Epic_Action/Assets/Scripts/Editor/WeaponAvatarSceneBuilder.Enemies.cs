using UnityEngine;
using UnityEditor;

public static partial class WeaponAvatarSceneBuilder
{
    static void BuildEnemies(Transform root, WeaponAvatar avatar)
    {
        var body = GameSceneBuilder.MakeLit("Enemy", new Color(0.9f, 0.22f, 0.2f), 0.4f, false);
        var rangedBody = GameSceneBuilder.MakeLit("EnemyRanged", new Color(0.4f, 0.3f, 0.9f), 0.4f, false);
        var visor = GameSceneBuilder.MakeLit("Visor", new Color(0.05f, 0.05f, 0.08f), 0.9f, false);
        var cut = GameSceneBuilder.MakeLit("CutFace", new Color(1f, 0.93f, 0.55f), 0.2f, false);
        var blood = GameSceneBuilder.MakeLit("BloodCut", new Color(0.62f, 0.02f, 0.04f), 0.35f, false);
        var go = new GameObject("EnemyWaveSpawner");
        go.transform.SetParent(root, false);
        var templates = new GameObject("EnemyTemplates");
        templates.transform.SetParent(go.transform, false);
        templates.SetActive(false);
        Enemy melee = BuildEnemyTemplate(templates.transform, EnemyAttackKind.Melee, body, visor, cut, blood);
        Enemy ranged = BuildEnemyTemplate(templates.transform, EnemyAttackKind.Ranged, rangedBody, visor, cut, blood);
        var spawner = go.AddComponent<EnemyWaveSpawner>();
        GameSceneBuilder.Set(spawner, "target", avatar);
        GameSceneBuilder.Set(spawner, "meleeTemplate", melee);
        GameSceneBuilder.Set(spawner, "rangedTemplate", ranged);
    }

    static Enemy BuildEnemyTemplate(Transform root, EnemyAttackKind kind, Material body, Material visor, Material cut, Material blood)
    {
        GameSceneBuilder.BuildEnemy(root, (int)kind, body, visor, cut, blood);
        var go = root.GetChild(root.childCount - 1).gameObject;
        go.name = "Enemy" + kind;
        go.transform.localPosition = Vector3.zero;
        go.SetActive(false);
        var enemy = go.GetComponent<Enemy>();
        var enemyObject = new SerializedObject(enemy);
        enemyObject.FindProperty("respawn").boolValue = false;
        enemyObject.ApplyModifiedPropertiesWithoutUndo();
        var attack = go.AddComponent<EnemyAttack>();
        var attackObject = new SerializedObject(attack);
        attackObject.FindProperty("kind").enumValueIndex = (int)kind;
        attackObject.ApplyModifiedPropertiesWithoutUndo();
        return enemy;
    }
}

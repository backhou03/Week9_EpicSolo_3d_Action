using System.Collections.Generic;
using UnityEngine;

public class EnemyWaveSpawner : MonoBehaviour
{
    public const int MaxAlive = 16;
    public const int WaveSize = 5;
    public const float WaveInterval = 7f;
    public const float SpawnRadius = 15f;
    const float ArenaLimit = 36f;
    const int SpawnAttempts = 20;

    [SerializeField] Enemy meleeTemplate;
    [SerializeField] Enemy rangedTemplate;
    [SerializeField] WeaponAvatar target;
    readonly List<Enemy> spawned = new List<Enemy>();
    float nextWave;
    int wave;
    public int AliveCount
    {
        get
        {
            int count = 0;
            foreach (var enemy in spawned) if (enemy != null && !enemy.IsDead) count++;
            return count;
        }
    }

    void Update()
    {
        spawned.RemoveAll(enemy => enemy == null);
        if (target == null || target.IsDead || Time.time < nextWave) return;
        nextWave = Time.time + WaveInterval;
        int count = Mathf.Min(WaveSize, MaxAlive - AliveCount);
        wave++;
        for (int i = 0; i < count; i++)
        {
            Enemy template = (i + wave) % 3 == 0 ? rangedTemplate : meleeTemplate;
            if (template == null || !TrySpawnPosition(out Vector3 position)) continue;
            var enemy = Instantiate(template, position, Quaternion.identity, transform);
            enemy.name = template.name + "_Wave" + wave;
            enemy.gameObject.SetActive(true);
            spawned.Add(enemy);
        }
    }

    bool TrySpawnPosition(out Vector3 position)
    {
        for (int i = 0; i < SpawnAttempts; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            position = target.transform.position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * SpawnRadius;
            position.x = Mathf.Clamp(position.x, -ArenaLimit, ArenaLimit);
            position.z = Mathf.Clamp(position.z, -ArenaLimit, ArenaLimit);
            position.y = 0f;
            if ((position - target.transform.position).sqrMagnitude < 36f) continue;
            if (!Physics.CheckCapsule(position + Vector3.up * 0.6f, position + Vector3.up * 1.5f, 0.55f, ~0, QueryTriggerInteraction.Ignore)) return true;
        }
        position = default;
        return false;
    }
}

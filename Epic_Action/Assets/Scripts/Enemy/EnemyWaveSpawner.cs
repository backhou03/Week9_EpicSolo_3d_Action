using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemyWaveSpawner : MonoBehaviour
{
    public const int MaxAlive = 16;
    public const int WaveSize = 5;
    public const float WaveInterval = 7f;
    public const float SpawnRadius = 10f;
    const int SpawnAttempts = 30;
    [SerializeField] Enemy meleeTemplate;
    [SerializeField] Enemy rangedTemplate;
    [SerializeField] WeaponAvatar target;
    [SerializeField] Vector3 arenaCenter;
    [SerializeField] Vector2 arenaHalfSize = new Vector2(12f, 12f);
    readonly List<Enemy> spawned = new List<Enemy>();
    float nextWave, interval;
    int totalWaves;
    bool running;
    public int WavesSpawned { get; private set; }
    public bool IsComplete => running && WavesSpawned >= totalWaves && AliveCount == 0;
    public event Action<int> WaveStarted;
    public int AliveCount
    {
        get { int count = 0; foreach (var enemy in spawned) if (enemy != null && !enemy.IsDead) count++; return count; }
    }
    public void BeginWaves(int count, float delay)
    {
        if (running) return;
        totalWaves = count;
        interval = delay;
        nextWave = Time.time;
        running = true;
    }
    void Update()
    {
        spawned.RemoveAll(enemy => enemy == null);
        if (!running || WavesSpawned >= totalWaves || target == null || target.IsDead || Time.time < nextWave || AliveCount > MaxAlive - WaveSize) return;
        int count = 0;
        for (int i = 0; i < WaveSize; i++)
        {
            Enemy template = (i + WavesSpawned) % 3 == 0 ? rangedTemplate : meleeTemplate;
            if (template == null || !TrySpawnPosition(out Vector3 position)) continue;
            var enemy = Instantiate(template, position, Quaternion.identity, transform);
            enemy.name = template.name + "_Wave" + (WavesSpawned + 1);
            enemy.gameObject.SetActive(true);
            spawned.Add(enemy);
            count++;
        }
        if (count == 0) { nextWave = Time.time + 0.5f; return; }
        WavesSpawned++;
        nextWave = Time.time + interval;
        WaveStarted?.Invoke(WavesSpawned);
    }
    bool TrySpawnPosition(out Vector3 position)
    {
        for (int i = 0; i < SpawnAttempts; i++)
        {
            position = arenaCenter + new Vector3(UnityEngine.Random.Range(-arenaHalfSize.x, arenaHalfSize.x), 0f, UnityEngine.Random.Range(-arenaHalfSize.y, arenaHalfSize.y));
            if ((position - target.transform.position).sqrMagnitude < 16f) continue;
            if (!Physics.CheckCapsule(position + Vector3.up * 0.6f, position + Vector3.up * 1.5f, 0.55f, ~0, QueryTriggerInteraction.Ignore)) return true;
        }
        position = default;
        return false;
    }
}

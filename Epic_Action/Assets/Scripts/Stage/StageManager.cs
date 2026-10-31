using System;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public class StageManager : MonoBehaviour
{
    public const int StageOneWaves = 6;
    public const float StageOneWaveInterval = 9f;
    public const float MapRevealDuration = 1.8f;
    public const float MapRevealHeight = 48f;
    const float RevealEase = 0.3f;
    const float RevealFov = 62f;
    public const int MidBossHp = 500;
    [SerializeField] WeaponAvatar avatar;
    [SerializeField] EnemyWaveSpawner waveSpawner;
    [SerializeField] StageDoor[] entrances;
    [SerializeField] StageDoor[] exits;
    [SerializeField] GameObject[] enemyGroups;
    [SerializeField] WeaponData[] rewards;
    [SerializeField] Transform mapFocus;
    [SerializeField] Boss boss;
    public Boss ActiveBoss => activeArena == 3 && boss != null && !boss.Health.IsDead ? boss : null;
    readonly bool[] cleared = new bool[4];
    Enemy midBoss;
    bool avatarWasDead;
    // Hit points of the midboss guarding the active arena, for the HUD bar.
    public Enemy ActiveMidBoss => midBoss != null && !midBoss.IsDead ? midBoss : null;
    public int ActiveArena => activeArena;
    int activeArena = -1;
    float revealStarted = -100f;
    public int SelectedRoute { get; private set; } = -1;
    public bool RunCleared { get; private set; }
    public bool MapRevealing => Time.unscaledTime - revealStarted < MapRevealDuration;
    public event Action<int> ArenaEntered;
    public event Action<int> ArenaCleared;
    public event Action MapRevealStarted;
    public event Action MapRevealEnded;
    public event Action<int> RouteSelected;
    public event Action<WeaponData> RewardAcquired;
    public event Action BossEntered;
    public event Action RunCompleted;
    public string Status => RunCleared ? "CLEAR - Boss defeated" : MapRevealing ? "Route revealed: left Rifle / right Hammer" : activeArena == 0 ? $"Stage 1 - waves {waveSpawner.WavesSpawned}/{StageOneWaves}, enemies {waveSpawner.AliveCount}" : activeArena > 0 ? (activeArena == 3 ? "Boss arena" : "Midboss arena") : "Advance along the road";
    void Start()
    {
        for (int i = 0; i < exits.Length; i++) { entrances[i].SetLocked(false); exits[i].SetLocked(true); }
    }
    public bool SelectRoute(int route)
    {
        if (!cleared[0] || route < 0 || route > 1) return false;
        if (SelectedRoute == route) return true;
        SelectedRoute = route;
        RouteSelected?.Invoke(route);
        return true;
    }
    public bool EnterArena(int index)
    {
        if (cleared[index]) return true;
        if (activeArena >= 0 || (index > 0 && !cleared[0]) || (index == 3 && !cleared[1] && !cleared[2])) return false;
        activeArena = index;
        entrances[index].SetLocked(true);
        exits[index].SetLocked(true);
        if (index == 0) waveSpawner.BeginWaves(StageOneWaves, StageOneWaveInterval);
        else enemyGroups[index - 1].SetActive(true);
        midBoss = null;
        if (index == 1 || index == 2)
        {
            var mid = enemyGroups[index - 1].GetComponentInChildren<MidBoss>(true);
            if (mid != null) midBoss = mid.GetComponent<Enemy>();
        }
        ArenaEntered?.Invoke(index);
        if (index == 3) BossEntered?.Invoke();
        return true;
    }
    void Update()
    {
        // After a death the entrance stays open so the fight can be re-entered from the checkpoint.
        if (avatar.IsDead && !avatarWasDead && activeArena >= 0) entrances[activeArena].SetLocked(false);
        avatarWasDead = avatar.IsDead;
        if (activeArena < 0) return;
        if (activeArena == 0) { if (!waveSpawner.IsComplete) return; }
        else if (activeArena == 3)
        {
            if (boss != null && !boss.Health.IsDead) return;
            if (boss != null) boss.ClearSummons();
        }
        else foreach (var enemy in enemyGroups[activeArena - 1].GetComponentsInChildren<Enemy>()) if (!enemy.IsDead) return;
        int completed = activeArena;
        cleared[completed] = true;
        activeArena = -1;
        midBoss = null;
        avatar.RestoreHealth();
        entrances[completed].SetLocked(false);
        exits[completed].SetLocked(false);
        ArenaCleared?.Invoke(completed);
        if (completed == 0)
        {
            RevealMap();
        }
        else if (completed < 3)
        {
            avatar.AcquireWeapon(rewards[completed - 1]);
            RewardAcquired?.Invoke(rewards[completed - 1]);
        }
        else { RunCleared = true; RunCompleted?.Invoke(); }
    }
    public void RevealMap()
    {
        revealStarted = Time.unscaledTime;
        MapRevealStarted?.Invoke();
    }
    void LateUpdate()
    {
        float elapsed = Time.unscaledTime - revealStarted;
        if (elapsed >= MapRevealDuration)
        {
            if (revealStarted > -50f) { revealStarted = -100f; MapRevealEnded?.Invoke(); }
            return;
        }
        var camera = avatar.CameraRig.transform;
        // Quick ease out, short hold on the fork, ease back to the player view.
        float t = elapsed / MapRevealDuration;
        float blend = Mathf.Clamp01(Mathf.Min(t / RevealEase, (1f - t) / RevealEase));
        blend = blend * blend * (3f - 2f * blend);
        Vector3 point = mapFocus.position;
        Vector3 drift = new Vector3(0f, 0f, Mathf.Lerp(-3f, 3f, t));
        camera.position = Vector3.Lerp(camera.position, point + new Vector3(0f, MapRevealHeight, -18f) + drift, blend);
        camera.rotation = Quaternion.Slerp(camera.rotation, Quaternion.LookRotation(point - camera.position), blend);
        var view = camera.GetComponent<Camera>();
        if (view != null) view.fieldOfView = Mathf.Lerp(view.fieldOfView, RevealFov, blend);
    }
}

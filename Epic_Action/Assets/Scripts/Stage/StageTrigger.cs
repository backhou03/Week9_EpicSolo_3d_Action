using UnityEngine;

public class StageTrigger : MonoBehaviour
{
    public StageManager manager;
    public int arena = -1;
    public int route = -1;
    public EnemyWaveSpawner encounter;
    bool visited;
    void OnTriggerEnter(Collider other)
    {
        var avatar = other.GetComponent<WeaponAvatar>();
        if (avatar == null || avatar.IsDead || visited) return;
        if (route >= 0 && !manager.SelectRoute(route)) return;
        if (arena >= 0 && !manager.EnterArena(arena)) return;
        visited = true;
        avatar.SetCheckpoint(transform.position, Quaternion.identity);
        if (encounter != null) encounter.BeginWaves(1, 0f);
    }
}

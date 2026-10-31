using UnityEngine;
using UnityEngine.SceneManagement;

// Adds the new pickup to existing game scenes without rebuilding their hand-authored contents.
public static class MegaphonePickupSpawner
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Object.FindFirstObjectByType<PlayerCombat>() == null || GameObject.Find("WeaponPickup_Megaphone") != null) return;
        var prefab = Resources.Load<GameObject>("Weapons/MegaphonePickup");
        if (prefab == null)
        {
            Debug.LogError("Megaphone pickup asset is missing. Run Tools/Epic Action/Build Megaphone Assets.");
            return;
        }
        var root = GameObject.Find("GameRoot");
        var pickup = Object.Instantiate(prefab, new Vector3(0f, 0f, 4f), Quaternion.identity, root != null ? root.transform : null);
        pickup.name = "WeaponPickup_Megaphone";
    }
}

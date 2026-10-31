using UnityEngine;
using UnityEngine.SceneManagement;

public static class NailgunPickupSpawner
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded-=OnSceneLoaded;
        SceneManager.sceneLoaded+=OnSceneLoaded;
    }
    static void OnSceneLoaded(Scene scene,LoadSceneMode mode)
    {
        if(Object.FindFirstObjectByType<PlayerCombat>()==null||GameObject.Find("WeaponPickup_Nailgun")!=null)return;
        var prefab=Resources.Load<GameObject>("Weapons/NailgunPickup");
        if(prefab==null){Debug.LogError("Nailgun asset missing. Run Tools/Epic Action/Build Nailgun Assets.");return;}
        var root=GameObject.Find("GameRoot");
        var pickup=Object.Instantiate(prefab,new Vector3(3f,0f,3f),Quaternion.identity,root!=null?root.transform:null);
        pickup.name="WeaponPickup_Nailgun";
    }
}

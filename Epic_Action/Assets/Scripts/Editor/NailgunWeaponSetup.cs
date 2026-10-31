using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class NailgunWeaponSetup
{
    const string Folder="Assets/Data/Nailguns";
    const string PickupPath="Assets/Resources/Weapons/NailgunPickup.prefab";
    const string ModelFolder="Assets/NailgunApproved/Generated/";
    static NailgunWeaponSetup(){EditorApplication.update+=EnsureAssets;}
    static void EnsureAssets()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        EditorApplication.update-=EnsureAssets;
        if(AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath)!=null)return;
        if(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/NailgunApproved/NailgunBlue_Normal_Body/NailgunBlue_Normal_Body.obj")==null)return;
        Build();
    }
    [MenuItem("Tools/Epic Action/Build Nailgun Assets")]
    public static void Build()
    {
        NailgunApprovedSetup.Build();
        Directory.CreateDirectory(Folder);
        Directory.CreateDirectory("Assets/Resources/Weapons");
        AssetDatabase.Refresh();
        var normalPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder+"NailgunBlue_Normal.prefab");
        var evolvedPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder+"NailgunBlue_Evolved.prefab");
        var projectile=BuildProjectile();
        var evolved=MakeData("EvolvedNailgun",evolvedPrefab,true,null,projectile);
        var normal=MakeData("NormalNailgun",normalPrefab,false,evolved,null);
        var pickup=new GameObject("WeaponPickup_Nailgun");
        try
        {
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(normalPrefab,pickup.transform);
            visual.name="Visual";
            visual.transform.localPosition=new Vector3(0f,0.7f,0f);
            visual.transform.localScale=Vector3.one*normal.heldScale;
            var serialized=new SerializedObject(pickup.AddComponent<WeaponPickup>());
            serialized.FindProperty("data").objectReferenceValue=normal;
            serialized.FindProperty("visual").objectReferenceValue=visual.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(pickup,PickupPath);
        }
        finally{Object.DestroyImmediate(pickup);}
        AssetDatabase.SaveAssets();
        Debug.Log("Nailguns ready: head contact 3-shot / torso charge piercing shot. Pickup at (3,0,3).");
    }
    static GameObject BuildProjectile()
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/NailgunApproved/NailgunBlue_Evolved_Nail/NailgunBlue_Evolved_Nail.obj");
        var holder=new GameObject("PiercingNailVisual");
        try
        {
            var model=(GameObject)PrefabUtility.InstantiatePrefab(source,holder.transform);
            var renderers=model.GetComponentsInChildren<MeshRenderer>();
            Bounds bounds=renderers[0].bounds;
            foreach(var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    string key=materials[i]!=null?materials[i].name.Replace(" (Instance)","").Replace(" (Material)",""):"MachinedSteel";
                    var replacement=AssetDatabase.LoadAssetAtPath<Material>(ModelFolder+key+".mat");
                    if(replacement!=null)materials[i]=replacement;
                }
                renderer.sharedMaterials=materials;
            }
            model.transform.localPosition=-bounds.center;
            return PrefabUtility.SaveAsPrefabAsset(holder,ModelFolder+"PiercingNail.prefab");
        }
        finally{Object.DestroyImmediate(holder);}
    }
    static WeaponData MakeData(string name,GameObject prefab,bool evolved,WeaponData upgrade,GameObject projectile)
    {
        string path=Folder+"/"+name+".asset";
        var data=AssetDatabase.LoadAssetAtPath<WeaponData>(path);
        if(data==null){data=ScriptableObject.CreateInstance<WeaponData>();AssetDatabase.CreateAsset(data,path);}
        data.prefab=prefab;
        data.displayName=evolved?"진화 네일건":"일반 네일건";
        data.description=evolved
            ?"좌클릭: 적 몸통에 밀착 사격 → 0.45초 충전 → 관통 못 발사\n우클릭: 밀착 없이 0.45초 충전 후 관통 못 발사 · 1발 후 총 파괴\n관통한 적은 몸에 구멍이 나며 즉사 · 사거리 22m\nE: 다른 무기 줍기 (교체)"
            :"좌클릭: 전방 6m 적에게 붙어 머리에 1발 발사\n클릭당 1발 · 3발 맞으면 사망\n0.5초 동안 추가 클릭이 없으면 뒤로 떨어짐\nQ: 진화 네일건으로 진화\nE: 다른 무기 줍기 (교체)";
        data.heldScale=evolved?1.2f:1.5f;
        data.gripLocalPosition=Vector3.zero;
        data.gripLocalEuler=Vector3.zero;
        data.restPosition=new Vector3(0.45f,1f,0.45f);
        data.restEuler=Vector3.zero;
        data.nailgun=true;data.nailPiercing=evolved;
        data.nailAttachRange=6f;data.nailDetachDelay=0.5f;data.nailChargeTime=0.45f;
        data.nailProjectileSpeed=75f;data.nailProjectileRange=22f;
        data.nailMuzzle=new Vector3(0f,evolved?0.35f:0.314f,evolved?1.095f:0.4175f);
        data.nailProjectilePrefab=projectile;
        data.damage=10;data.knockback=0f;data.maxHits=0;data.hitStop=0f;
        data.holdMoveScale=1f;data.swingMoveScale=1f;
        data.upgradeTo=upgrade;
        data.combo.Clear();data.altCombo.Clear();
        EditorUtility.SetDirty(data);
        return data;
    }
}

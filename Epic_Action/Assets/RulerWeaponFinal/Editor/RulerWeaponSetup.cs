using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
public static class RulerWeaponSetup
{
 const string Root="Assets/RulerWeaponFinal";
 [MenuItem("Tools/Ruler Weapon/Build Prefabs")]
 public static void Build() {
  string dest=Root+"/Generated";Directory.CreateDirectory(dest);AssetDatabase.Refresh();
  string[] names={"YellowPlastic","RulerPrint","DarkSteel","EdgeSteel","WeaponPrint"};
  Color[] colors={new Color(1,.66f,.055f),Color.white,new Color(.12f,.17f,.22f),new Color(.86f,.92f,.97f),Color.white};
  float[] metal={0,0,.65f,.9f,.35f};float[] smooth={.57f,.54f,.65f,.81f,.54f};
  var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)shader=Shader.Find("Standard");
  var mats=new Dictionary<string,Material>();
  for(int i=0;i<names.Length;i++) {
   string p=dest+"/"+names[i]+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);
   if(m==null){m=new Material(shader);AssetDatabase.CreateAsset(m,p);}m.shader=shader;m.color=colors[i];
   if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metal[i]);
   if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth[i]);
   if(m.HasProperty("_Glossiness"))m.SetFloat("_Glossiness",smooth[i]);
   string tex=i==1?Root+"/Ruler_Normal_30cm/Ruler_Albedo.png":i==4?Root+"/Ruler_Weapon_Final/RulerWeapon_Albedo.png":null;
   if(tex!=null)m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(tex);mats[names[i]]=m;EditorUtility.SetDirty(m);
  }
  foreach(string name in new[]{"Ruler_Normal_30cm","Ruler_Weapon_Final"}) {
   var source=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+name+"/"+name+".obj");
   if(source==null)throw new System.Exception("Missing model: "+name);
   var holder=new GameObject(name);
   try {
    var instance=(GameObject)PrefabUtility.InstantiatePrefab(source);instance.transform.SetParent(holder.transform,false);
    foreach(var r in instance.GetComponentsInChildren<MeshRenderer>()) {
     var shared=r.sharedMaterials;
     for(int i=0;i<shared.Length;i++) {
      string key=shared[i]!=null?shared[i].name.Replace(" (Instance)","").Replace(" (Material)",""):"";
      foreach(var pair in mats)if(key==pair.Key||key.EndsWith("_"+pair.Key)){shared[i]=pair.Value;break;}
     }
     r.sharedMaterials=shared;
    }
    PrefabUtility.SaveAsPrefabAsset(holder,dest+"/"+name+".prefab");
   } finally {Object.DestroyImmediate(holder);}
  }
  AssetDatabase.SaveAssets();Debug.Log("Ruler Weapon: prefabs created in "+dest);
 }
}

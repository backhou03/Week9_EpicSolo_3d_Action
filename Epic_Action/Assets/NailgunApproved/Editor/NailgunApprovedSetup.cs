using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Collections.Generic;
public static class NailgunApprovedSetup {
 const string Root="Assets/NailgunApproved";
 [Serializable] public class Spec {public string name;public float[] color,emission;public float metal,rough;}
 [Serializable] public class Specs {public Spec[] materials;}
 [MenuItem("Tools/Nailgun Approved/Build Prefabs")]
 public static void Build() {
  string dest=Root+"/Generated";Directory.CreateDirectory(dest);AssetDatabase.Refresh();var specs=JsonUtility.FromJson<Specs>(File.ReadAllText(Root+"/MaterialSpecs.json"));
  var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)shader=Shader.Find("Standard");var mats=new Dictionary<string,Material>();
  foreach(var s in specs.materials) {
   string path=dest+"/"+s.name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}m.shader=shader;m.color=new Color(s.color[0],s.color[1],s.color[2],1);
   if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",s.metal);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",1-s.rough);if(m.HasProperty("_Glossiness"))m.SetFloat("_Glossiness",1-s.rough);
   if(s.emission!=null&&s.emission.Length==3){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(s.emission[0]*2,s.emission[1]*2,s.emission[2]*2));}EditorUtility.SetDirty(m);mats[s.name]=m;
  }
  foreach(string stage in new[]{"NailgunBlue_Normal","NailgunBlue_Evolved"}) {
   var holder=new GameObject(stage);
   try {
    var suffixes=new List<string>{"Body","Contact","Magazine","Grip"};if(stage=="NailgunBlue_Evolved")suffixes.Add("Nail");if(stage=="NailgunBlue_Evolved")for(int i=1;i<=5;i++)suffixes.Add("Ammo"+i);
    foreach(string suffix in suffixes) {
     string part=stage+"_"+suffix;var model=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+part+"/"+part+".obj");if(model==null)throw new Exception("Missing model: "+part);
     var instance=(GameObject)PrefabUtility.InstantiatePrefab(model);instance.transform.SetParent(holder.transform,false);
     foreach(var r in instance.GetComponentsInChildren<MeshRenderer>()) {
      var shared=r.sharedMaterials;for(int i=0;i<shared.Length;i++){string key=shared[i]!=null?shared[i].name.Replace(" (Instance)","").Replace(" (Material)",""):"";foreach(var pair in mats)if(key==pair.Key||key.EndsWith("_"+pair.Key)){shared[i]=pair.Value;break;}}r.sharedMaterials=shared;
     }
    }
    PrefabUtility.SaveAsPrefabAsset(holder,dest+"/"+stage+".prefab");
   } finally {UnityEngine.Object.DestroyImmediate(holder);}
  }
  AssetDatabase.SaveAssets();Debug.Log("Normal and evolved nailgun prefabs created in "+dest);
 }
}

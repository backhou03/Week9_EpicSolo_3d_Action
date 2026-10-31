using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Collections.Generic;
public static class ScrapSonicSetup {
 const string Root="Assets/ScrapSonic";
 [Serializable] public class Spec { public string name,texture; public float[] color,emission; public float metal,rough; }
 [Serializable] public class Specs { public Spec[] materials; }
 [MenuItem("Tools/Scrap Sonic/Build Prefab")]
 public static void Build() {
  string dest=Root+"/Generated";Directory.CreateDirectory(dest);AssetDatabase.Refresh();
  var specs=JsonUtility.FromJson<Specs>(File.ReadAllText(Root+"/MaterialSpecs.json"));
  var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)shader=Shader.Find("Standard");var mats=new Dictionary<string,Material>();
  foreach(var s in specs.materials) {
   string path=dest+"/"+s.name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}m.shader=shader;m.color=new Color(s.color[0],s.color[1],s.color[2],1);
   if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",s.metal);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",1-s.rough);if(m.HasProperty("_Glossiness"))m.SetFloat("_Glossiness",1-s.rough);
   if(!string.IsNullOrEmpty(s.texture))m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/ScrapSonic_Electronics/"+s.texture);
   if(s.emission!=null&&s.emission.Length==3){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(s.emission[0]*2,s.emission[1]*2,s.emission[2]*2));}
   EditorUtility.SetDirty(m);mats[s.name]=m;
  }
  var holder=new GameObject("ScrapSonic");
  try {
   foreach(string suffix in new[]{"Shell","Cooling","Electronics","Wires","Grip"}) {
    string part="ScrapSonic_"+suffix;var model=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+part+"/"+part+".obj");if(model==null)throw new Exception("Missing model: "+part);
    var instance=(GameObject)PrefabUtility.InstantiatePrefab(model);instance.transform.SetParent(holder.transform,false);
    foreach(var r in instance.GetComponentsInChildren<MeshRenderer>()) {
     var shared=r.sharedMaterials;for(int i=0;i<shared.Length;i++){string key=shared[i]!=null?shared[i].name.Replace(" (Instance)","").Replace(" (Material)",""):"";foreach(var pair in mats)if(key==pair.Key||key.EndsWith("_"+pair.Key)){shared[i]=pair.Value;break;}}r.sharedMaterials=shared;
    }
   }
   PrefabUtility.SaveAsPrefabAsset(holder,dest+"/ScrapSonic.prefab");AssetDatabase.SaveAssets();
  } finally {UnityEngine.Object.DestroyImmediate(holder);}
  Debug.Log("Scrap Sonic prefab created in "+dest);
 }
}

using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
public static class PinkingShearsFinalSetup {
 const string Root="Assets/PinkingShearsFinal";
 [MenuItem("Tools/Pinking Shears Final/Build Prefabs")]
 public static void Build() {
  string dest=Root+"/Generated";Directory.CreateDirectory(dest);AssetDatabase.Refresh();
  string[] keys={"BladeHighlight","BladeFace","DarkSteel","WarmGrip","Rubber","Steel","SteelFrame","GroundSteel"};
  Color[] colors={new Color(.82f,.90f,.96f),new Color(.22f,.27f,.32f),new Color(.12f,.17f,.22f),new Color(1,.65f,.045f),new Color(.045f,.065f,.085f),new Color(.63f,.73f,.82f),new Color(.27f,.32f,.37f),new Color(.66f,.74f,.81f)};
  float[] metallic={.8f,.65f,.65f,0,0,.8f,.65f,.8f};float[] smooth={.77f,.57f,.65f,.5f,.18f,.75f,.6f,.7f};
  var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)shader=Shader.Find("Standard");
  var mats=new Dictionary<string,Material>();
  for(int i=0;i<keys.Length;i++) {
   string path=dest+"/"+keys[i]+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}mat.shader=shader;mat.color=colors[i];
   if(mat.HasProperty("_Metallic"))mat.SetFloat("_Metallic",metallic[i]);if(mat.HasProperty("_Smoothness"))mat.SetFloat("_Smoothness",smooth[i]);if(mat.HasProperty("_Glossiness"))mat.SetFloat("_Glossiness",smooth[i]);
   mats[keys[i]]=mat;EditorUtility.SetDirty(mat);
  }
  foreach(string name in new[]{"PinkingShears_Normal","PinkingShears_Evolved"}) {
   var holder=new GameObject(name);
   try {
    foreach(string suffix in new[]{"_HalfA","_HalfB","_Pivot"}) {
     string part=name+suffix;var source=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+part+"/"+part+".obj");
     if(source==null)throw new System.Exception("Missing part: "+part);
     var joint=new GameObject(suffix.Substring(1));joint.transform.SetParent(holder.transform,false);
     if(suffix!="_Pivot")joint.transform.localScale=new Vector3(1f,1.35f,1f);
     var instance=(GameObject)PrefabUtility.InstantiatePrefab(source);instance.transform.SetParent(joint.transform,false);
     foreach(var r in instance.GetComponentsInChildren<MeshRenderer>()) {
      var shared=r.sharedMaterials;
      for(int i=0;i<shared.Length;i++) {
       string key=shared[i]!=null?shared[i].name.Replace(" (Instance)","").Replace(" (Material)",""):"";
       foreach(var pair in mats)if(key==pair.Key||key.EndsWith("_"+pair.Key)){shared[i]=pair.Value;break;}
      }
      r.sharedMaterials=shared;
     }
    }
    PrefabUtility.SaveAsPrefabAsset(holder,dest+"/"+name+".prefab");
   } finally {Object.DestroyImmediate(holder);}
  }
  AssetDatabase.SaveAssets();Debug.Log("Pinking shears: two articulated prefabs created in "+dest);
 }
}

using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
public static class RulerChainsawSetup {
 const string Root="Assets/RulerChainsaw";
 [MenuItem("Tools/Ruler Chainsaw/Build Prefab")]
 public static void Build() {
  string dest=Root+"/Generated";Directory.CreateDirectory(dest);AssetDatabase.Refresh();
  string[] names={"DarkSteel","YellowPlastic","RulerFace","ChainSteel","Housing","BladeYellow","Rubber","GripRings","Steel","CuttingSteel"};
  Color[] colors={new Color(.12f,.17f,.22f),new Color(1,.66f,.055f),Color.white,new Color(.075f,.095f,.12f),new Color(.18f,.22f,.27f),new Color(.97f,.59f,.035f),new Color(.045f,.065f,.085f),new Color(.11f,.15f,.18f),new Color(.63f,.73f,.82f),new Color(.75f,.83f,.89f)};
  float[] metal={.65f,0,.15f,.6f,.6f,.25f,0,.1f,.8f,.8f};float[] smooth={.65f,.57f,.54f,.56f,.58f,.64f,.18f,.35f,.75f,.74f};
  var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)shader=Shader.Find("Standard");var materials=new Dictionary<string,Material>();
  for(int i=0;i<names.Length;i++) {
   string path=dest+"/"+names[i]+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}m.shader=shader;m.color=colors[i];
   if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metal[i]);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth[i]);if(m.HasProperty("_Glossiness"))m.SetFloat("_Glossiness",smooth[i]);
   if(names[i]=="RulerFace")m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/RulerChainsaw_Body/RulerChainsaw_Albedo.png");EditorUtility.SetDirty(m);materials[names[i]]=m;
  }
  var holder=new GameObject("RulerChainsaw");
  try {
   foreach(string part in new[]{"RulerChainsaw_Body","RulerChainsaw_Drive","RulerChainsaw_Chain"}) {
    var model=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+part+"/"+part+".obj");if(model==null)throw new System.Exception("Missing model: "+part);
    var instance=(GameObject)PrefabUtility.InstantiatePrefab(model);instance.transform.SetParent(holder.transform,false);
    foreach(var r in instance.GetComponentsInChildren<MeshRenderer>()) {
     var shared=r.sharedMaterials;
     for(int i=0;i<shared.Length;i++) {string key=shared[i]!=null?shared[i].name.Replace(" (Instance)","").Replace(" (Material)",""):"";foreach(var pair in materials)if(key==pair.Key||key.EndsWith("_"+pair.Key)){shared[i]=pair.Value;break;}}
     r.sharedMaterials=shared;
    }
   }
   PrefabUtility.SaveAsPrefabAsset(holder,dest+"/RulerChainsaw.prefab");AssetDatabase.SaveAssets();
  } finally {Object.DestroyImmediate(holder);}
  Debug.Log("Ruler chainsaw prefab created in "+dest);
 }
}

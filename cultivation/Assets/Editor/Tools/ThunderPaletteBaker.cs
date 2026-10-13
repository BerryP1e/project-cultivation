using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>只更新玄霄A2～A4的调色引用；A1和其他特效的共享原材质不修改。</summary>
public static class ThunderPaletteBaker
{
    const string Art = "Assets/Art/VFX/Project/XuanxiaoPalette";
    const string Shared = "Assets/Art/VFX/Shared/Materials/ExtremeFX/";
    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\','/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    public static void Collect(GameObject prefab, HashSet<GameObject> prefabs, HashSet<Material> materials)
    {
        if (!prefab || !prefabs.Add(prefab)) return;
        foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true)) foreach (var material in renderer.sharedMaterials) if (material) materials.Add(material);
        foreach (var spawner in prefab.GetComponentsInChildren<EFX_Spawner>(true)) Collect(spawner.ObjectSpawn, prefabs, materials);
        foreach (var explosion in prefab.GetComponentsInChildren<EFX_ExplosionObject>(true)) Collect(explosion.Objcet, prefabs, materials);
    }
    static Color TextureHue(Texture texture)
    {
        var rt = RenderTexture.GetTemporary(128,128,0); var prior = RenderTexture.active; var cpu = new Texture2D(128,128,TextureFormat.RGBA32,false);
        try
        {
            Graphics.Blit(texture,rt); RenderTexture.active=rt; cpu.ReadPixels(new Rect(0,0,128,128),0,0);cpu.Apply();
            Vector3 sum=Vector3.zero;float weight=0;
            foreach(var color in cpu.GetPixels())
            {
                float value=Mathf.Max(color.r,Mathf.Max(color.g,color.b)),low=Mathf.Min(color.r,Mathf.Min(color.g,color.b));
                if(value<.1f || value-low<.08f)continue;
                float w=value-low;sum+=new Vector3(color.r,color.g,color.b)*(w/value);weight+=w;
            }
            if(weight<=0)return new Color(.45f,.65f,1);
            sum/=weight;return new Color(sum.x,sum.y,sum.z,1);
        }
        finally{RenderTexture.active=prior;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(cpu);}
    }
    [MenuItem("修仙/战斗/统一玄霄四式为A1雷色（不动场景）")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("请退出Play模式");
        Folder(Art);
        var catalog=Resources.Load<CombatVfxCatalog>("Combat/CombatVfxCatalog");
        var reference=catalog.查找("thunder_a1").预制体;
        var blueSpark=AssetDatabase.LoadAssetAtPath<Material>(Shared+"sparker_blue.mat");
        var blueFlash=AssetDatabase.LoadAssetAtPath<Material>(Shared+"Flash_blue.mat");
        Color hue=TextureHue(blueSpark.mainTexture);
        var materials=new HashSet<Material>();var prefabs=new HashSet<GameObject>();
        foreach(var id in new[]{"thunder_a2","thunder_a3","thunder_a4"})Collect(catalog.查找(id).预制体,prefabs,materials);
        string path="Assets/resources/Combat/XuanxiaoA1Palette.asset";
        var palette=AssetDatabase.LoadAssetAtPath<CombatVfxPalette>(path);
        if(!palette){palette=ScriptableObject.CreateInstance<CombatVfxPalette>();AssetDatabase.CreateAsset(palette,path);}
        var mappings=new List<CombatVfxPalette.Replacement>();
        foreach(var source in materials.OrderBy(AssetDatabase.GetAssetPath))
        {
            // 中性的烟尘与地面碎片保留原表现；所有发光/闪电/裂纹包含动态子效果。
            if(source.name=="Smoke"||source.name=="ground"||source==blueSpark||source==blueFlash)continue;
            Material target;
            if(source.name=="sparker")target=blueSpark;
            else if(source.name=="Flash")target=blueFlash;
            else
            {
                string output=Art+"/"+source.name+"_A1.mat";
                var clone=new Material(Shader.Find("Cultivation/Combat/VfxPaletteAdditive")){name=source.name+"_A1",mainTexture=source.mainTexture};
                clone.mainTextureScale=source.mainTextureScale;clone.mainTextureOffset=source.mainTextureOffset;
                clone.SetColor("_TintColor",source.HasProperty("_TintColor")?source.GetColor("_TintColor"):Color.white);
                clone.SetColor("_TargetColor",hue);clone.SetFloat("_Intensity",source.shader.name.StartsWith("Mobile/")?1:2);
                target=AssetDatabase.LoadAssetAtPath<Material>(output);
                if(target){EditorUtility.CopySerialized(clone,target);EditorUtility.SetDirty(target);UnityEngine.Object.DestroyImmediate(clone);}
                else{AssetDatabase.CreateAsset(clone,output);target=clone;}
            }
            mappings.Add(new CombatVfxPalette.Replacement{source=source,target=target});
        }
        palette.materials=mappings.ToArray();palette.lightColor=reference.GetComponentInChildren<Light>(true).color;
        EditorUtility.SetDirty(palette);
        catalog.查找("thunder_a1").调色=null;
        foreach(var id in new[]{"thunder_a2","thunder_a3","thunder_a4"})catalog.查找(id).调色=palette;
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        Debug.Log("[玄霄调色] A2～A4含动态子效果使用A1色；材质映射"+mappings.Count+"，原生资源和特效大小/时序不变");
    }
}

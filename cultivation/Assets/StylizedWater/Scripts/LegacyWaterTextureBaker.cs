#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Deterministic replacement outputs for the removed Built-in Substance importer.</summary>
public static class LegacyWaterTextureBaker
{
    const string Folder = "Assets/StylizedWater/Compatibility";

    [MenuItem("Tools/Cultivation/画面试验/修复旧水缺失的 Substance 贴图")]
    public static void BakeMissingOutputs()
    {
        Directory.CreateDirectory(Folder);
        const int size = 256;
        var normal = new Texture2D(size,size,TextureFormat.RGB24,false,true);
        var masks = new Texture2D(size,size,TextureFormat.RGB24,false,true);
        try
        {
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float u=x/(float)size, v=y/(float)size, du=1f/size;
                float dx=(Height(u+du,v)-Height(u-du,v))*.07f/(2*du);
                float dy=(Height(u,v+du)-Height(u,v-du))*.07f/(2*du);
                var n=new Vector3(-dx,-dy,1).normalized;
                normal.SetPixel(x,y,new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f));
                float ripple=Height(u,v)*.5f+.5f;
                float foam=Mathf.SmoothStep(.1f,.85f,Height(v+.31f,u+.19f)*.5f+.5f);
                // The legacy shader reads R for moving highlights and B for shoreline foam.
                masks.SetPixel(x,y,new Color(ripple,0,foam));
            }
            normal.Apply();masks.Apply();
            Write("LegacyWater_Normal.png",normal,true);
            Write("LegacyWater_ShaderMap.png",masks,false);
        }
        finally {Object.DestroyImmediate(normal);Object.DestroyImmediate(masks);}
        var normalAsset=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/LegacyWater_Normal.png");
        var maskAsset=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/LegacyWater_ShaderMap.png");
        int repaired=0;
        foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/StylizedWater"}))
        {
            var mat=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if(!mat || !mat.shader || !mat.shader.name.StartsWith("StylizedWater/")) continue;
            bool changed=false;
            if(mat.HasProperty("_Normals") && !mat.GetTexture("_Normals")){mat.SetTexture("_Normals",normalAsset);changed=true;}
            if(mat.HasProperty("_Shadermap") && !mat.GetTexture("_Shadermap")){mat.SetTexture("_Shadermap",maskAsset);changed=true;}
            if(changed){EditorUtility.SetDirty(mat);AssetDatabase.SaveAssetIfDirty(mat);repaired++;}
        }
        Debug.Log("Legacy water: repaired missing texture outputs on "+repaired+" materials. Original Substance source retained.");
    }

    static float Height(float u,float v)
    {
        float p=Mathf.PI*2;
        return Mathf.Sin(p*(u*3+v*2))*.45f+Mathf.Sin(p*(u*5-v*3)+1.1f)*.3f+Mathf.Cos(p*(u*9+v*7)+.7f)*.15f+Mathf.Sin(p*(u*13-v*11)+2.2f)*.1f;
    }

    static void Write(string name,Texture2D texture,bool normal)
    {
        string path=Folder+"/"+name;
        File.WriteAllBytes(path,texture.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
        importer.sRGBTexture=false;
        importer.wrapMode=TextureWrapMode.Repeat;
        importer.mipmapEnabled=true;
        importer.SaveAndReimport();
    }
}
#endif

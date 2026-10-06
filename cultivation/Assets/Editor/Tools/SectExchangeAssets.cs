using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>图标同步到物品定义，而不是只在某一页临时替换。此工具不写场景。</summary>
public static class SectExchangeAssets
{
    const string Root="Assets/resources/UI/SectExchange";
    [MenuItem("修仙/宗门/更新兑换素材与坐骑图标")]
    public static void Build()
    {
        Directory.CreateDirectory(Root+"/Items");AssetDatabase.Refresh();
        foreach(var m in PanelDatabase.取().坐骑)
        {
            if(m==null || m.坐骑id=="mount_julong_01")continue;
            var prefab=m.加载模型();if(prefab==null)throw new System.InvalidOperationException("坐骑模型缺失："+m.坐骑id);
            RenderIcon(prefab,Root+"/Items/"+m.坐骑id+".png");
        }
        var dummy=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/resources/摆设/练功木桩.prefab");
        if(dummy!=null)RenderIcon(dummy,Root+"/Items/training-dummy.png");
        ImportImages();Sync();AssetDatabase.SaveAssets();Debug.Log("[宗门素材] 图标已同步，不修改场景");
    }
    public static void ImportImages()
    {
        foreach(var path in Directory.GetFiles(Root,"*.png",SearchOption.AllDirectories))
        {
            string p=path.Replace('\\','/');AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceUpdate);
            var t=AssetImporter.GetAtPath(p) as TextureImporter;if(t==null)continue;
            t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;t.alphaIsTransparency=true;t.mipmapEnabled=false;
            t.maxTextureSize=p.Contains("/Items/")?512:2048;t.textureCompression=p.Contains("/Items/")?TextureImporterCompression.Uncompressed:TextureImporterCompression.CompressedHQ;
            t.spritePixelsPerUnit=100;t.SaveAndReimport();
        }
    }
    static Sprite Sprite(string path)=>AssetDatabase.LoadAssetAtPath<Sprite>(path+".png");
    public static void Sync()
    {
        int count=0;
        foreach(var g in AssetDatabase.FindAssets("t:ItemDefinition",new[]{"Assets/Data/Generated/ItemDefinition"}))
        {
            var item=AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(g));Sprite icon=null;
            if(item.使用效果 is 获得法宝效果 treasure)icon=treasure.取法宝()?.图标;
            else if(item.使用效果 is 学主动神通效果 a)icon=AbilityIcon(a.取神通());
            else if(item.使用效果 is 学被动神通效果 b)icon=AbilityIcon(b.取神通());
            else if(item.使用效果 is 学坐骑效果 m)
            {
                icon=Sprite(Root+"/Items/"+m.坐骑id);var mount=m.取坐骑();
                if(mount!=null && icon!=null && mount.图标!=icon){mount.图标=icon;EditorUtility.SetDirty(mount);}
            }
            else if(item.使用效果 is 学丹方效果)icon=Sprite("Assets/resources/UI/InkUI/AlchemyInteractive/recipe-bamboo-roll");
            else if(item.使用效果 is 学功法效果 c)
            {
                icon=Sprite(Root+"/Items/manual");var gongfa=c.取功法();
                if(gongfa!=null && icon!=null && gongfa.图标==null){gongfa.图标=icon;EditorUtility.SetDirty(gongfa);}
            }
            else if(item.物品id.StartsWith("item_seed_"))icon=Sprite(Root+"/Items/seed-pouch");
            else if(item.物品id.StartsWith("item_dan_") || item.物品id=="item_huichundan")icon=Sprite("Assets/resources/UI/InkUI/AlchemyInteractive/pill");
            else if(item.物品id.StartsWith("item_lingtian_"))icon=Sprite(Root+"/Items/merit-token");
            else if(item.物品id=="item_menpai_bianfu")icon=Sprite("Assets/resources/UI/InkUI/Appearance/portrait-app_player");
            else if(item.物品id=="item_muzhuang")icon=Sprite(Root+"/Items/training-dummy");
            else icon=Sprite("Assets/resources/UI/InkUI/AlchemyInteractive/material-"+item.物品id);
            if(icon!=null && item.图标!=icon){item.图标=icon;EditorUtility.SetDirty(item);count++;}
        }
        Debug.Log("[背包图标] 同步 "+count+" 件物品");
    }
    static Sprite AbilityIcon(DivineAbilityDefinition a)
    {
        if(a==null)return null;var art=Sprite("Assets/resources/UI/InkUI/AbilityArt/icon-"+a.神通id.Replace("ability_","ability-"));
        return art!=null?art:a.图标;
    }
    public static void RenderIcon(GameObject prefab,string path)
    {
        var utility=new PreviewRenderUtility();GameObject go=null;Texture2D png=null;var previous=RenderTexture.active;RenderTexture encoded=null;bool previousSRGB=GL.sRGBWrite;
        try
        {
            go=Object.Instantiate(prefab);utility.AddSingleGO(go);
            foreach(var p in go.GetComponentsInChildren<ParticleSystem>(true)){p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);p.gameObject.SetActive(false);}
            foreach(var a in go.GetComponentsInChildren<Animator>(true))a.enabled=false;
            var rs=go.GetComponentsInChildren<Renderer>();Bounds bounds=new Bounds();bool first=true;
            foreach(var r in rs){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
            var cam=utility.camera;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.clear;cam.orthographic=true;
            cam.transform.rotation=Quaternion.Euler(12,35,0);cam.transform.position=bounds.center-cam.transform.forward*Mathf.Max(10,bounds.size.magnitude*2);
            var rotation=Quaternion.Inverse(cam.transform.rotation);float radius=0;
            foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1}){
                var v=rotation*Vector3.Scale(bounds.extents,new Vector3(x,y,z));radius=Mathf.Max(radius,Mathf.Abs(v.x),Mathf.Abs(v.y));
            }
            cam.orthographicSize=Mathf.Max(.1f,radius)*1.12f;cam.nearClipPlane=.01f;cam.farClipPlane=Mathf.Max(100,bounds.size.magnitude*5);
            utility.lights[0].intensity=1.3f;utility.lights[0].transform.rotation=Quaternion.Euler(40,40,0);
            utility.lights[1].intensity=.8f;utility.lights[1].transform.rotation=Quaternion.Euler(340,220,0);utility.ambientColor=new Color(.55f,.55f,.55f);
            utility.BeginPreview(new Rect(0,0,512,512),GUIStyle.none);
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.clear;
            utility.Render(true);var rendered=utility.EndPreview() as RenderTexture;
            // 预览RT是线性颜色，PNG供Sprite按sRGB读取；编码一次，避免图标被二次压暗。
            encoded=RenderTexture.GetTemporary(512,512,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            GL.sRGBWrite=true;Graphics.Blit(rendered,encoded);GL.sRGBWrite=previousSRGB;RenderTexture.active=encoded;
            png=new Texture2D(512,512,TextureFormat.RGBA32,false);png.ReadPixels(new Rect(0,0,512,512),0,0);png.Apply();File.WriteAllBytes(path,png.EncodeToPNG());
        }
        finally{GL.sRGBWrite=previousSRGB;RenderTexture.active=previous;if(encoded!=null)RenderTexture.ReleaseTemporary(encoded);if(png!=null)Object.DestroyImmediate(png);utility.Cleanup();}
    }
}

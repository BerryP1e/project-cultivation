using System;
using UnityEditor;
using UnityEngine;

/// <summary>配置战斗特效目录，不修改第三方预制体和场景。</summary>
public static class CombatVfxAssets
{
    [MenuItem("修仙/战斗/重建命中特效目录（不动场景）")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("请停止Play后构建");
        const string folder="Assets/resources/Combat";
        if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/resources","Combat");
        var path=folder+"/CombatVfxCatalog.asset";var catalog=AssetDatabase.LoadAssetAtPath<CombatVfxCatalog>(path);
        if(!catalog){catalog=ScriptableObject.CreateInstance<CombatVfxCatalog>();AssetDatabase.CreateAsset(catalog,path);}
        CombatVfxCatalog.Entry Entry(string id,string name,float size,bool direction=false,Vector3 rotation=default,float life=12)
        {var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/resources/CombatVFX/ExtremeFX/"+name+".prefab");if(!prefab)throw new InvalidOperationException("缺少ExtremeFX："+name);return new CombatVfxCatalog.Entry{id=id,预制体=prefab,缩放=size,跟随来向=direction,旋转欧拉=rotation,最长存活=life};}
        catalog.特效=new[]{
            Entry(CombatVfxPipeline.太虚剑命中,"Slash_Normal",.12f,true,new Vector3(90,0,0),1.3f),
            Entry(CombatVfxPipeline.火斩命中,"Slash_Fire",.12f,true,new Vector3(90,0,0),2),
            Entry(CombatVfxPipeline.太虚炼气命中,"Impact_Spark",.2f,true,Vector3.zero,2),
            Entry(CombatVfxPipeline.雷命中,"ThunderHit",.3f,false,Vector3.zero,1.5f),
            Entry(CombatVfxPipeline.火球落地,"Break_Lava",.05f,false,Vector3.zero,7),
            Entry(CombatVfxPipeline.雷四式[0],"ThunderStorm",1.5f),
            Entry(CombatVfxPipeline.雷四式[1],"Thunder",1.5f),
            Entry(CombatVfxPipeline.雷四式[2],"ThunderAttackGround",1.5f),
            Entry(CombatVfxPipeline.雷四式[3],"EpicZeus",2.5f)
        };
        catalog.查找(CombatVfxPipeline.雷四式[3]).散布半径=5f;
        var lava=catalog.查找(CombatVfxPipeline.火球落地);
        lava.灯光强度倍率=.04f;lava.灯光最大半径=1.5f;
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();Debug.Log("[战斗特效] 九项映射已生成；只配置表现，不改伤害公式");
    }
}

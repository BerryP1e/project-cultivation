using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>把保留原 GLB 的 FBX 工作副本接入既有外观资源链，不改场景或玩家动画。</summary>
public static class OutfitExpansionBaker
{
    public static readonly string[] 新外观 = { "app_nascent", "app_divinity", "app_integration" };
    static readonly string[] 预制体名 = { "NascentOutfit", "DivinityOutfit", "IntegrationOutfit" };

    public static void 准备模型()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出运行");
        var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/resources/外观/GoldenCoreOutfit.prefab")
            .GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMaterials[0];
        for (int i = 0; i < 新外观.Length; i++)
        {
            string id = 新外观[i], folder = "Assets/Art/Characters/Tripo/RealmOutfits/" + id;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/" + id + ".fbx");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + id + "-albedo.png");
            if (!model || !texture) throw new InvalidOperationException("缺少模型或贴图：" + id);
            var source = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (!source || source.bones.Length != 78 || source.sharedMesh.bounds.size.z < .8f)
                throw new InvalidOperationException("源模型骨架或导出单位不符：" + id);
            string materialPath = folder + "/" + id + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material) { material = new Material(template); AssetDatabase.CreateAsset(material, materialPath); }
            var shader=Shader.Find("Cultivation/OutfitDoubleSided");
            if(!shader)throw new InvalidOperationException("缺少源 GLB 双面着装着色器");
            material.shader=shader;material.doubleSidedGI=true;
            material.mainTexture = texture; EditorUtility.SetDirty(material);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                instance.name = 预制体名[i];
                foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var materials = new Material[renderer.sharedMesh.subMeshCount];
                    for (int j = 0; j < materials.Length; j++) materials[j] = material;
                    renderer.sharedMaterials = materials;
                }
                PrefabUtility.SaveAsPrefabAsset(instance, "Assets/resources/外观/" + 预制体名[i] + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        AssetDatabase.SaveAssets();
    }

    public static void 重建已分区着装()
    {
        var reference = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/resources/外观/修仙者.prefab")
            .GetComponentInChildren<SkinnedMeshRenderer>(true);
        foreach (string id in new[] { "app_player", "app_foundation", "app_nascent", "app_divinity", "app_integration" })
        {
            var appearance = AppearanceDatabase.取().取(id);
            if (!appearance) throw new InvalidOperationException("外观表未导入：" + id);
            OutfitClothBaker.Bake(appearance, reference);
        }
        AssetDatabase.SaveAssets();
    }
}

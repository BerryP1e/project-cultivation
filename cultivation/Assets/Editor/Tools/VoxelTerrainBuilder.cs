using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class VoxelTerrainBuilder
{
    [MenuItem("修仙/体素/启用当前地图实体地面")]
    public static void 建立()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play first.");
        var terrain=UnityEngine.Object.FindObjectOfType<Terrain>();
        var manifest=AssetDatabase.LoadAssetAtPath<VoxelMapPilotSettings>(VoxelMapPilotBuilder.SettingsPath);
        if(!terrain || !manifest || terrain.gameObject.scene.path!=manifest.场景路径)throw new InvalidOperationException("Open the configured scene first.");
        if(manifest.地面){Selection.activeObject=manifest.地面;return;}
        var data=terrain.terrainData;var layers=data.terrainLayers;
        if(layers.Length!=4 || data.alphamapTextures.Length!=1)throw new InvalidOperationException("Current ground shader expects four terrain layers.");
        const string folder="Assets/Art/Environment/World/Voxel/Terrain";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        var shader=Shader.Find("Cultivation/VoxelTerrainSurface");if(!shader)throw new InvalidOperationException("Ground shader is not ready.");
        var mat=new Material(shader){name="WildernessVoxelGround"};
        mat.SetTexture("_Control",data.alphamapTextures[0]);var position=terrain.transform.position;
        mat.SetVector("_Terrain",new Vector4(position.x,position.z,data.size.x,data.size.z));
        for(int i=0;i<4;i++)
        {mat.SetTexture("_Layer"+i,layers[i].diffuseTexture);mat.SetVector("_Tile"+i,new Vector4(layers[i].tileSize.x,layers[i].tileSize.y,layers[i].tileOffset.x,layers[i].tileOffset.y));}
        AssetDatabase.CreateAsset(mat,folder+"/WildernessGround.mat");
        var config=ScriptableObject.CreateInstance<VoxelTerrainSettings>();config.源地形=data;config.预期位置=position;config.地块材质=mat;
        AssetDatabase.CreateAsset(config,folder+"/WildernessGround.asset");manifest.地面=config;
        EditorUtility.SetDirty(manifest);AssetDatabase.SaveAssets();Selection.activeObject=config;
        Debug.Log("VOXEL_TERRAIN_CONFIGURED thickness=4m support=.35m; all terrain patches are addressable, source scene and TerrainData unchanged.");
    }
}

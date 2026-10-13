using UnityEngine;

/// <summary>家园运行时资源的唯一入口；美术资源保留原位置，由显式引用打包。</summary>
public sealed class HomeAssets : ScriptableObject
{
    public GameObject 地面, 基站, 傀儡, 稿子, 水壶;
    public Material 地面材质;
    public Vector2[] 地面内轮廓;
    public float 地面圆角 = 2.4f;
    public GameObject[] 围挡树木, 围挡石头;
    public AnimationClip 待机, 行走, 松土, 播种收获, 浇水;
    public static HomeAssets 取() => Resources.Load<HomeAssets>("Home/Assets");
}

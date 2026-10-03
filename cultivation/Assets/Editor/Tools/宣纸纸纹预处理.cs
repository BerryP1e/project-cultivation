using UnityEditor;
using UnityEngine;

/// <summary>
/// **宣纸 / 纸纹预处理**：把一张"能看的纸"变成"能当颗粒用的纸"。
///
/// ## 为什么必须预处理
///
/// 用户 2026-10-03 给的那张宣纸，实测（1024x512，`GetPixels32` 统计）：
/// **平均亮度 0.852，99.9% 的像素挤在 0.85 附近**（暗像素只有 0.1%、亮像素 0%）。
/// 直接乘到画面上：
///
/// | 做法 | 结果 |
/// |---|---|
/// | 整张纸乘上去 | 只是把画面整体**变亮/变暗**（低频），大块云斑还会变成**脏斑** |
/// | 运行时高通（中心减四邻）×增益 6 | 实测只动了 **0.67% 的像素、平均差 0.52/255** —— 肉眼看不见 |
///
/// 根因：**纸的"纹"是高频的，但它自己的动态范围极小**。要当颗粒用，
/// 必须 ① 去掉低频（大块云斑）② 把剩下那点高频**归一化**到固定幅度。
///
/// ## 做法
///
/// 1. 求亮度 `L`；2. 盒式模糊（半径 = 短边/64，最小 2）得低频 `B`；
/// 3. 高通 `D = L − B`；4. 统计标准差 σ，把 **±2σ 映射到 ±0.25**、中点压到 **0.5**：
///    `out = 0.5 + D / (4σ) * 0.5`（再 clamp 到 0.02~0.98）。
///    ⇒ 产物 σ ≈ 0.125，所以 `±2σ = ±0.25`：这正是 `GameGlobalGrade.纸纹对比 = 2` 那个默认值的由来。
///
/// 产物 `xxx_纸纹.png` 是**数据图**（不是颜色图）——
/// ⚠️ 导入必须 **sRGB = false**，否则引擎会在采样时做 gamma 变换，中点 0.5 就不是 0.5 了。
///
/// ## 用法
///
/// Project 里**选中一张贴图** → 菜单 `修仙 / 美术 / 宣纸纸纹预处理`（ASCII：Cultivation / Bake Paper Grain）。
/// 产物与源图同目录、名字加 `_纸纹`，导入设置由工具一并配好（sRGB off / 不压缩 / Mirror / 无 mip）。
///
/// ⚠️ 源贴图必须**可读**（Advanced → Read/Write 打开）；工具会先把它打开再读。
/// </summary>
public static class 宣纸纸纹预处理
{
    /// <summary>±2σ 映射到多大的明暗幅度（0.25 = ±25%）</summary>
    const float 目标幅度 = 0.5f;
    const float 中点 = 0.5f;

    [MenuItem("修仙/美术/宣纸纸纹预处理", false, 810)]
    [MenuItem("Cultivation/Bake Paper Grain", false, 810)]
    public static void 预处理()
    {
        var 源 = Selection.activeObject as Texture2D;
        if (源 == null)
        {
            Debug.LogWarning("[纸纹] 先在 Project 窗口选中一张贴图，再执行这个菜单。");
            return;
        }

        string 源路径 = AssetDatabase.GetAssetPath(源);
        var imp = AssetImporter.GetAtPath(源路径) as TextureImporter;
        if (imp == null) { Debug.LogError("[纸纹] 拿不到 TextureImporter：" + 源路径); return; }
        if (!imp.isReadable)
        {
            imp.isReadable = true;
            imp.SaveAndReimport();
            Debug.Log("[纸纹] 源贴图原来不可读 —— 已临时打开 Read/Write 后重新导入");
            源 = AssetDatabase.LoadAssetAtPath<Texture2D>(源路径);
        }

        int w = 源.width, h = 源.height;
        var px = 源.GetPixels32();
        if (px == null || px.Length != w * h) { Debug.LogError("[纸纹] 读像素失败（贴图不可读？）"); return; }

        var 亮 = new float[w * h];
        double 和 = 0;
        for (int i = 0; i < 亮.Length; i++)
        {
            亮[i] = (px[i].r * 0.2126f + px[i].g * 0.7152f + px[i].b * 0.0722f) / 255f;
            和 += 亮[i];
        }
        float 均值 = (float)(和 / 亮.Length);

        int 半径 = Mathf.Max(2, Mathf.Min(w, h) / 64);
        var 低频 = 盒式模糊(亮, w, h, 半径);

        double 平方和 = 0;
        var 高通 = new float[w * h];
        for (int i = 0; i < 高通.Length; i++)
        {
            高通[i] = 亮[i] - 低频[i];
            平方和 += 高通[i] * (double)高通[i];
        }
        float σ = Mathf.Sqrt((float)(平方和 / 高通.Length));

        var 输出 = new Color32[w * h];
        for (int i = 0; i < 输出.Length; i++)
        {
            float v = Mathf.Clamp01(中点 + 高通[i] / Mathf.Max(4f * σ, 1e-5f) * 目标幅度);
            v = Mathf.Clamp(v, 0.02f, 0.98f);
            byte b = (byte)Mathf.RoundToInt(v * 255f);
            输出[i] = new Color32(b, b, b, 255);
        }

        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.SetPixels32(输出);
        tex.Apply();
        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        string 目录 = System.IO.Path.GetDirectoryName(源路径).Replace('\\', '/');
        string 名 = System.IO.Path.GetFileNameWithoutExtension(源路径);
        string 出路径 = 目录 + "/" + 名 + "_纸纹.png";
        System.IO.File.WriteAllBytes(出路径, png);
        AssetDatabase.ImportAsset(出路径, ImportAssetOptions.ForceUpdate);

        var 出imp = AssetImporter.GetAtPath(出路径) as TextureImporter;
        if (出imp != null)
        {
            出imp.textureType = TextureImporterType.Default;
            出imp.sRGBTexture = false;            // ★ 数据图，绝不能按颜色读
            出imp.mipmapEnabled = false;
            出imp.wrapMode = TextureWrapMode.Mirror;
            出imp.filterMode = FilterMode.Bilinear;
            出imp.isReadable = false;
            出imp.maxTextureSize = 2048;
            出imp.textureCompression = TextureImporterCompression.Uncompressed;
            出imp.SaveAndReimport();
        }

        Debug.Log(string.Format("[纸纹] 源：{0}x{1} 平均亮度 {2:0.000}｜高通 σ = {3:0.0000}（= {4:0.0}/255）"
            + "｜增益 {5:0.0}｜产物 σ ≈ {6:0.000}（±2σ = ±{7:0.00}）",
            源.width, 源.height, 均值, σ, σ * 255f, 目标幅度 / (4f * Mathf.Max(σ, 1e-5f)),
            目标幅度 / 4f, 目标幅度 / 2f));
        Debug.Log("[纸纹] ✔ 已生成 " + 出路径 + "（sRGB=off / Mirror / 无 mip / 不压缩）"
            + " —— 把它填到 GameGlobalGrade 的「纸纹」，或改成 Resources 里那张的名字", 源);
    }

    /// <summary>两趟滑动窗口盒式模糊（O(n)，边界按 clamp 取样）</summary>
    static float[] 盒式模糊(float[] 源, int w, int h, int 半径)
    {
        var 横 = new float[w * h];
        var 出 = new float[w * h];
        int 宽 = 半径 * 2 + 1;

        for (int y = 0; y < h; y++)
        {
            double 和 = 0;
            for (int x = -半径; x <= 半径; x++) 和 += 源[y * w + Mathf.Clamp(x, 0, w - 1)];
            for (int x = 0; x < w; x++)
            {
                横[y * w + x] = (float)(和 / 宽);
                int 出x = Mathf.Clamp(x - 半径, 0, w - 1);
                int 入x = Mathf.Clamp(x + 半径 + 1, 0, w - 1);
                和 += 源[y * w + 入x] - 源[y * w + 出x];
            }
        }

        for (int x = 0; x < w; x++)
        {
            double 和 = 0;
            for (int y = -半径; y <= 半径; y++) 和 += 横[Mathf.Clamp(y, 0, h - 1) * w + x];
            for (int y = 0; y < h; y++)
            {
                出[y * w + x] = (float)(和 / 宽);
                int 出y = Mathf.Clamp(y - 半径, 0, h - 1);
                int 入y = Mathf.Clamp(y + 半径 + 1, 0, h - 1);
                和 += 横[入y * w + x] - 横[出y * w + x];
            }
        }

        return 出;
    }

    // ---- ASCII 别名 ----
    public static void Bake() { 预处理(); }
}

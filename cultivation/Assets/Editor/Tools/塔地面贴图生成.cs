// 镇妖塔地面贴图生成器
//
// ## 为什么要生成贴图
//
// 塔的 `地面板` 是一整块 24 顶点的大平板（54×54m），UV 只有 0~1，
// 材质 `digongwall_001_02` 的 tiling 是 (1,1) —— 那张 **128×256** 的贴图
// 被拉伸铺满整个塔层。相机又是俯视，近景就是一片纯糊。
//
// 实测把 tiling 提到 32 也只是把糊纹变成细密杂纹（源图本身几乎没有细节），
// 靠平铺救不回来。所以**直接生成一张适配塔尺度、细节清晰的石板地面**。
//
// ## 设计取向（对齐"卡通/动漫化"）
//
// 不用噪声堆高频细节，而是**大色块 + 清晰描缝**：
// 8×8 砧石、每块独立色相/明度抖动、接缝压暗、石面轻微打磨——
// 这正是动漫游戏地面读起来"干净又有层次"的做法，也贴切"镇妖塔"的阴冷石殿气质。
//
// ## 用法
// 菜单「工具/画面/③ 生成塔地面贴图」。生成到
// `Assets/resources/Environment1/Common/Textures/TowerFloor/`，然后手动挂到地面材质上
// （或用「④ 应用地面贴图」一键挂 + 设 tiling）。

using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cultivation.EditorTools
{
    public static class 塔地面贴图生成
    {
        private const string 输出目录 = "Assets/resources/Environment1/Common/Textures/TowerFloor";
        private const string 贴图名 = "tower_floor_slab.png";
        private const int 尺寸 = 1024;
        private const int 每边石数 = 8;       // 8×8 = 64 块砧石
        private const float 缝宽占比 = 0.055f;  // 接缝占单块宽度的比例

        [MenuItem("工具/画面/③ 生成塔地面贴图", false, 40)]
        public static void 生成()
        {
            var tex = 绘制();
            Directory.CreateDirectory(输出目录);
            var 路径 = 输出目录 + "/" + 贴图名;
            File.WriteAllBytes(路径, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.Refresh();

            // 导入设置：可平铺、不压缩（细节要清楚）、开 mipmap
            var imp = AssetImporter.GetAtPath(路径) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Default;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.filterMode = FilterMode.Bilinear;
                imp.mipmapEnabled = true;
                imp.anisoLevel = 4;
                imp.maxTextureSize = 1024;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                var 平台 = imp.GetPlatformTextureSettings("Standalone");
                平台.overridden = false;
                imp.SetPlatformTextureSettings(平台);
                imp.SaveAndReimport();
            }
            Debug.Log($"[塔地面贴图] 已生成 {路径}  (可平铺，{尺寸}×{尺寸})");
        }

        [MenuItem("工具/画面/④ 应用塔地面贴图（挂材质 + 设平铺）", false, 41)]
        public static void 应用()
        {
            var 路径 = 输出目录 + "/" + 贴图名;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(路径);
            if (tex == null) { Debug.LogError("[塔地面] 找不到贴图，先跑「③ 生成塔地面贴图」"); return; }

            string[] 目标材质 = { "environment_common_digongwall_001_02" };
            // ★ 3× 是**实测选出来的**，别凭感觉调：
            //   地面板 54m，贴图里是 8×8 砧石，铺 3 次 → 每块石板约 2.7m。
            //   相机是俯视视角，这个尺度下石板读得清又不抢戏。
            //   【踩过的坑】第一版铺 6 次（每块 1.1m）→ 整屏马赛克网格，比原来还糟；
            //   铺 1.5 次又太平、等于没贴。3× 是中间的最优解。
            // 亮度 0.62：贴图本身已经压过边、描过缝，这里再压一道降对比，避免地面抢视线。
            var 平铺 = new Vector2(3f, 3f);
            var 染色 = new Color(0.62f, 0.632f, 0.670f, 1f);
            int 改了 = 0;

            foreach (var g in AssetDatabase.FindAssets("t:Material"))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var 名 = Path.GetFileNameWithoutExtension(p);
                if (System.Array.IndexOf(目标材质, 名) < 0) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) continue;
                m.SetTexture("_MainTex", tex);
                m.SetTextureScale("_MainTex", 平铺);
                if (m.HasProperty("_Color")) m.SetColor("_Color", 染色);
                EditorUtility.SetDirty(m);
                改了++;
                Debug.Log($"[塔地面] 已挂 {p}  tiling=({平铺.x},{平铺.y})  color=({染色.r:F2},{染色.g:F2},{染色.b:F2})");
            }
            AssetDatabase.SaveAssets();
            if (改了 == 0) Debug.LogWarning("[塔地面] 没找到目标材质 environment_common_digongwall_001_02");
            else Debug.Log($"[塔地面] 完成，改了 {改了} 个材质。");
        }

        // ---------------- 绘制 ----------------

        private static Texture2D 绘制()
        {
            var tex = new Texture2D(尺寸, 尺寸, TextureFormat.RGBA32, false);
            var 像素 = new Color[尺寸 * 尺寸];
            float 单块 = 尺寸 / (float)每边石数;
            float 缝 = 单块 * 缝宽占比;

            for (int y = 0; y < 尺寸; y++)
            {
                for (int x = 0; x < 尺寸; x++)
                {
                    int 列 = (int)(x / 单块);
                    int 行 = (int)(y / 单块);
                    float 块内x = x - 列 * 单块;
                    float 块内y = y - 行 * 单块;

                    // —— 每块石头的独立基色（用确定性哈希，保证可重复生成）——
                    float r1 = 哈希(列, 行, 1);
                    float r2 = 哈希(列, 行, 2);
                    float r3 = 哈希(列, 行, 3);

                    // 石殿基色：偏冷的灰蓝，明度在 0.30~0.42 之间（暗部留给提灯）
                    float 明度 = Mathf.Lerp(0.30f, 0.42f, r1);
                    var 基色 = new Color(明度 * 0.94f, 明度 * 0.97f, 明度 * 1.06f);

                    // 轻微色相抖动：有的偏暖（旧石）、有的偏冷（湿石）
                    float 暖冷 = (r2 - 0.5f) * 0.06f;
                    基色.r += 暖冷 * 0.5f;
                    基色.b -= 暖冷 * 0.5f;

                    // —— 石面质感：沿对角线的一点打磨痕 + 细颗粒 ——
                    float 磨痕 = Mathf.Sin((块内x + 块内y) * 0.55f + r3 * 6.28f) * 0.012f;
                    float 颗粒 = (哈希(x, y, 7) - 0.5f) * 0.022f;

                    // —— 边缘：靠边压暗，做出石块的圆角感与厚度感 ——
                    float 距边 = Mathf.Min(
                        Mathf.Min(块内x, 单块 - 块内x),
                        Mathf.Min(块内y, 单块 - 块内y));
                    float 边暗 = Mathf.Clamp01(距边 / (单块 * 0.18f));
                    边暗 = Mathf.Lerp(0.55f, 1f, 边暗 * 边暗);

                    var c = 基色 * 边暗;
                    c.r += 磨痕 + 颗粒;
                    c.g += 磨痕 + 颗粒;
                    c.b += 磨痕 + 颗粒;

                    // —— 接缝：压到很暗的冷灰，让"大色块"分得开 ——
                    if (块内x < 缝 || 块内y < 缝)
                    {
                        float 最边 = Mathf.Min(
                            Mathf.Min(块内x, 单块 - 块内x),
                            Mathf.Min(块内y, 单块 - 块内y));
                        float t = Mathf.Clamp01(最边 / 缝);       // 0=缝心 1=缝缘
                        var 缝色 = new Color(0.055f, 0.058f, 0.068f);
                        c = Color.Lerp(缝色, c, t * t);
                    }

                    c.a = 1f;
                    像素[y * 尺寸 + x] = c;
                }
            }

            tex.SetPixels(像素);
            tex.Apply();
            return tex;
        }

        /// <summary>确定性哈希 0~1（不依赖 Random，保证每次生成一致）</summary>
        private static float 哈希(int x, int y, int 盐)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + 盐 * 40503;
                h = (h ^ (h >> 13)) * 1274126177;
                h = h ^ (h >> 16);
                return (h & 0x7fffffff) / (float)0x7fffffff;
            }
        }
    }
}

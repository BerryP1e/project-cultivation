using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 把外部交付的 UI 素材（`Assets/resources/UI/InkUI/**`）按规格设成 **Sprite (2D and UI)** 并写好九宫格 border。
///
/// ## 为什么要一个工具
///
/// 素材是**按 2× 交付**的（规格表见 `docs/reference/UI素材/素材规格与提示词.md`），
/// 而交付方的 `manifest.json` 里写的是 **1× 的 border**。手工在 Inspector 里一条条填 79 张图，
/// 一定会漏、会填错，而且**border 填大了（左右之和 ≥ 宽度）会让九宫格直接坏掉**。
/// 所以：一张表 + 自动夹取 + 逐条打日志。
///
/// ## 用法
///
/// 菜单：**`修仙 / UI / 导入 InkUI 素材（Sprite + 九宫格）`**（ASCII：Cultivation / Import InkUI Sprites）
/// 幂等：可以反复跑。
///
/// ⚠️ 几个必须记住的取值：
///   · **PPU = 100**（和全工程一致）；2× 素材在 Image 上要配 `pixelsPerUnitMultiplier = 2`
///     （`UIInkSkin.九宫格倍率`），这样 border 才会按 1× 的视觉尺寸渲染。
///   · **图集/压缩**：大图用 `CompressedHQ`（BC7），小件（图标/按钮/槽位）留不压缩，免得纸纹被压出色带。
///   · **不要 mipmap**（UI 不做缩小采样），**Wrap = Clamp**。
/// </summary>
public static class InkUI素材导入
{
    const string 根 = "Assets/resources/UI/InkUI";

    /// <summary>文件名 → border（左/下/右/上，单位 = **交付图的像素**）。值来自各包的 manifest × 2（2× 交付）</summary>
    static readonly Dictionary<string, Vector4> 九宫格 = new Dictionary<string, Vector4>
    {
        // ---- 神通页 ----
        { "panel-sheet",        new Vector4(192, 192, 192, 192) },
        { "panel-inner",        new Vector4( 96,  96,  96,  96) },
        { "nav-rail",           new Vector4( 48,  48,  48,  48) },
        { "tab-normal",         new Vector4( 48,  16,  48,  16) },
        { "tab-hover",          new Vector4( 48,  16,  48,  16) },
        { "tab-active",         new Vector4( 48,  16,  48,  16) },
        { "row-normal",         new Vector4( 64,  16,  64,  16) },
        { "row-hover",          new Vector4( 64,  16,  64,  16) },
        { "row-selected",       new Vector4( 64,  16,  64,  16) },
        { "panel-detail",       new Vector4(128, 128, 128, 128) },
        { "badge-tier-jade",    new Vector4( 48,  16,  48,  16) },
        { "badge-tier-cinnabar",new Vector4( 48,  16,  48,  16) },
        { "scroll-track",       new Vector4( 16,  16,  16,  16) },
        { "scroll-thumb",       new Vector4( 24,  24,  24,  24) },
        { "bar-track",          new Vector4( 24,  16,  24,  16) },
        // ---- 丹房 / 修炼 ----
        { "cultivation-frame",  new Vector4(192, 192, 192, 192) },
        { "gongfa-card",        new Vector4( 96,  96,  96,  96) },
        { "material-slot-empty",   new Vector4(64, 64, 64, 64) },
        { "material-slot-selected",new Vector4(64, 64, 64, 64) },
        { "material-slot-filled",  new Vector4(64, 64, 64, 64) },
        { "material-slot-disabled",new Vector4(64, 64, 64, 64) },
        { "recipe-row-normal",  new Vector4( 48,  16,  48,  16) },
        { "recipe-row-hover",   new Vector4( 48,  16,  48,  16) },
        { "recipe-row-selected",new Vector4( 48,  16,  48,  16) },
        // ---- 其余面板 ----
        { "plot-tag",           new Vector4( 96,  96,  96,  96) },
        { "dialog-scroll",      new Vector4( 96,  64,  96,  64) },
        { "dialog-nameplate",   new Vector4( 48,  32,  48,  32) },
        { "dialog-choice-normal",  new Vector4(64, 32, 64, 32) },
        { "dialog-choice-hover",   new Vector4(64, 32, 64, 32) },
        { "dialog-choice-selected",new Vector4(64, 32, 64, 32) },
        { "toast",              new Vector4( 48,  32,  48,  32) },
        { "tower-floor-bar",    new Vector4( 48,  24,  48,  24) },
        { "teleport-plate",     new Vector4( 96,  96,  96,  96) },
        { "title-plaque",       new Vector4( 96,  96,  96,  96) },
        // ---- 按钮包（这批是 1× 交付，border 就是 BUTTON-KIT 写的 120/28/120/28）----
        { "ivory-normal",       new Vector4(120, 28, 120, 28) },
        { "ivory-hover",        new Vector4(120, 28, 120, 28) },
        { "ivory-pressed",      new Vector4(120, 28, 120, 28) },
        { "ivory-disabled",     new Vector4(120, 28, 120, 28) },
        { "jade-normal",        new Vector4(120, 28, 120, 28) },
        { "jade-hover",         new Vector4(120, 28, 120, 28) },
        { "jade-pressed",       new Vector4(120, 28, 120, 28) },
        { "jade-disabled",      new Vector4(120, 28, 120, 28) },
        { "cinnabar-normal",    new Vector4(120, 28, 120, 28) },
        { "cinnabar-hover",     new Vector4(120, 28, 120, 28) },
        { "cinnabar-pressed",   new Vector4(120, 28, 120, 28) },
        { "cinnabar-disabled",  new Vector4(120, 28, 120, 28) },
        // ---- 另外三包（`transparent-*-v1` / `large-ui-pieces-v1`）----
        //   ⚠️ 这三包的 manifest 只写了"从图集裁哪块"，**没写九宫格** ⇒ 这里按用途给保守值，
        //   跑一次看日志里的尺寸，再按"角饰大概占多宽"微调。
        { "parent-curtain-9slice-source", new Vector4(96, 96, 96, 96) },   // 面板大幕布（含山水）
        { "parent-navigation-rail",       new Vector4(24, 24, 24, 24) },
        { "nav-tab-inactive",             new Vector4(24,  8, 24,  8) },
        { "nav-tab-active",               new Vector4(36, 12, 36, 12) },
        { "ornate-divider",               new Vector4(24,  8, 24,  8) },
        { "left-parent-navigation-panel", new Vector4(96, 96, 96, 96) },
        { "learned-ability-library-panel",new Vector4(96, 96, 96, 96) },
        { "ability-detail-panel",         new Vector4(96, 96, 96, 96) },
        { "ability-library-card",         new Vector4(28, 28, 28, 28) },
        { "enabled-passive-row",          new Vector4(28, 20, 28, 20) },
        { "parent-navigation-tab",        new Vector4(24,  8, 24,  8) },
        { "primary-action-button",        new Vector4(120, 28, 120, 28) },
        { "secondary-action-button",      new Vector4(120, 28, 120, 28) },
    };

    /// <summary>小件不压缩（怕色带）；其余用 CompressedHQ</summary>
    static bool 留不压缩(string 名) =>
        名.StartsWith("icon-") || 名.StartsWith("portrait-")
        || 名.StartsWith("ivory-") || 名.StartsWith("jade-") || 名.StartsWith("cinnabar-")
        || 名.Contains("slot") || 名.Contains("badge") || 名.Contains("scroll")
        || 名.Contains("bar-") || 名.Contains("tab-");

    [MenuItem("修仙/UI/导入 InkUI 素材（Sprite + 九宫格）", false, 900)]
    [MenuItem("Cultivation/Import InkUI Sprites", false, 900)]
    public static void 导入()
    {
        if (!AssetDatabase.IsValidFolder(根))
        {
            Debug.LogError("[InkUI] 找不到目录：" + 根 + "（先把素材拷进来）");
            return;
        }

        var 名单 = AssetDatabase.FindAssets("t:Texture2D", new[] { 根 });
        int 改了 = 0, 九宫 = 0, 夹了 = 0;
        var 明细 = new List<string>();

        foreach (var g in 名单)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            var imp = AssetImporter.GetAtPath(p) as TextureImporter;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            if (imp == null || tex == null) continue;

            string 名 = System.IO.Path.GetFileNameWithoutExtension(p);

            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 100f;
            imp.alphaIsTransparency = true;
            // 异形可交互图需要 CPU alpha 采样；大幕布、立绘不保留 CPU 副本。
            imp.isReadable = 名.Contains("slot") || 名.StartsWith("tab-") || 名 == "plot-tag"
                || 名.StartsWith("ivory-") || 名.StartsWith("jade-") || 名.StartsWith("cinnabar-");
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.maxTextureSize = 2048;
            imp.textureCompression = 留不压缩(名) ? TextureImporterCompression.Uncompressed
                                                  : TextureImporterCompression.CompressedHQ;

            var 设 = new TextureImporterSettings();
            imp.ReadTextureSettings(设);
            设.spriteMeshType = SpriteMeshType.FullRect;          // 九宫格 + 异形命中都要 FullRect
            设.spriteAlignment = (int)SpriteAlignment.Center;
            设.spritePivot = new Vector2(0.5f, 0.5f);
            imp.SetTextureSettings(设);

            // ---- 九宫格 border（按交付像素填；超了就地夹取并告警）----
            Vector4 b;
            if (九宫格.TryGetValue(名, out b))
            {
                float 左 = b.x, 下 = b.y, 右 = b.z, 上 = b.w;
                float 上限x = tex.width * 0.5f - 1f, 上限y = tex.height * 0.5f - 1f;
                if (左 + 右 > tex.width - 2 || 下 + 上 > tex.height - 2)
                {
                    float k = Mathf.Min(上限x / Mathf.Max(左 + 右, 1f) * 2f, 上限y / Mathf.Max(下 + 上, 1f) * 2f);
                    Debug.LogWarning("[InkUI] " + 名 + " 的 border(" + b + ") 对 " + tex.width + "x" + tex.height
                        + " 太大 ⇒ 按 " + k.ToString("F2") + " 夹取。要么图太小，要么 spec 乘错了倍数");
                    左 *= k; 右 *= k; 下 *= k; 上 *= k;
                    夹了++;
                }
                imp.spriteBorder = new Vector4(Mathf.Round(左), Mathf.Round(下), Mathf.Round(右), Mathf.Round(上));
                九宫++;
            }

            imp.SaveAndReimport();
            改了++;
            明细.Add(名 + " " + tex.width + "x" + tex.height
                + (九宫格.ContainsKey(名) ? " border=" + imp.spriteBorder : ""));
        }

        AssetDatabase.Refresh();
        Debug.Log("[InkUI] 处理 " + 改了 + " 张：九宫格 " + 九宫 + " 张" + (夹了 > 0 ? "（其中 " + 夹了 + " 张被夹取，见上面 Warning）" : "")
                  + "\n  " + string.Join("\n  ", 明细.ToArray()));
    }

    /// <summary>只读检查：列出所有素材的尺寸与当前 border（不改任何东西）</summary>
    [MenuItem("修仙/UI/检查 InkUI 素材导入状态", false, 901)]
    public static void 检查()
    {
        var 名单 = AssetDatabase.FindAssets("t:Texture2D", new[] { 根 });
        Debug.Log("[InkUI·检查] 共 " + 名单.Length + " 张");
        foreach (var g in 名单)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            var imp = AssetImporter.GetAtPath(p) as TextureImporter;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            if (imp == null || tex == null) continue;
            string 名 = System.IO.Path.GetFileNameWithoutExtension(p);
            Debug.Log("[InkUI·检查] " + 名 + " " + tex.width + "x" + tex.height
                + " type=" + imp.textureType + " PPU=" + imp.spritePixelsPerUnit
                + " border=" + imp.spriteBorder + " 压缩=" + imp.textureCompression);
        }
    }
}

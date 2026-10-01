using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 【遮挡与特效开关素材工具】把"某个渲染器临时换成半透明、之后原样还回去"这件事集中在这里。
///
/// 为什么要抽出来：**建筑透明**（`OcclusionTransparency`）和**树冠透明**（同一个组件里的树冠部分）
/// 用的是**同一套**逻辑 —— 缓存原材质、按源材质复用一份透明材质、离场时原样还原。
/// 两处各写一份必然会有一处忘了还材质（那就污染资产了）。
///
/// ★ 关键约束（踩过）：
///   · **绝不能改 `sharedMaterial` 指向的那个材质本身** —— 树冠的材质是**多个树共用**的，
///     改一处会让所有树一起变透明。只改渲染器的 `sharedMaterials` **数组**，指向本工具造的副本。
///   · 透明材质按**源材质**缓存（不是按渲染器），所以 500 棵树只有几十个材质实例。
/// </summary>
public static class 遮挡素材
{
    /// <summary>渲染器 → 它的原材质数组（用来还原）</summary>
    public class 台账
    {
        readonly Dictionary<Renderer, Material[]> _原 = new Dictionary<Renderer, Material[]>();
        readonly Dictionary<Material, Material> _透明缓存 = new Dictionary<Material, Material>();

        /// <summary>把渲染器换成半透明。已经在台账里的不重复处理。返回是否新换了</summary>
        public bool 变透明(Renderer r, float 透明度)
        {
            if (r == null) return false;
            if (_原.ContainsKey(r)) return false;

            var 原 = r.sharedMaterials;
            if (原 == null || 原.Length == 0) return false;

            _原[r] = 原;
            var 换 = new Material[原.Length];
            for (int i = 0; i < 原.Length; i++) 换[i] = 取透明(原[i], 透明度);
            r.sharedMaterials = 换;
            return true;
        }

        /// <summary>还原一个渲染器</summary>
        public void 还原(Renderer r)
        {
            if (r == null) { _原.Remove(r); return; }
            Material[] 原;
            if (_原.TryGetValue(r, out 原) && 原 != null) r.sharedMaterials = 原;
            _原.Remove(r);
        }

        /// <summary>全部还原（组件禁用 / 销毁 / 切场景时调）</summary>
        public void 全还原()
        {
            foreach (var kv in _原) if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;
            _原.Clear();
        }

        public bool 已在管(Renderer r) => r != null && _原.ContainsKey(r);
        public int 数量 => _原.Count;

        Material 取透明(Material 源, float 透明度)
        {
            if (源 == null) return null;
            Material 有;
            if (_透明缓存.TryGetValue(源, out 有) && 有 != null) return 有;

            // 透明版优先用「Transparent/Diffuse」（真混合、能和原色一致地变淡）；
            // 团结引擎里如果它被剥离了，退到 Cutout（至少不糊），再退到 Unlit。
            var sh = Shader.Find("Legacy Shaders/Transparent/Diffuse");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse");
            if (sh == null) sh = Shader.Find("Unlit/Transparent");
            if (sh == null) { Debug.LogWarning("[遮挡与特效开关] 找不到任何透明 shader，无法变透明"); return 源; }

            var m = new Material(sh) { name = "透明_" + 源.name };
            if (源.HasProperty("_MainTex"))
            {
                m.mainTexture = 源.mainTexture;
                m.mainTextureScale = 源.mainTextureScale;
                m.mainTextureOffset = 源.mainTextureOffset;
            }
            var c = 源.HasProperty("_Color") ? 源.GetColor("_Color") : Color.white;
            m.color = new Color(c.r, c.g, c.b, 透明度);

            _透明缓存[源] = m;
            return m;
        }
    }

    /// <summary>这个渲染器是不是"树冠"（按物件名判断：树根/子件都以 environment_Tree 开头）</summary>
    public static bool 是树冠(Renderer r)
    {
        if (r == null) return false;
        var t = r.transform;
        for (int i = 0; i < 6 && t != null; i++, t = t.parent)
            if (t.name.StartsWith("environment_Tree", System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}

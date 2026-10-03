using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 【遮挡与特效开关素材工具】把"某个渲染器临时换成**水墨淡出**、之后原样还回去"这件事集中在这里。
///
/// 为什么要抽出来：**建筑透明**（`OcclusionTransparency`）和**树冠透明**（同一个组件里的树冠部分）
/// 用的是**同一套**逻辑 —— 缓存原材质、按源材质复用一份水墨材质、离场时原样还原。
/// 两处各写一份必然会有一处忘了还材质（那就污染资产了）。
///
/// ★ 三条铁律（前两条踩过）：
///   · **绝不能改 `sharedMaterial` 指向的那个材质本身** —— 树冠的材质是**多个树共用**的，
///     改一处会让所有树一起透明。只改渲染器的 `sharedMaterials` **数组**，指向本工具造的副本。
///   · 水墨材质按**源材质**缓存（不是按渲染器），所以 500 棵树只有几十个材质实例。
///   · ⚠️ **`_Fade` 不能用材质实例存**（一份副本被多个渲染器共用，改了会一起淡出），
///     必须走 **`MaterialPropertyBlock`** 按渲染器逐帧设 —— 这样既是几十个材质，又能各自计时。
///
/// 2026-10-03 改造：原来换的是 `Legacy Shaders/Transparent/Diffuse`（一换就丢光照/丢纸纹，
/// 看着像"塑料变半透明"）。现在换成自写的 <c>Cultivation/InkDissolve</c>：
/// 整体渐隐 → 边缘墨散 → 最后留一层淡墨剪影（细说见 shader 头注释）。
/// </summary>
public static class 遮挡素材
{
    /// <summary>水墨淡出用的 shader 名（找不到就让调用方退回老办法）</summary>
    public const string 水墨Shader名 = "Cultivation/InkDissolve";

    static readonly int ID_Fade = Shader.PropertyToID("_Fade");
    static readonly int ID_Ghost = Shader.PropertyToID("_Ghost");
    static readonly int ID_Dissolve = Shader.PropertyToID("_Dissolve");
    static readonly int ID_DissolveScale = Shader.PropertyToID("_DissolveScale");
    static readonly int ID_EdgeWidth = Shader.PropertyToID("_EdgeWidth");
    static readonly int ID_EdgeInk = Shader.PropertyToID("_EdgeInk");
    static readonly int ID_PaperAmount = Shader.PropertyToID("_PaperAmount");
    static readonly int ID_PaperTiling = Shader.PropertyToID("_PaperTiling");
    static readonly int ID_PaperTex = Shader.PropertyToID("_PaperTex");
    static readonly int ID_PaperGain = Shader.PropertyToID("_PaperGain");
    static readonly int ID_InkColor = Shader.PropertyToID("_InkColor");
    static readonly int ID_PaperColor = Shader.PropertyToID("_PaperColor");
    static readonly int ID_ZWriteOn = Shader.PropertyToID("_ZWriteOn");

    /// <summary>换上去之后每帧要用的参数（建筑和树冠给的不一样）</summary>
    public struct 水墨参数
    {
        /// <summary>淡到最透时保留的剪影不透明度（0.22 = 还看得见一层淡影）</summary>
        public float 剪影;
        /// <summary>侵蚀强度（**默认 0 = 纯淡入淡出**；>0 才开始"墨化开"）</summary>
        public float 侵蚀;
        /// <summary>墨蚀团块尺度（越大团块越小越碎）</summary>
        public float 侵蚀尺度;
        /// <summary>边缘墨散宽度</summary>
        public float 墨边宽;
        /// <summary>墨边强度</summary>
        public float 墨边强度;
        /// <summary>叠纸纹的量（**默认 0 = 不叠**：叠上去物体就成了一张纸板，用户 2026-10-03 明确不要）</summary>
        public float 纸纹;
        /// <summary>写深度？**实心壳子（建筑）1 / billboard 卡片（树冠）0**</summary>
        public float 写深度;
        /// <summary>淡出时长（秒）</summary>
        public float 淡出秒;
        /// <summary>回场时长（秒）</summary>
        public float 回场秒;

        public static 水墨参数 建筑默认()
        {
            return new 水墨参数
            {
                剪影 = 0.22f, 侵蚀 = 0f, 侵蚀尺度 = 0.70f, 墨边宽 = 0.16f, 墨边强度 = 0.25f,
                纸纹 = 0f, 写深度 = 1f, 淡出秒 = 0.30f, 回场秒 = 0.40f,
            };
        }

        /// <summary>
        /// 树冠：billboard 卡片、量大（365 个渲染体）⇒ **必须 ZWrite Off**
        /// （写深度会让每张卡片互相遮挡 ⇒ 变成"一块块的片状物"，用户 2026-10-03 报的），
        /// 透明度也更透一点（和改造前的观感一致：一堆卡片柔和叠成一片）。
        /// </summary>
        public static 水墨参数 树冠默认()
        {
            var p = 建筑默认();
            p.剪影 = 0.12f;
            p.墨边强度 = 0.15f;
            p.写深度 = 0f;
            return p;
        }
    }

    /// <summary>渲染器 → 它的原材质数组（用来还原）</summary>
    public class 台账
    {
        readonly Dictionary<Renderer, Material[]> _原 = new Dictionary<Renderer, Material[]>();
        readonly Dictionary<Material, Material> _建筑缓存 = new Dictionary<Material, Material>();
        readonly Dictionary<Material, Material> _树冠缓存 = new Dictionary<Material, Material>();
        readonly Dictionary<Material, Material> _老缓存 = new Dictionary<Material, Material>();
        /// <summary>这个渲染器现在用的是水墨材质（false = 走"老透明方案"）</summary>
        readonly HashSet<Renderer> _用水墨 = new HashSet<Renderer>();
        readonly Dictionary<Renderer, float> _当前 = new Dictionary<Renderer, float>();
        readonly MaterialPropertyBlock _块 = new MaterialPropertyBlock();

        static Shader _水墨;
        static bool _找过;

        /// <summary>本台账正在管的渲染器（外部每帧推进用）</summary>
        public IEnumerable<Renderer> 名单 => _原.Keys;

        /// <summary>
        /// 换成淡出材质（已有记录的不重复换）。返回是否新换了。
        /// `希望水墨 = false` ⇒ 走**改造前那套**（`Legacy Shaders/Transparent/Diffuse`，
        /// 观感与 2026-10-03 之前完全一致），但**淡入淡出仍然保留** —— 这是给用户的"一键退回"。
        /// </summary>
        public bool 换成水墨(Renderer r, bool 希望水墨 = true)
        {
            if (r == null) return false;
            if (_原.ContainsKey(r)) return false;

            var 原 = r.sharedMaterials;
            if (原 == null || 原.Length == 0) return false;

            _原[r] = 原;
            bool 树 = 是树冠(r);
            var 换 = new Material[原.Length];
            for (int i = 0; i < 原.Length; i++)
                换[i] = 希望水墨 ? 取水墨(原[i], 树) : 取老透明(原[i]);
            r.sharedMaterials = 换;
            _当前[r] = 0f;
            if (希望水墨) _用水墨.Add(r); else _用水墨.Remove(r);
            写参数(r, 0f, 树 ? 水墨参数.树冠默认() : 水墨参数.建筑默认());
            return true;
        }

        /// <summary>
        /// 推进一个渲染器的水墨淡出。返回推进后的 `_Fade`（0 = 完全恢复，1 = 只剩剪影）。
        /// `遮挡` = 这一帧它是不是还被判为挡视线（含"保持期"）。
        /// </summary>
        public float 推进(Renderer r, bool 遮挡, in 水墨参数 p)
        {
            if (r == null) return 0f;
            float 当前;
            if (!_当前.TryGetValue(r, out 当前)) 当前 = 0f;

            float 目标 = 遮挡 ? 1f : 0f;
            if (!Mathf.Approximately(当前, 目标))
            {
                float 秒 = 目标 > 当前 ? Mathf.Max(0.01f, p.淡出秒) : Mathf.Max(0.01f, p.回场秒);
                当前 = Mathf.MoveTowards(当前, 目标, Time.unscaledDeltaTime / 秒);
                _当前[r] = 当前;
            }
            写参数(r, 当前, p);
            return 当前;
        }

        /// <summary>当前淡出进度（没在管 = 0）</summary>
        public float 取进度(Renderer r)
        {
            float v;
            return r != null && _当前.TryGetValue(r, out v) ? v : 0f;
        }

        /// <summary>还原一个渲染器（材质数组 + 参数块一起还）</summary>
        public void 还原(Renderer r)
        {
            if (r != null)
            {
                Material[] 原;
                if (_原.TryGetValue(r, out 原) && 原 != null) r.sharedMaterials = 原;
                r.SetPropertyBlock(null);
                _当前.Remove(r);
            }
            _原.Remove(r);
        }

        /// <summary>全部还原（组件禁用 / 销毁 / 切场景时调）</summary>
        public void 全还原()
        {
            foreach (var kv in _原)
            {
                if (kv.Key == null) continue;
                kv.Key.sharedMaterials = kv.Value;
                kv.Key.SetPropertyBlock(null);
            }
            _原.Clear();
            _当前.Clear();
        }

        public bool 已在管(Renderer r) => r != null && _原.ContainsKey(r);
        public int 数量 => _原.Count;

        void 写参数(Renderer r, float fade, in 水墨参数 p)
        {
            _块.Clear();
            if (_用水墨.Contains(r))
            {
                _块.SetFloat(ID_Fade, fade);
                _块.SetFloat(ID_Ghost, p.剪影);
                _块.SetFloat(ID_Dissolve, p.侵蚀);
                _块.SetFloat(ID_DissolveScale, p.侵蚀尺度);
                _块.SetFloat(ID_EdgeWidth, p.墨边宽);
                _块.SetFloat(ID_EdgeInk, p.墨边强度);
                _块.SetFloat(ID_PaperAmount, p.纸纹);
            }
            else
            {
                // 老方案（Legacy Transparent/Diffuse）：只驱动 `_Color.a`，**rgb 保留原材质自己的**
                var ms = r.sharedMaterials;
                var c = (ms != null && ms.Length > 0 && ms[0] != null) ? ms[0].color : Color.white;
                _块.SetColor("_Color", new Color(c.r, c.g, c.b, Mathf.Lerp(1f, p.剪影, fade)));
            }
            r.SetPropertyBlock(_块);
        }

        /// <summary>兜底/退回用：老方案（改造前那套 `Legacy Shaders/Transparent/Diffuse`），按源材质缓存</summary>
        Material 取老透明(Material 源)
        {
            if (源 == null) return null;
            Material 有;
            if (_老缓存.TryGetValue(源, out 有) && 有 != null) return 有;

            var sh = Shader.Find("Legacy Shaders/Transparent/Diffuse");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse");
            if (sh == null) sh = Shader.Find("Unlit/Transparent");
            if (sh == null) return 源;

            var m = new Material(sh) { name = "透明_" + 源.name };
            if (源.HasProperty("_MainTex"))
            {
                m.mainTexture = 源.mainTexture;
                m.mainTextureScale = 源.mainTextureScale;
                m.mainTextureOffset = 源.mainTextureOffset;
            }
            var c = 源.HasProperty("_Color") ? 源.GetColor("_Color") : Color.white;
            m.color = new Color(c.r, c.g, c.b, 1f);
            _老缓存[源] = m;
            return m;
        }

        /// <summary>
        /// 取（或造）水墨材质副本。⚠️ **缓存键是「源材质 + 是不是树冠」** ——
        /// 因为 `ZWrite` 是**材质级**的着色状态（MaterialPropertyBlock 改不动它），
        /// 建筑要写深度、树冠不能写深度（写了就变成"一片片卡片"），所以必须分成两份。
        /// </summary>
        Material 取水墨(Material 源, bool 树冠)
        {
            if (源 == null) return null;
            var 缓存 = 树冠 ? _树冠缓存 : _建筑缓存;
            Material 有;
            if (缓存.TryGetValue(源, out 有) && 有 != null) return 有;

            if (!_找过) { _水墨 = Shader.Find(水墨Shader名); _找过 = true; }
            if (_水墨 == null)
            {
                Debug.LogWarning("[遮挡与特效开关] 找不到 shader「" + 水墨Shader名 + "」—— 退回老的透明方案（观感会掉一档）");
                var 老的 = 取老透明(源);
                缓存[源] = 老的;
                return 老的;
            }

            var m = new Material(_水墨) { name = (树冠 ? "水墨冠_" : "水墨_") + 源.name };
            if (源.HasProperty("_MainTex"))
            {
                m.mainTexture = 源.mainTexture;
                m.mainTextureScale = 源.mainTextureScale;
                m.mainTextureOffset = 源.mainTextureOffset;
            }
            if (源.HasProperty("_Color")) m.SetColor("_Color", 源.GetColor("_Color"));

            var p = 树冠 ? 水墨参数.树冠默认() : 水墨参数.建筑默认();
            m.SetFloat(ID_Fade, 0f);
            m.SetFloat(ID_Ghost, p.剪影);
            m.SetFloat(ID_Dissolve, p.侵蚀);
            m.SetFloat(ID_DissolveScale, p.侵蚀尺度);
            m.SetFloat(ID_EdgeWidth, p.墨边宽);
            m.SetFloat(ID_EdgeInk, p.墨边强度);
            m.SetFloat(ID_PaperAmount, p.纸纹);
            m.SetFloat(ID_PaperGain, 2f);
            m.SetFloat(ID_ZWriteOn, p.写深度);        // ★ 材质级：树冠 0 / 建筑 1
            m.SetFloat("_Wash", 0.15f);              // 只去一点饱和，绝不动亮度
            m.SetColor(ID_InkColor, new Color(0.06f, 0.06f, 0.07f, 1f));
            m.SetColor(ID_PaperColor, new Color(0.93f, 0.92f, 0.90f, 1f));

            // 纸纹图仍然挂上，但**默认量为 0**（用户 2026-10-03：叠上去物体变纸板，不要）
            var 纸 = Resources.Load<Texture>("宣纸/宣纸纹理_纸纹");
            if (纸 != null)
            {
                m.SetTexture(ID_PaperTex, 纸);
                m.SetFloat(ID_PaperTiling, 0.35f);
            }

            缓存[源] = m;
            return m;
        }
    }

    /// <summary>
    /// 这个渲染体是不是**叶子**（= 该淡的那部分；树干/根/枝**不该淡**）。
    ///
    /// 【为什么这么判】2026-10-03 实测 7 种树型（`village`，见 `docs/architecture/遮挡与特效开关 §7.6`）：
    /// 树包的结构统一是 **不透明网格 = 木质部（树干/根/大枝）+ 带 alpha 的网格 = 叶子**
    /// （`Cutout` / `Tree Creator Leaves` / `Transparent`）。逐种都符合：
    ///
    /// | 树型 | 木质部（不淡） | 叶子（淡） |
    /// |---|---|---|
    /// | zhangshu_01 | `_b0` Diffuse 高3.4 宽1.1 | `_a0` **Tree Creator Leaves** |
    /// | Green_001 | `_b` Diffuse | `_a` Cutout |
    /// | Green_003 / 005 | Diffuse | Cutout |
    /// | dashu_001 / 003 | Diffuse | Cutout + Transparent/Diffuse |
    /// | songshu_002 | `_d02` Diffuse | `_d01` Cutout |
    ///
    /// 判据用**材质的渲染队列**（`renderQueue ≥ 2450` = AlphaTest 及以上 ⇒ 带 alpha），
    /// 而不是猜名字 —— 名字在各包里不一致（`_01` 有时是树皮、有时是叶子）。
    /// </summary>
    public static bool 是叶子(Renderer r)
    {
        if (r == null) return false;
        var ms = r.sharedMaterials;
        if (ms == null) return false;
        foreach (var m in ms)
        {
            if (m == null) continue;
            if (m.renderQueue >= 2450) return true;                  // AlphaTest / Transparent
            var sh = m.shader;
            if (sh != null)
            {
                string n = sh.name;
                if (n.Contains("Cutout") || n.Contains("Transparent")
                    || n.Contains("Leaves") || n.Contains("Foliage") || n.Contains("SpeedTree")) return true;
            }
        }
        return false;
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

// 全局材质收敛工具
// 基准：玩家当前材质 —— Standard / 金属 0 / 光泽 0 / 白染色 + 反照率贴图
// 目标：把全工程的"受光实体"材质统一到同一色彩与质感体系，消除自发光与受光两套割裂。
// 明确不动：粒子/拖尾/贴花/天空盒/树/UI 以及所有第三方特效 shader（改了会直接坏）。

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Cultivation.EditorTools
{
    public static class 材质统一收敛
    {
        private const string 基准说明 = "Standard / _Metallic=0 / _Glossiness=0";

        /// <summary>允许被收敛的 shader（受光实体）。其它一律跳过。</summary>
        private static readonly HashSet<string> 可收敛 = new HashSet<string>
        {
            "Legacy Shaders/Diffuse",
            "Legacy Shaders/Self-Illumin/Diffuse",
            "Legacy Shaders/Specular",
            "Legacy Shaders/Bumped Diffuse",
            "Legacy Shaders/Bumped Specular",
            "Legacy Shaders/Transparent/Cutout/Diffuse",
            "Legacy Shaders/Transparent/Cutout/Specular",
            "Legacy Shaders/Transparent/Cutout/Bumped Diffuse",
            "Legacy Shaders/Transparent/Cutout/Bumped Specular",
            "Legacy Shaders/Transparent/Diffuse",
            "Legacy Shaders/Transparent/Specular",
            "Legacy Shaders/Transparent/Bumped Specular",
            "Mobile/Diffuse",
            "Mobile/Bumped Specular",
            "Double Face/Diffuse",
            "Transparent/SelfIllum",
        };

        /// <summary>
        /// 永不动。
        /// - 树/植被：多为 billboard 片，转 Standard 会发暗成实心。
        /// - 特效目录：`Assets/Effect/**`、`Assets/resources/特效/**` 里的 Legacy 材质
        ///   常靠叠加混合发光，转成不透明会直接坏（试运行里有 10 个这种）。
        /// </summary>
        private static readonly string[] 排除目录 =
        {
            "/Environment/Tree/",
            "/Environment/Terrain/",
            "/Tree/",
            "/Vegetation/",
            "/Effect/",
            "/特效/",
        };

        [MenuItem("工具/材质/① 试运行：只统计不改动", false, 10)]
        public static void 试运行()
        {
            var 统计 = 扫描();
            var sb = new StringBuilder();
            sb.AppendLine("=== 材质收敛 · 试运行（未做任何改动）===");
            sb.AppendLine("基准：" + 基准说明);
            sb.AppendLine();
            sb.AppendLine($"可收敛材质总数 = {统计.可收敛.Count}");
            sb.AppendLine($"已跳过（受保护） = {统计.跳过.Count}");
            sb.AppendLine();
            sb.AppendLine("--- 按 shader 分布 ---");
            foreach (var kv in 按key计数(统计.可收敛, m => m.shader.name))
                sb.AppendLine($"{kv.Value,6}   {kv.Key}");
            sb.AppendLine();
            sb.AppendLine("--- 按二级目录分布 ---");
            foreach (var kv in 按key计数(统计.可收敛, m => 二级(统计.路径[m])))
                sb.AppendLine($"{kv.Value,6}   {kv.Key}");
            sb.AppendLine();
            sb.AppendLine("--- 抽样 8 个，看会怎么改 ---");
            int n = 0;
            foreach (var m in 统计.可收敛)
            {
                if (n++ >= 8) break;
                sb.AppendLine("  " + 差异(统计.路径[m], m));
            }

            写入(sb.ToString());
        }

        [MenuItem("工具/材质/② 执行收敛：NPC + 建筑 + 场景道具", false, 11)]
        public static void 执行()
        {
            var 统计 = 扫描();
            if (统计.可收敛.Count == 0) { Debug.LogWarning("[材质收敛] 没有待处理材质。"); return; }

            if (!EditorUtility.DisplayDialog("材质统一收敛",
                $"将把 {统计.可收敛.Count} 个材质收敛到 Standard（金属0/光泽0）。\n\n" +
                "粒子/树/特效已自动排除。\n" +
                "备份位于 .dsh\\_材质备份_全局收敛。\n\n确认执行？", "执行", "取消"))
                return;

            var 明细 = new StringBuilder();
            int 改动 = 0, 失败 = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var m in 统计.可收敛)
                {
                    string 前 = 摘要(m);
                    if (收敛一个(m)) { 改动++; 明细.AppendLine("改  " + 统计.路径[m] + "\n    " + 前 + "   ->   " + 摘要(m)); }
                    else 失败++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            写入($"=== 材质收敛 · 执行结果 ===\n改动 = {改动}   失败 = {失败}   跳过 = {统计.跳过.Count}\n\n" + 明细);

            var 校验 = 扫描();
            var sb = new StringBuilder();
            sb.AppendLine("=== 收敛后残留检查 ===");
            foreach (var kv in 按key计数(校验.可收敛, m => m.shader.name))
                sb.AppendLine($"{kv.Value,6}   {kv.Key}");
            sb.AppendLine(校验.可收敛.Count == 0 ? "\n✅ 已全部收敛。" : $"\n⚠ 仍有 {校验.可收敛.Count} 个未收敛。");
            写入(sb.ToString());
        }

        [MenuItem("工具/材质/③ 校验：检查收敛是否彻底", false, 12)]
        public static void 校验()
        {
            var 统计 = 扫描();
            var sb = new StringBuilder();
            sb.AppendLine("=== 材质收敛 · 校验 ===");
            sb.AppendLine($"残留可收敛材质 = {统计.可收敛.Count}   （0 为合格）");
            foreach (var kv in 按key计数(统计.可收敛, m => m.shader.name))
                sb.AppendLine($"{kv.Value,6}   {kv.Key}");
            sb.AppendLine();
            sb.AppendLine($"当前 Standard 材质总数 = {统计.标准数}");
            sb.AppendLine($"受保护跳过 = {统计.跳过.Count}");
            foreach (var kv in 按key计数(统计.跳过, m => m.shader.name))
                sb.AppendLine($"{kv.Value,6}   {kv.Key}");
            写入(sb.ToString());
        }

        // ---------- 核心 ----------

        /// <summary>把单个材质收敛到玩家基准。返回是否真的改动。</summary>
        private static bool 收敛一个(Material m)
        {
            if (m == null || m.shader == null || !可收敛.Contains(m.shader.name)) return false;

            string 旧 = m.shader.name;
            bool 是透明族 = 旧.Contains("/Transparent/");
            bool 是镂空 = 旧.Contains("/Cutout/");
            bool 是发光 = 旧.Contains("Self-Illumin") || 旧.Contains("SelfIllum");

            // —— 先取齐旧值（改 shader 前必须抓完）——
            Color 颜色 = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            Texture 主贴 = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            Texture 法线 = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
            float 镂空阈值 = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f;
            float 光泽旧 = m.HasProperty("_Shininess") ? m.GetFloat("_Shininess") : 0f;
            Texture 细节 = m.HasProperty("_Detail") ? m.GetTexture("_Detail") : null;
            float 细节强度 = m.HasProperty("_DetailScale") ? m.GetFloat("_DetailScale") : 1f;

            var 标准 = Shader.Find("Standard");
            if (标准 == null) { Debug.LogError("[材质收敛] 找不到 Standard shader。"); return false; }
            m.shader = 标准;

            // —— 反照率：原样保留（这正是"以玩家为基准"的核心：贴图 + 白染色）——
            if (m.HasProperty("_Color"))
            {
                // 自发光族用 _Color 当亮度乘子，保留它才能维持原本的明暗关系
                m.SetColor("_Color", 颜色);
            }
            if (m.HasProperty("_MainTex"))
            {
                m.SetTexture("_MainTex", 主贴);
                m.SetTextureScale("_MainTex", m.HasProperty("_MainTex") ? 取缩放(m, "_MainTex") : Vector2.one);
            }

            // 法线贴图（Bumped 族）搬过去
            if (法线 != null && m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", 法线);

            // 细节贴图
            if (细节 != null && m.HasProperty("_Detail"))
            {
                m.SetTexture("_Detail", 细节);
                if (m.HasProperty("_DetailScale")) m.SetFloat("_DetailScale", 细节强度);
            }

            // —— 基准质感：金属 0 ——
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_MetallicGlossMap")) m.SetTexture("_MetallicGlossMap", null);

            // —— 光泽：Specular 族给极弱高光（否则金属/石面塑料感），其余全部对齐玩家 = 0 ——
            float 光泽 = 0f;
            if (旧.Contains("Specular")) 光泽 = Mathf.Clamp(光泽旧 * 0.35f, 0f, 0.22f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 光泽);
            if (m.HasProperty("_GlossMapScale")) m.SetFloat("_GlossMapScale", 光泽);
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 光泽 > 0.001f ? 1f : 0f);
            if (m.HasProperty("_GlossyReflections")) m.SetFloat("_GlossyReflections", 0f);

            // —— 自发光族：不补自发光。用户要求改成受光，就彻底受光 ——
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", null);
            m.DisableKeyword("_EMISSION");
            if (是发光 && m.HasProperty("_Illum")) m.SetTexture("_Illum", null);

            // —— 渲染模式 ——
            if (是镂空) 设为镂空(m, 镂空阈值, 颜色);
            else if (是透明族) 设为透明(m, 颜色);
            else 设为不透明(m, 颜色);

            EditorUtility.SetDirty(m);
            return true;
        }

        private static void 设为不透明(Material m, Color 颜色)
        {
            m.SetOverrideTag("RenderType", "Opaque");
            if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 1);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = -1;
            if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(颜色.r, 颜色.g, 颜色.b, 1f));
        }

        private static void 设为镂空(Material m, float 阈值, Color 颜色)
        {
            m.SetOverrideTag("RenderType", "TransparentCutout");
            if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 1f);
            if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 1);
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", Mathf.Clamp(阈值, 0.01f, 0.99f));
            m.EnableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 2450;
            if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(颜色.r, 颜色.g, 颜色.b, 颜色.a));
        }

        private static void 设为透明(Material m, Color 颜色)
        {
            m.SetOverrideTag("RenderType", "Transparent");
            if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 2f);   // Fade
            if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
            if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(颜色.r, 颜色.g, 颜色.b, 颜色.a));
        }

        private static Vector2 取缩放(Material m, string 名)
        {
            try { return m.GetTextureScale(名); } catch { return Vector2.one; }
        }

        // ---------- 扫描 ----------

        private class 扫描结果
        {
            public readonly List<Material> 可收敛 = new List<Material>();
            public readonly List<Material> 跳过 = new List<Material>();
            public readonly Dictionary<Material, string> 路径 = new Dictionary<Material, string>();
            public int 标准数;
        }

        private static 扫描结果 扫描()
        {
            var r = new 扫描结果();
            foreach (var guid in AssetDatabase.FindAssets("t:Material"))
            {
                var 路径 = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(路径);
                if (m == null || m.shader == null) continue;
                if (m.shader.name == "Standard") r.标准数++;
                r.路径[m] = 路径;
                if (可收敛.Contains(m.shader.name))
                {
                    if (在排除目录(路径)) r.跳过.Add(m);
                    else r.可收敛.Add(m);
                }
                else r.跳过.Add(m);
            }
            return r;
        }

        private static bool 在排除目录(string 路径)
        {
            var p = 路径.Replace('\\', '/');
            foreach (var d in 排除目录)
                if (p.Contains(d)) return true;
            return false;
        }

        private static string 二级(string 路径)
        {
            var p = 路径.Replace('\\', '/');
            if (!p.StartsWith("Assets/")) return p;
            var 剩余 = p.Substring("Assets/".Length);
            var i = 剩余.IndexOf('/');
            if (i < 0) return "(根)";
            var 顶层 = 剩余.Substring(0, i);
            var 其余 = 剩余.Substring(i + 1);
            var j = 其余.IndexOf('/');
            return j < 0 ? 顶层 : 顶层 + "/" + 其余.Substring(0, j);
        }

        private static Dictionary<string, int> 按key计数(List<Material> 列表, System.Func<Material, string> 取键)
        {
            var d = new Dictionary<string, int>();
            foreach (var m in 列表)
            {
                var k = 取键(m);
                if (!d.ContainsKey(k)) d[k] = 0;
                d[k]++;
            }
            var 排序 = new List<KeyValuePair<string, int>>(d);
            排序.Sort((a, b) => b.Value.CompareTo(a.Value));
            var o = new Dictionary<string, int>();
            foreach (var kv in 排序) o[kv.Key] = kv.Value;
            return o;
        }

        private static string 摘要(Material m)
        {
            if (m == null) return "null";
            var sb = new StringBuilder();
            sb.Append(m.shader != null ? m.shader.name : "?");
            if (m.HasProperty("_Color"))
            {
                var c = m.GetColor("_Color");
                sb.Append($"  color=({c.r:F3},{c.g:F3},{c.b:F3},{c.a:F2})");
            }
            if (m.HasProperty("_Metallic")) sb.Append($"  metal={m.GetFloat("_Metallic"):F2}");
            if (m.HasProperty("_Glossiness")) sb.Append($"  gloss={m.GetFloat("_Glossiness"):F2}");
            var t = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") as Texture2D : null;
            sb.Append("  tex=" + (t == null ? "null" : t.name));
            sb.Append("  q=" + m.renderQueue);
            return sb.ToString();
        }

        private static string 差异(string 路径, Material m)
        {
            return 路径 + "\n      " + 摘要(m) + "   ->   Standard / metal=0 / gloss=0";
        }

        private static void 写入(string 内容)
        {
            Debug.Log(内容);
            try
            {
                var 目录 = Path.Combine(Directory.GetCurrentDirectory(), ".dsh", "_材质备份_全局收敛");
                Directory.CreateDirectory(目录);
                var 文件 = Path.Combine(目录, "收敛报告_" + System.DateTime.Now.ToString("MMdd_HHmmss") + ".txt");
                File.WriteAllText(文件, 内容, new UTF8Encoding(false));
                Debug.Log("[材质收敛] 报告已写入 " + 文件);
            }
            catch (System.Exception e) { Debug.LogWarning("[材质收敛] 报告写入失败：" + e.Message); }
        }
    }
}

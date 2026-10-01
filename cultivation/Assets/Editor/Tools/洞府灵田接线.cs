// 洞府灵田主线接线工具（第一阶段「洞府安顿 → 解锁灵田」）
//
// 做三件事（都是**幂等**的，可以反复跑）：
//
//   ① 在 Sect 场景里放一个 `洞府灵田点位` 标记 ——
//      运行时会由 `灵田.按点位摆好()` 把灵田摆到那儿（见 灵田.cs）
//   ② 往 `任务表.csv` 追加主线阶段（接在 `q_main_004_17 前往个人洞府` 之后）
//   ③ 往 `对话表.csv` 追加对应台词
//
// 做完必须跑一次「修仙 / 从配置表生成资产」才生效（本工具会自动调）。

using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cultivation.EditorTools
{
    public static class 洞府灵田接线
    {
        const string 场景路径 = "Assets/Scenes/3C_Testbed.scene";
        const string 点位名 = "洞府灵田点位";
        /// <summary>主广场空地（实测：碰撞地板 y≈-1、四周无建筑）。改这里就能挪灵田</summary>
        /// <summary>洞府里灵田的位置。实测 (-12,0,6) 半径 3m 内无障碍（西侧空地）</summary>
        static readonly Vector3 灵田点位 = new Vector3(-12f, 0f, 6f);
        const string 任务表 = "Assets/Data/Tables/任务表.csv";
        const string 对话表 = "Assets/Data/Tables/对话表.csv";

        // ============================================================ ① 场景点位

        [MenuItem("工具/主线/① 在宗门放「洞府灵田点位」", false, 60)]
        public static void 放点位()
        {
            var 当前 = SceneManager.GetActiveScene();
            if (当前.path != 场景路径)
            {
                if (当前.isDirty && !EditorUtility.DisplayDialog("场景未保存",
                        "当前场景有未保存改动，先保存再切到宗门？", "保存并切换", "取消")) return;
                if (当前.isDirty) EditorSceneManager.SaveScene(当前);
                EditorSceneManager.OpenScene(场景路径, OpenSceneMode.Single);
            }

            // 已有就不重复放
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name == 点位名)
                {
                    Debug.Log("[洞府灵田接线] 点位已存在：" + go.transform.position);
                    return;
                }

            // ⚠️【踩过的坑·2026-10-01】**洞府就是 `3C_Testbed`，这一点文档里早就写了！**
            //
            //   `docs/design/主线剧情.md` 第五幕：
            //     「大师兄（御风前进）至 `sect1 to 3c` 旁边」
            //     「你从这道光进去就好啦」→ 任务目标「前往个人洞府」
            //   ⇒ `3C_Testbed` 就是个人洞府。
            //
            //   我一开始没查文档，先后把它错放到 **宗门主广场**、又差点**新建一个
            //   `Cave.scene`** —— 两个都错。**遇到"这个地方在哪"先 grep 文档。**
            //
            //   洞府实况：`Level` 地面 44×44、`cultivation room` 在 (12.4,0,-1.4)、
            //   木桩在 (-3,0,3) 一带、传送点 `3ctestbed to sect1` 在 (-8,0,-11.3)。
            //   灵田放**西侧空地 (-12,0,6)**：实测半径 3m 内无障碍。
            Vector3 目标 = 灵田点位;

            // 贴到真实地板（只在固定点附近小范围修正高度）
            if (Physics.Raycast(目标 + Vector3.up * 20f, Vector3.down, out var 命中, 60f,
                    ~0, QueryTriggerInteraction.Ignore)
                && 命中.collider.name.Contains("地板"))
                目标 = 命中.point;

            var 标记 = new GameObject(点位名);
            标记.transform.position = 目标;
            标记.transform.rotation = Quaternion.Euler(0f, 90f, 0f);   // 田块朝广场

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log($"[洞府灵田接线] 已在 {场景路径} 放下「{点位名}」，位置 {目标}");
        }

        // ============================================================ ②③ 表

        [MenuItem("工具/主线/② 追加「洞府灵田」主线阶段 + 对话", false, 61)]
        public static void 追加阶段()
        {
            int 加阶段 = 追加任务阶段();
            int 加对话 = 追加对话();
            Debug.Log($"[洞府灵田接线] 任务表 +{加阶段} 行、对话表 +{加对话} 行。"
                      + "接下来自动跑「从配置表生成资产」");
            跑导入();
        }

        static int 追加任务阶段()
        {
            var 行 = 读表(任务表);
            if (行.Count == 0) { Debug.LogError("[洞府灵田接线] 任务表读不到"); return 0; }
            var 头 = 行[0].Split(',');
            int 列数 = 头.Length;
            var 已有id = new HashSet<string>();
            for (int i = 1; i < 行.Count; i++)
            {
                var f = 行[i].Split(',');
                if (f.Length > 0) 已有id.Add(f[0]);
            }

            // 统一的任务列值（和 q_main_004 其余阶段一致）
            const string 任务id = "q_main_004";
            const string 任务名 = "拜入太虚宗";
            const string 类型 = "主线";

            var 新 = new List<string>
            {
                // 阶段18：到洞府后大师兄开口（对话由玩家按 F 触发）
                建行("q_main_004_18", 任务id, 任务名, 类型, 18,
                   阶段名: "灵田初见",
                   说明: "大师兄说，修行根基在于自给自足。",
                   条件: "对话", 对话id: "dlg_act5_lingtian_1",
                   完成加标记: "q_主线_灵田已解锁"),

                // 阶段19：大师兄御风飞向后山灵田
                建行("q_main_004_19", 任务id, 任务名, 类型, 19,
                   阶段名: "带你去后山",
                   说明: "跟上大师兄，去洞府后山的灵田。",
                   条件: "NPC到位", 目标npcId: "npc_dashixiong",
                   动作: "飞到", 动作目标npcId: "npc_dashixiong",
                   动作参数: "Yufeng_Forward", 坐标X: 0, 坐标Y: 0, 坐标Z: 0, 等待秒: 0),

                // 阶段20：教学对话（强制播放，不用按 F）
                建行("q_main_004_20", 任务id, 任务名, 类型, 20,
                   阶段名: "传你灵植心法",
                   说明: "大师兄把灵植培育心法传给你。",
                   条件: "等待秒数", 等待秒: 0.5f,
                   动作: "播放对话",
                   台词: "这片灵田，往后就是你的本命之地。|宗门弟子修行，根基在自给自足——灵石、灵药，皆出于此。"
                        + "|这一阶下品的灵田虽薄，好在无需照看，种下去自会长成。|我留些灵种与滋养符箓与你。"
                        + "|每日打理片刻即可，莫要误了修行。|按 F4 便能查看灵田。",
                   说话人: "大师兄"),

                // 阶段21：赠灵种 + 收尾说明
                建行("q_main_004_21", 任务id, 任务名, 类型, 21,
                   阶段名: "收下灵种",
                   说明: "灵草成熟后会自动留存，一键便可收取。",
                   条件: "无",
                   奖励物品: "item_seed_shengling:5|item_seed_ninglu:3|item_shenglingcao:6|item_ningluhua:4",
                   完成加标记: "q_主线_灵田已开启"),

                // ── 第二阶段铺垫（炼丹阁）──
                //
                // 【为什么只到"带你看丹房 + 传丹方"为止】
                // 第二阶段的**策划原文还没写**（`lore/炼丹系统说明.txt` 只有"待开发"）。
                // 所以这里只接**能确定的**：过一天后大师兄来带你去炼丹阁，把一阶丹方给你。
                // 更细的授业剧情等策划补文档再加 —— 不自己编。
                //
                // 【用时间系统做条件】用户要求「安顿次日，大师兄如约前来」——
                // 所以阶段 23 用 `等待秒数`，在现实里很快（10 分钟一天），
                // 但语义上是"过了一天"。真正卡"次日"要用日期条件，
                // 现在任务条件枚举里没有"过了一天"，先用等待秒数占位。
                建行("q_main_004_23", 任务id, 任务名, 类型, 23,
                   阶段名: "次日清晨",
                   说明: "过了一夜，大师兄如约前来。",
                   条件: "等待秒数", 等待秒: 3f,
                   完成加标记: "q_主线_次日"),

                建行("q_main_004_24", 任务id, 任务名, 类型, 24,
                   阶段名: "带你去看丹房",
                   说明: "大师兄带你熟悉宗门炼丹阁。",
                   条件: "等待秒数", 等待秒: 0.5f,
                   动作: "播放对话",
                   台词: "小师弟，醒了？|今日带你去认认丹房——你灵田里那些灵草，往后都要送到这里来。"
                        + "|炼丹要一味主材、几味辅材，还要耗你自己的灵气。|同一炉丹，品的越高越难成，可药力也越厚。"
                        + "|我把这几张一阶丹方留给你，剩下的，你自己摸索。",
                   说话人: "大师兄"),

                建行("q_main_004_25", 任务id, 任务名, 类型, 25,
                   阶段名: "丹方入手",
                   说明: "去宗门炼丹阁（走近按 F）试试炼丹。",
                   条件: "无",
                   完成加标记: "q_主线_丹房已解锁"),

                // 阶段22：引出第二阶段（炼器 / 炼丹）
                建行("q_main_004_22", 任务id, 任务名, 类型, 22,
                   阶段名: "灵田已开",
                   说明: "灵草可炼丹，可炼器。改日大师兄再教你。",
                   条件: "无",
                   动作: "播放对话",
                   台词: "灵草成熟，便可拿去炼丹、炼器。|这些门道，改日我再教你。",
                   说话人: "大师兄",
                   完成加标记: "q_主线_待炼器炼丹"),
            };

            int 计数 = 0;
            foreach (var l in 新)
            {
                var f = l.Split(',');
                if (已有id.Contains(f[0])) continue;
                // 补齐到列数（多的逗号会被 Split 吃掉，所以直接用原始串补尾逗号）
                int 现列 = l.Split(',').Length;
                行.Add(l + new string(',', Mathf.Max(0, 列数 - 现列)));
                计数++;
            }
            if (计数 > 0) 写表(任务表, 行);
            return 计数;
        }

        /// <summary>按列名拼一行（只填需要的列，其余留空）</summary>
        static string 建行(string id, string 任务id, string 任务名, string 类型, int 阶段,
                         string 阶段名 = "", string 说明 = "", string 条件 = "",
                         string 目标npcId = "", string 对话id = "",
                         string 完成加标记 = "", string 奖励物品 = "",
                         string 动作 = "", string 动作目标npcId = "", string 动作参数 = "",
                         string 台词 = "", string 说话人 = "",
                         float 坐标X = 0, float 坐标Y = 0, float 坐标Z = 0, float 等待秒 = 0)
        {
            // 列顺序照 任务表.csv 的表头：
            // id,任务id,任务名,类型,阶段,阶段名,说明,条件,物品id,数量,目标npcId,
            // 坐标X,坐标Y,坐标Z,到达半径,对话id,接取加标记,完成加标记,打标记,奖励物品,
            // 动作,动作目标npcId,动作参数,自动接取,前置任务id,等待秒,动作速度,镜头时长,镜头高度,
            // 台词,说话人,情绪,情绪强度,场景名,淡入档数,淡出档数,打字速度
            string[] 格 = new string[37];
            for (int i = 0; i < 格.Length; i++) 格[i] = "";
            格[0] = id; 格[1] = 任务id; 格[2] = 任务名; 格[3] = 类型; 格[4] = 阶段.ToString();
            格[5] = 阶段名; 格[6] = 说明; 格[7] = 条件;
            格[10] = 目标npcId;
            if (坐标X != 0 || 坐标Y != 0 || 坐标Z != 0)
            { 格[11] = 坐标X.ToString("0.###"); 格[12] = 坐标Y.ToString("0.###"); 格[13] = 坐标Z.ToString("0.###"); }
            格[15] = 对话id; 格[17] = 完成加标记; 格[19] = 奖励物品;
            格[20] = 动作; 格[21] = 动作目标npcId; 格[22] = 动作参数;
            if (等待秒 > 0) 格[25] = 等待秒.ToString("0.##");
            格[29] = 台词; 格[30] = 说话人;
            return string.Join(",", 格);
        }

        static int 追加对话()
        {
            var 行 = 读表(对话表);
            if (行.Count == 0) { Debug.LogError("[洞府灵田接线] 对话表读不到"); return 0; }
            int 列数 = 行[0].Split(',').Length;
            var 已有id = new HashSet<string>();
            for (int i = 1; i < 行.Count; i++)
            {
                var f = 行[i].Split(',');
                if (f.Length > 0) 已有id.Add(f[0]);
            }

            // 格式：id,npcId,分段,说话人,文本,立绘,玩家立绘,回答1..3,跳转1..3,需要标记,排除标记,优先,情绪,情绪强度,触发任务,完成任务
            var 新 = new List<string>
            {
                "dlg_act5_lingtian_1,npc_dashixiong,1,,（大师兄随你走进后山，指着那片薄田。）|这里，便是你的灵田了。,立绘/大师兄,,嗯,,,2,0,0,q_主线_到洞府,,41,,1,,,,",
                "dlg_act5_lingtian_2,npc_dashixiong,2,,宗门弟子修行，根基在自给自足。|灵石、灵药，皆出于此——往后炼丹、炼器、修炼，都要靠它。,立绘/大师兄,,原来如此,,,3,0,0,,,0,,1,,,,",
                "dlg_act5_lingtian_3,npc_dashixiong,3,,这一阶下品的灵田虽薄，好在无需照看，种下去自会长成。|我留些灵种与滋养符箓与你，每日打理片刻即可。,立绘/大师兄,,多谢师兄,,,4,0,0,,,0,,1,,,,",
                "dlg_act5_lingtian_4,npc_dashixiong,4,,灵田可随你的修为与宗门贡献升阶。|阶高了，能种的灵药更珍稀，长得也更快。,立绘/大师兄,,弟子记下了,,,0,0,0,,,0,,1,,,,",
            };

            int 计数 = 0;
            foreach (var l in 新)
            {
                var f = l.Split(',');
                if (已有id.Contains(f[0])) continue;
                int 现列 = l.Split(',').Length;
                行.Add(l + new string(',', Mathf.Max(0, 列数 - 现列)));
                计数++;
            }
            if (计数 > 0) 写表(对话表, 行);
            return 计数;
        }

        // ============================================================ 工具

        static List<string> 读表(string 路径)
        {
            var 全 = Path.Combine(Directory.GetCurrentDirectory(), 路径);
            if (!File.Exists(全)) return new List<string>();
            return new List<string>(File.ReadAllLines(全, new UTF8Encoding(true)));
        }

        static void 写表(string 路径, List<string> 行)
        {
            var 全 = Path.Combine(Directory.GetCurrentDirectory(), 路径);
            File.WriteAllText(全, string.Join("\r\n", 行) + "\r\n", new UTF8Encoding(true));
            AssetDatabase.ImportAsset(路径, ImportAssetOptions.ForceUpdate);
        }

        static void 跑导入()
        {
            var t = System.AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
                .FirstOrDefault(x => x.Name == "DataTableImporter");
            var m = t?.GetMethod("ImportAll");
            if (m == null) { Debug.LogWarning("[洞府灵田接线] 找不到 DataTableImporter.ImportAll，请手动跑菜单「修仙/从配置表生成资产」"); return; }
            m.Invoke(null, null);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 存档读写。每个槽位一个 JSON 文件，放在 Application.persistentDataPath/saves 下。
///
/// 用 JsonUtility 而不是自己拼字符串：字段增删时有版本号兜底，
/// 缺的字段会拿到默认值，不会因为旧存档少一个字段就整个读不出来。
/// </summary>
public static class SaveSystem
{
    /// <summary>存档槽位数。UI 上就是一排格子，改这里 UI 会自动跟着变</summary>
    public const int 槽位数 = 5;

    public static string 存档目录 => Path.Combine(Application.persistentDataPath, "saves");

    public static string 槽位路径(int 槽位) => Path.Combine(存档目录, "slot_" + 槽位 + ".json");

    // ---------------------------------------------------------------- 读写

    public static bool 存档(int 槽位, SaveData 数据)
    {
        if (数据 == null) return false;
        if (槽位 < 0 || 槽位 >= 槽位数) { Debug.LogWarning("[SaveSystem] 槽位越界 " + 槽位); return false; }

        try
        {
            Directory.CreateDirectory(存档目录);
            数据.版本 = SaveData.当前版本;
            数据.刷新时间戳();
            File.WriteAllText(槽位路径(槽位), JsonUtility.ToJson(数据, true));
            Debug.Log("[SaveSystem] 已存档槽位 " + 槽位 + "：" + 数据.角色名 + " / " + 数据.境界);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[SaveSystem] 存档失败：" + e.Message);
            return false;
        }
    }

    /// <summary>读槽位。没有存档或读失败都返回 null</summary>
    public static SaveData 读档(int 槽位)
    {
        var p = 槽位路径(槽位);
        if (!File.Exists(p)) return null;

        try
        {
            var 数据 = JsonUtility.FromJson<SaveData>(File.ReadAllText(p));
            if (数据 == null) return null;
            if (数据.版本 > SaveData.当前版本)
                Debug.LogWarning("[SaveSystem] 槽位 " + 槽位 + " 的存档版本 " + 数据.版本 + " 比程序新，可能读不全");
            return 数据;
        }
        catch (Exception e)
        {
            Debug.LogError("[SaveSystem] 读档失败（槽位 " + 槽位 + "）：" + e.Message);
            return null;
        }
    }

    public static bool 有存档(int 槽位)
    {
        var 数据 = 读档(槽位);
        return 数据 != null && !数据.是空的;
    }

    public static bool 删档(int 槽位)
    {
        var p = 槽位路径(槽位);
        if (!File.Exists(p)) return false;
        try { File.Delete(p); return true; }
        catch (Exception e) { Debug.LogError("[SaveSystem] 删档失败：" + e.Message); return false; }
    }

    /// <summary>
    /// 删掉一个槽位，并让后面的存档【依次往前补位】，不留空档。
    ///
    /// 例：槽位 0/1/2 有档，删掉 0 → 原来的 1 变成 0、原来的 2 变成 1，最后空出来。
    /// 补位立刻落盘，所以下次打开面板看到的就是紧凑的一列。
    /// </summary>
    public static bool 删档并补位(int 槽位)
    {
        if (槽位 < 0 || 槽位 >= 槽位数) return false;
        if (!删档(槽位)) return false;

        for (int i = 槽位; i < 槽位数 - 1; i++)
        {
            var 下一个 = 读档(i + 1);
            if (下一个 == null) { 删档(i); continue; }   // 后面本来就空，顺手清掉
            存档(i, 下一个);
        }
        删档(槽位数 - 1);   // 整体前移后，最后一格必定空出来

        Debug.Log("[SaveSystem] 已删除槽位 " + 槽位 + "，后面的存档已依次补位");
        return true;
    }

    /// <summary>读全部槽位，没存档的位置是 null</summary>
    public static SaveData[] 读全部()
    {
        var 全部 = new SaveData[槽位数];
        for (int i = 0; i < 槽位数; i++) 全部[i] = 读档(i);
        return 全部;
    }

    // ---------------------------------------------------------------- 新档

    /// <summary>
    /// **这一局是从"新游戏"开起来的**（主菜单点了空槽 → <see cref="建新档"/>）。
    ///
    /// 用途：`主线开场` 只在**新开局**演那三句黑幕 + 起名；
    /// 读档进来、或者从别的场景回到古古镇，都**不该**再演。
    ///
    /// ⚠️ 不能用 `当前存档 != null` 来判断"是不是新开局"（踩过的坑 2026-09-27）：
    ///   新游戏模式下 `MainMenuUI` 也会先 `建新档()` 再赋给 `当前存档`，
    ///   所以 `当前存档` 两种情况下都非 null —— 用它判断会把**新开局的开场也挡掉**
    ///   （用户报："开新存档又没有开局简介的黑幕了"）。
    ///
    /// `主线开场` 消费（读）这个标记，读完不影响它 —— 因为 `建新档` 只在主菜单调，
    /// 一局里不会再调第二次；重进 Play 由 `RuntimeInitializeOnLoadMethod` 归零。
    /// </summary>
    public static bool 本局是新开局;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void 重置新开局标记() { 本局是新开局 = false; }

    /// <summary>
    /// 建一份新档并初始化角色数据。
    /// 属性快照这一层等「属性结算系统」做好后会替换掉。
    /// </summary>
    public static SaveData 建新档(string 角色名 = null)
    {
        本局是新开局 = true;                  // ★ 打上"这是新开局"的标志（供 主线开场 判断）
        var 数据 = new SaveData
        {
            角色名 = string.IsNullOrEmpty(角色名) ? "无名散修" : 角色名,
            境界 = "炼气期一层",
            场景名 = "village",
            位置 = Vector3.zero,
            朝向Y = 0f,
            当前气血 = -1f,      // -1 = 用满值
            当前灵气 = -1f,
            // ★ 新档**没有功法**（用户 2026-09-27 确认的设计）：
            //   主角开局什么功法都没有，第一门功法要靠在背包里「使用」秘籍类物品学会，
            //   学会第一门时它才会成为「当前修炼的功法」（见 学功法效果.使用）。
            //   原来这里写死 "gongfa_taixu_lianqi" —— 那和"已学功法为空"自相矛盾，
            //   而且 应用到角色 里有 `if (功法 != null) 面板.当前功法 = 功法;`，
            //   一旦取得到就会让主角**凭空多出一门功法**。
            功法id = "",
        };

        // 太虚炼气诀自带的基础属性快照（对应 PlayerDefaultStats 的一组值）
        var 默认 = new (string 键, float 值)[]
        {
            ("攻击", 42f), ("气血", 320f), ("灵力", 160f),
            ("防御", 12f), ("暴击率", 0.05f), ("神识", 12.5f), ("吐纳", 2.4f),
        };
        foreach (var (键, 值) in 默认) 数据.写属性(键, 值);

        // ★ 新档：背包必须是空的（用户 2026-09-27）。
        //   场景里的 CharacterUI 可能被预先塞了东西（例如跑过
        //   「修仙/物品/把能力物品塞进当前场景背包」那个调试菜单），
        //   这里显式清一次，保证新角色背包里什么都没有。
        面板内容全清(数据);

        return 数据;
    }

    /// <summary>
    /// 新档：把面板相关的进度全部置空。
    /// 背包 / 法宝 / 灵阵 / 坐骑 / 技能槽 / 停用被动 / 已获真灵 / 战阵 / 经验 / 任务进度 / 对话标记
    /// —— 全部按"什么都没有"起手（用户要求：最开始创建的角色背包里是没东西的）。
    /// </summary>
    static void 面板内容全清(SaveData 数据)
    {
        数据.背包物品.Clear();
        数据.法宝.Clear();
        数据.灵阵.Clear();
        数据.坐骑.Clear();
        数据.当前坐骑 = "";
        数据.主动技能槽.Clear();
        数据.已停用被动.Clear();
        数据.已获得真灵.Clear();
        数据.战阵站位.Clear();
        数据.当前经验 = 0;
        数据.突破所需总经验 = 0;
        数据.任务进度 = "";
        数据.对话标记 = "";
        // 镇妖塔：新角色从第 1 层开始（版本 6）
        数据.镇妖塔当前层 = 1;
        数据.镇妖塔最高层 = 1;
    }

    /// <summary>把一份档套到当前场景里的角色身上。属性结算系统做好前，只恢复能恢复的那几项</summary>
    public static void 应用到角色(SaveData 数据)
    {
        if (数据 == null) return;
        当前存档 = 数据;

        var 玩家 = GameObject.Find("Player");
        if (玩家 == null) return;

        // ---- 纪年 / 修炼机会 / 灵田（版本 8）----
        // 【为什么老档要重置而不是套 0】版本 < 8 的档这些字段根本不存在（默认 0/空），
        // 直接套上去会让"今天的机会"永远不发。所以老档走 重置()，让它从第 0 天干净起步。
        var 时间 = 时间管理器.取();
        if (时间 != null)
        {
            if (数据.是空的 || 数据.版本 < 8) 时间.重置();
            else 时间.导入(数据.天数, 数据.日内进度, 数据.日常机会, 数据.打怪机会, 0);
        }

        var 田 = 灵田.取();
        if (田 != null)
        {
            // 【版本 11 起】灵田是玩家自己在洞府里摆的，每块地带**位置与朝向**。
            // 版本 < 11 的档是"固定 12 块地"那套（没有位置）—— 直接重新开局。
            // 用户已明确「不用补老档兼容，老档可以全部删掉」。
            if (数据.是空的 || 数据.版本 < 11) 田.导入(null);
            else 田.导入(数据.灵田地块);
        }

        // 【版本 12 起】摆设（练功木桩这类玩家摆下来的物件）
        var 摆 = 摆设.取();
        if (摆 != null)
        {
            if (数据.是空的 || 数据.版本 < 12) 摆.导入(null);
            else 摆.导入(数据.摆设);
        }

        // 【版本 13 起】宗门贡献（兑换用货币）。加法字段：老档读出来是 0，不用版本门槛
        宗门贡献.从存档设置(数据.是空的 ? 0 : 数据.宗门贡献);

        // 【版本 14 起】日常清单 + 两大伏笔等级。同样是加法字段：
        //   老档 ⇒ 天 -1 / 掩码 0 / 两条伏笔 0 级（= 今天还没做事、伏笔还没开始发酵）
        if (日常循环.取() != null)
            日常循环.取().导入(数据.是空的 ? -1 : 数据.日常_天, 数据.是空的 ? 0 : 数据.日常_掩码);
        if (伏笔管理器.取() != null)
            伏笔管理器.取().导入(数据.是空的 ? 0 : 数据.伏笔_灵田, 数据.是空的 ? 0 : 数据.伏笔_塔);

        var cc = 玩家.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        // ★ 切场景接力期间**不要**用存档里的位置覆盖玩家：
        //   跨场景传送的目标是「传送到目的地的落点」，而存档位置是"上次存档时站的地方"。
        //   用存档位置盖会**把玩家从落点搬回存档点**（用户 2026-09-27 报的
        //   「传送到个人洞府后没生成在应该在的落点上」就是这个）。
        //   落点由 黑幕字幕 的场景过渡负责，读档时才用存档位置。
        if (跨场景数据.正在接力)
        {
            Debug.Log("[存档] 正在跨场景接力 → 跳过「按存档位置摆放玩家」（落点由切场景过渡负责）");
        }
        else if (数据.位置有效)
        {
            玩家.transform.position = 数据.位置;
        }
        else if (数据.位置 != Vector3.zero)
        {
            // 兼容版本 6 及以前的旧档（那时没有 位置有效 标志，用 zero 当哨兵）
            玩家.transform.position = 数据.位置;
        }
        玩家.transform.rotation = Quaternion.Euler(0f, 数据.朝向Y, 0f);

        // 外观：static 跨场景能活、跨读档活不了，所以读档要显式恢复
        if (!string.IsNullOrEmpty(数据.外观id))
        {
            var 外观组件 = 玩家.GetComponent<玩家外观>();
            玩家外观.从存档设置外观(数据.外观id, 外观组件);
        }
        if (cc != null) cc.enabled = true;

        // 资源
        var 生命 = 玩家.GetComponent<PlayerVitals>();
        if (生命 != null)
        {
            if (数据.当前灵气 >= 0f) 生命.CurrentSpirit = 数据.当前灵气;
            if (数据.当前气血 >= 0f) 生命.CurrentHealth = 数据.当前气血;
        }

        // ---- 修炼系统 ----
        var 修炼 = 玩家.GetComponent<PlayerCultivation>();
        if (修炼 != null)
        {
            修炼.设置总灵气(数据.总灵气);
            修炼.设置修炼次数(数据.修炼次数累积);
            // 破境封顶要**在设灵气之前**恢复，否则 查境界 会按默认 1 级把等级压掉
            修炼.已解锁最高等级 = Mathf.Clamp(数据.已解锁最高等级, 1, 90);
            修炼.破境加成 = Mathf.Clamp01(数据.破境加成);   // 版本 9：服丹留下的那份加成

            var 面板 = 修炼.面板数据;
            if (面板 != null)
            {
                // 功法按 id 还原（取不到就保持当前）
                var 功法 = 修炼.取功法(数据.功法id);
                if (功法 != null) 面板.当前功法 = 功法;

                // 已学功法（按 id 还原；存档为空时保持默认=全学）
                if (数据.已学功法 != null && 数据.已学功法.Count > 0)
                {
                    var 还原 = new System.Collections.Generic.List<GongFaDefinition>();
                    foreach (var id in 数据.已学功法)
                    {
                        var g = 修炼.取功法(id);
                        if (g != null && !还原.Contains(g)) 还原.Add(g);
                    }
                    if (还原.Count > 0) 面板.已学功法 = 还原;
                }

                // ★ 已获得的能力：**以存档为准**（新档就是空的 —— 用户要求"学了才有"）
                面板.EnsureLists();
                面板.已获得主动神通 = new System.Collections.Generic.List<ActiveDivineAbility>();
                if (数据.已获得主动神通 != null)
                    foreach (var id in 数据.已获得主动神通)
                        foreach (var a in 面板.神通)
                            if (a is ActiveDivineAbility act && act.神通id == id && !面板.已获得主动(act)) 面板.已获得主动神通.Add(act);
                面板.已获得被动神通 = new System.Collections.Generic.List<PassiveDivineAbility>();
                if (数据.已获得被动神通 != null)
                    foreach (var id in 数据.已获得被动神通)
                        foreach (var a in 面板.神通)
                            if (a is PassiveDivineAbility ps && ps.神通id == id && !面板.已获得被动(ps)) 面板.已获得被动神通.Add(ps);

                面板.RaiseChanged();
            }

            // 让普攻组件按新功法重装
            var 装载 = 玩家.GetComponent<PlayerAbilityLoader>();
            if (装载 != null) 装载.Refresh();

            Debug.Log("[存档] 已恢复修为：总灵气 " + 数据.总灵气
                + "｜次数 " + 数据.修炼次数累积.ToString("0.##")
                + "｜境界 " + 修炼.境界名, 玩家);
        }

        // ---- 面板数据（背包 / 装备 / 战阵都挂在它上面）----
        var 面板数据 = UnityEngine.Object.FindObjectOfType<UIPanelData>();
        if (面板数据 != null)
        {
            // ★ 新档：**先把场景面板清干净**，再按存档恢复。
            //   为什么必须清：场景里的 CharacterUI 可能被预置过东西
            //   （编辑器调试菜单「用物品表填满当前场景背包」会写进场景并保存），
            //   那份数据是**场景资产**的一部分 —— 存档里背包是空的也盖不掉它。
            //   判定：存档里的背包/法宝/灵阵/坐骑/真灵全空 = 这是一份新档。
            bool 是新档 = (数据.背包物品 == null || 数据.背包物品.Count == 0)
                       && (数据.法宝 == null || 数据.法宝.Count == 0)
                       && (数据.灵阵 == null || 数据.灵阵.Count == 0)
                       && (数据.坐骑 == null || 数据.坐骑.Count == 0)
                       && (数据.已获得真灵 == null || 数据.已获得真灵.Count == 0);
            if (是新档)
            {
                bool 场景里本来有东西 = !面板数据.玩法数据为空();
                面板数据.清空玩法数据();
                对话标记.清空();                   // 静态残留：新档不该继承上一局的标记
                Debug.Log("[存档] 判定为新档：已清空场景面板"
                    + (场景里本来有东西 ? "（**场景里原本预置了东西**，已一并清掉）" : "")
                    + " 并清掉对话标记");
            }

            // 背包 / 法宝 / 灵阵 / 坐骑 / 技能槽 / 停用被动 / 已获真灵 / 经验（版本 5）
            恢复面板(数据, 面板数据);

            // ★ 任务进度不能在这里恢复：`应用到角色` 是游戏场景加载**之前**跑的，
            //   那时 任务管理器 还不存在。挂起来，等它的 Start() 来取。
            待恢复任务进度 = 数据.任务进度;
            待恢复对话标记 = 数据.对话标记;
            if (!string.IsNullOrEmpty(待恢复任务进度) || !string.IsNullOrEmpty(待恢复对话标记))
                Debug.Log("[存档] 主线进度已挂起，等 任务管理器 就绪后恢复：[" + 待恢复任务进度 + "]");
        }

        // ---- 镇妖塔层数（版本 6）----
        // 放在这里而不是 任务管理器.Start()：层数是**纯静态**的运行时状态，
        // 和场景无关（不像任务进度要等 任务管理器 实例化）。越早恢复越好 ——
        // TowerController.Start() 会直接读 TowerProgress.当前层 决定从第几层开刷，
        // 晚一步就会先按旧层刷一波再跳层（表现为"进塔先闪一下第 1 层的怪"）。
        TowerProgress.从存档读(数据);

        // ---- 战阵 ----
        // 「已获得的真灵」= `Assets/Data/Generated/NpcDefinition` 里所有 demon / human
        //（由 DataTableImporter 自动收集），所以按 id 在「已获得列表 + 场上 NPC」里就能找到。
        // 兽宠 / 驯服那套玩法已经取消，不需要再存"已获得"进度。
        if (面板数据 != null && 数据.战阵站位 != null && 数据.战阵站位.Count > 0)
        {
            面板数据.EnsureLists();
            int 还原数 = 0;
            for (int i = 0; i < 面板数据.战阵站位.Count; i++)
            {
                var id = i < 数据.战阵站位.Count ? 数据.战阵站位[i] : null;
                var 真灵 = 找真灵定义(id, 面板数据);
                面板数据.战阵站位[i] = 真灵;
                if (真灵 != null) 还原数++;
            }
            // 广播出去 → 挂在玩家身上的 SpiritFormationManager 会差量重建场上的真灵
            面板数据.RaiseChanged();
            Debug.Log("[存档] 已恢复战阵：上阵 " + 还原数 + " 个", 玩家);
        }
    }

    /// <summary>按 id 找战阵真灵的定义：先看「已获得真灵」，再在场上 NPC 里找</summary>
    static NpcDefinition 找真灵定义(string id, UIPanelData 面板)
    {
        if (string.IsNullOrEmpty(id)) return null;

        if (面板 != null && 面板.已获得真灵 != null)
            foreach (var s in 面板.已获得真灵)
                if (s != null && s.id == id) return s;

        foreach (var n in UnityEngine.Object.FindObjectsOfType<NpcInstance>())
            if (n != null && n.定义 != null && n.定义.id == id) return n.定义;

        Debug.LogWarning("[存档] 找不到战阵真灵「" + id + "」的定义，这一格留空"
                         + "（它可能不在「已获得真灵」列表里）");
        return null;
    }

    /// <summary>从角色身上抓一份当前状态写进存档数据（存档时调用）</summary>
    public static void 从角色采集(SaveData 数据)
    {
        if (数据 == null) return;

        // ★ **记下当前场景**（2026-10-01 修）。
        //   以前这个字段只在建新档时写过一次 "village"，之后永远是它 ——
        //   于是不管在太虚宗 / 宗门野外 / 镇妖塔存的档，读档**一律回古古镇**，
        //   而 `位置` 是那个场景的坐标 ⇒ 人被扔到古古镇里一个毫不相干的地点。
        var 场 = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!string.IsNullOrEmpty(场.name))
        {
            数据.场景名 = 场.name;
            Debug.Log("[存档] 记录所在场景：" + 数据.场景名);
        }

        var 玩家 = GameObject.Find("Player");
        if (玩家 == null) return;

        数据.位置 = 玩家.transform.position;
        数据.位置有效 = true;                     // ★ 显式标志，不再拿 Vector3.zero 当哨兵
        数据.朝向Y = 玩家.transform.eulerAngles.y;

        // 外观（static，读档会丢，必须存）
        try { 数据.外观id = 玩家外观.当前外观id; }
        catch (System.Exception e) { Debug.LogWarning("[存档] 采集外观失败：" + e.Message); }

        var 生命 = 玩家.GetComponent<PlayerVitals>();
        if (生命 != null)
        {
            数据.当前气血 = 生命.CurrentHealth;
            数据.当前灵气 = 生命.CurrentSpirit;
        }

        // ---- 修炼系统（总灵气 / 修炼次数 / 境界等级 / 功法 / 已学功法）----
        var 修炼 = 玩家.GetComponent<PlayerCultivation>();
        if (修炼 != null)
        {
            数据.总灵气 = 修炼.总灵气;
            数据.修炼次数累积 = 修炼.修炼次数累积;
            数据.境界等级 = 修炼.等级;
            数据.已解锁最高等级 = 修炼.已解锁最高等级;
            数据.破境加成 = 修炼.破境加成;
            数据.境界 = 修炼.境界名;                 // 顺手把那个旧字符串字段也更新掉

            var 面板 = 修炼.面板数据;
            if (面板 != null)
            {
                if (面板.当前功法 != null) 数据.功法id = 面板.当前功法.功法id;
                数据.已学功法.Clear();
                if (面板.已学功法 != null)
                    foreach (var g in 面板.已学功法)
                        if (g != null) 数据.已学功法.Add(g.功法id);

                // ★ 已获得的能力（主动 / 被动神通）
                数据.已获得主动神通.Clear();
                if (面板.已获得主动神通 != null)
                    foreach (var a in 面板.已获得主动神通)
                        if (a != null) 数据.已获得主动神通.Add(a.神通id);
                数据.已获得被动神通.Clear();
                if (面板.已获得被动神通 != null)
                    foreach (var a in 面板.已获得被动神通)
                        if (a != null) 数据.已获得被动神通.Add(a.神通id);
            }
        }

        // ---- 战阵站位（9 格，空位记空字符串）----
        数据.战阵站位.Clear();
        var 战阵面板 = UnityEngine.Object.FindObjectOfType<UIPanelData>();
        if (战阵面板 != null)
        {
            战阵面板.EnsureLists();
            for (int i = 0; i < SpiritFormationLayout.格子数; i++)
            {
                var d = i < 战阵面板.战阵站位.Count ? 战阵面板.战阵站位[i] : null;
                数据.战阵站位.Add(d != null ? d.id : "");
            }
        }

        // ---- 背包 / 装备（版本 5）----
        if (战阵面板 != null) 采集面板(数据, 战阵面板);

        // ---- 主线进度 + 对话标记（版本 5）----        // ⚠ 任务进度必须在这里抓：任务管理器会随场景重建，它的静态状态也只在运行时有意义
        var 任务 = UnityEngine.Object.FindObjectOfType<任务管理器>();
        if (任务 != null)
        {
            数据.任务进度 = 任务.导出进度();
        }
        else if (string.IsNullOrEmpty(数据.任务进度))
        {
            数据.任务进度 = "";     // 场景里没有任务管理器（例如在主菜单存档）——保留旧值
        }
        var 标记 = 对话标记.全部标记();
        数据.对话标记 = 标记 != null && 标记.Length > 0 ? string.Join(";", 标记) : "";

        // ---- 镇妖塔层数（版本 6）----
        // 静态进度 → 存档对象。塔里和塔外都采得到（TowerProgress 是 static）。
        TowerProgress.写进存档(数据);

        // ---- 纪年 / 修炼机会 / 灵田（版本 8）----
        // ⚠️ 写 `UnityEngine.Object` 而不是 `Object`：本文件有 `using System;`，
        //    裸写 `Object` 会在 System.Object 和 UnityEngine.Object 之间歧义（CS0104）。
        var 时间 = UnityEngine.Object.FindObjectOfType<时间管理器>();
        if (时间 != null)
        {
            int 天; float 进度, 打怪; int 今日;
            string 机会;
            时间.导出(out 天, out 进度, out 机会, out 打怪, out 今日);
            数据.天数 = 天;
            数据.日内进度 = 进度;
            数据.日常机会 = 机会;
            数据.打怪机会 = 打怪;
        }

        var 田 = UnityEngine.Object.FindObjectOfType<灵田>();
        if (田 != null)
        {
            // 地块状态本身就是可序列化的（位置是 Vector3，JsonUtility 认），所以直接交出去
            数据.灵田地块 = 田.导出();
        }

        // 摆设（练功木桩这类玩家摆下来的物件）—— 也要能存档
        var 摆 = UnityEngine.Object.FindObjectOfType<摆设>();
        if (摆 != null) 数据.摆设 = 摆.导出();

        // 宗门贡献（兑换用货币）—— 静态值，直接抄进存档
        数据.宗门贡献 = 宗门贡献.当前;

        // 日常清单 + 两大伏笔（版本 14）—— 同样从静态实例抄
        var 日常 = 日常循环.取();
        if (日常 != null)
        {
            int 天, 掩码;
            日常.导出(out 天, out 掩码);
            数据.日常_天 = 天;
            数据.日常_掩码 = 掩码;
        }
        var 伏笔 = 伏笔管理器.取();
        if (伏笔 != null)
        {
            int 田级, 塔级;
            伏笔.导出(out 田级, out 塔级);
            数据.伏笔_灵田 = 田级;
            数据.伏笔_塔 = 塔级;
        }

        Debug.Log("[存档] 已采集：背包 " + 数据.背包物品.Count + " 件、任务进度 ["
            + 数据.任务进度 + "]、对话标记 " + 标记.Length + " 个"
            + "、镇妖塔第 " + 数据.镇妖塔当前层 + " 层");
    }

    /// <summary>采集面板上的玩法数据（背包 / 法宝 / 灵阵 / 坐骑 / 技能槽 / 停用被动 / 已获真灵 / 经验）</summary>
    static void 采集面板(SaveData 数据, UIPanelData 面板)
    {
        面板.EnsureLists();

        数据.背包物品.Clear();
        if (面板.物品 != null)
            foreach (var it in 面板.物品) if (it != null) 数据.背包物品.Add(it.物品id);

        数据.法宝.Clear();
        if (面板.法宝 != null)
            foreach (var t in 面板.法宝) if (t != null) 数据.法宝.Add(t.法宝id);

        数据.灵阵.Clear();
        if (面板.灵阵 != null)
            foreach (var a in 面板.灵阵) if (a != null) 数据.灵阵.Add(a.灵阵id);

        数据.坐骑.Clear();
        if (面板.坐骑 != null)
            foreach (var m in 面板.坐骑) if (m != null) 数据.坐骑.Add(m.坐骑id);
        数据.当前坐骑 = 面板.当前坐骑 != null ? 面板.当前坐骑.坐骑id : "";

        数据.主动技能槽.Clear();
        if (面板.主动技能 != null)
            for (int i = 0; i < 面板.主动技能.Count; i++)
                数据.主动技能槽.Add(取内容id(面板.主动技能[i]));

        数据.已停用被动.Clear();
        if (面板.已停用被动 != null)
            foreach (var p in 面板.已停用被动) if (p != null) 数据.已停用被动.Add(p.神通id);

        数据.已获得真灵.Clear();
        if (面板.已获得真灵 != null)
            foreach (var s in 面板.已获得真灵) if (s != null) 数据.已获得真灵.Add(s.id);

        数据.当前经验 = 面板.当前经验;
        数据.突破所需总经验 = 面板.突破所需总经验;
    }

    /// <summary>主动技能槽里放的是 Object（神通/法宝/灵阵共用），按类型取 id</summary>
    static string 取内容id(UnityEngine.Object o)
    {
        if (o == null) return "";
        var a = o as ActiveDivineAbility; if (a != null) return a.神通id;
        var t = o as TreasureDefinition; if (t != null) return t.法宝id;
        var s = o as SpiritArrayDefinition; if (s != null) return s.灵阵id;
        return o.name;      // 兜底：拿资源名，读档时按名字再找一次
    }

    /// <summary>把存档里的面板数据（背包/装备/经验）套回 UIPanelData</summary>
    static void 恢复面板(SaveData 数据, UIPanelData 面板)
    {
        面板.EnsureLists();

        // ---- 背包：一件物品有几个就是几条（和 采集面板 对称）----
        面板.物品 = new System.Collections.Generic.List<ItemDefinition>();
        var 物品库 = 取物品库();
        if (数据.背包物品 != null && 物品库 != null)
        {
            var 表 = new System.Collections.Generic.Dictionary<string, ItemDefinition>();
            foreach (var it in 物品库) if (it != null && !string.IsNullOrEmpty(it.物品id)) 表[it.物品id] = it;
            foreach (var id in 数据.背包物品)
            {
                ItemDefinition it;
                if (id != null && 表.TryGetValue(id, out it)) 面板.物品.Add(it);
                else if (!string.IsNullOrEmpty(id)) Debug.LogWarning("[存档] 背包物品找不到定义：" + id);
            }
        }

        // ---- 法宝 / 灵阵 / 坐骑：按 id 在「当前场景面板自带的表」里找 ----
        面板.法宝 = 按id还原(数据.法宝, 面板.法宝,
            (t) => t != null ? t.法宝id : null);
        面板.灵阵 = 按id还原(数据.灵阵, 面板.灵阵,
            (t) => t != null ? t.灵阵id : null);
        面板.坐骑 = 按id还原(数据.坐骑, 面板.坐骑,
            (t) => t != null ? t.坐骑id : null);

        // 当前坐骑（走 设置当前坐骑，互斥规则写在那一处）
        面板.当前坐骑 = null;
        if (!string.IsNullOrEmpty(数据.当前坐骑) && 面板.坐骑 != null)
            foreach (var m in 面板.坐骑)
                if (m != null && m.坐骑id == 数据.当前坐骑) { 面板.当前坐骑 = m; break; }

        // ---- 已停用被动 ----
        面板.已停用被动 = new System.Collections.Generic.List<PassiveDivineAbility>();
        if (数据.已停用被动 != null)
            foreach (var id in 数据.已停用被动)
                foreach (var a in 面板.神通)
                    if (a is PassiveDivineAbility ps && ps.神通id == id && !面板.已停用被动.Contains(ps))
                        面板.已停用被动.Add(ps);

        // ---- 已获得真灵 ----
        面板.已获得真灵 = new System.Collections.Generic.List<NpcDefinition>();
        if (数据.已获得真灵 != null)
            foreach (var id in 数据.已获得真灵)
            {
                var d = 找真灵定义(id, 面板);
                if (d != null && !面板.已获得真灵.Contains(d)) 面板.已获得真灵.Add(d);
            }

        // ---- 主动技能槽（空槽写空字符串）----
        if (数据.主动技能槽 != null && 数据.主动技能槽.Count > 0)
        {
            while (面板.主动技能.Count < 数据.主动技能槽.Count) 面板.主动技能.Add(null);
            for (int i = 0; i < 数据.主动技能槽.Count && i < 面板.主动技能.Count; i++)
                面板.主动技能[i] = 找内容(数据.主动技能槽[i], 面板);
        }

        // ---- 境界经验 ----
        面板.当前经验 = 数据.当前经验;
        面板.突破所需总经验 = 数据.突破所需总经验;

        面板.RaiseChanged();
        Debug.Log("[存档] 已恢复背包 " + 面板.物品.Count + " 件、法宝 " + 面板.法宝.Count
            + "、坐骑 " + 面板.坐骑.Count + "、技能槽 " + 面板.主动技能.Count + " 格");
    }

    /// <summary>把一串 id 还原成当前面板里的定义列表（按 id 匹配，忽略找不到的）</summary>
    static System.Collections.Generic.List<T> 按id还原<T>(
        System.Collections.Generic.List<string> ids,
        System.Collections.Generic.List<T> 来源,
        System.Func<T, string> 取id) where T : UnityEngine.Object
    {
        var 出 = new System.Collections.Generic.List<T>();
        if (ids == null || ids.Count == 0 || 来源 == null) return 出;
        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id)) continue;
            foreach (var o in 来源)
                if (o != null && 取id(o) == id && !出.Contains(o)) { 出.Add(o); break; }
        }
        return 出;
    }

    /// <summary>找主动技能槽里的内容：先按神通/法宝/灵阵的 id，再按资源名兜底</summary>
    static UnityEngine.Object 找内容(string id, UIPanelData 面板)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var a in 面板.神通)
            if (a is ActiveDivineAbility act && act.神通id == id) return act;
        foreach (var t in 面板.法宝) if (t != null && t.法宝id == id) return t;
        foreach (var s in 面板.灵阵) if (s != null && s.灵阵id == id) return s;
        foreach (var a in 面板.神通) if (a != null && a.name == id) return a;
        return null;
    }

    /// <summary>物品库（背包按 id 还原用）。任务库顺带收了物品，没有再退到 Resources</summary>
    static System.Collections.Generic.List<ItemDefinition> 取物品库()
    {
        var 库 = QuestDatabase.取();
        if (库 != null && 库.物品库 != null && 库.物品库.Count > 0) return 库.物品库;
        return null;
    }

    // ---------------------------------------------------------------- 任务进度的延迟恢复

    /// <summary>
    /// 挂起的任务进度 / 对话标记。`应用到角色` 跑在游戏场景加载之前，
    /// 那时 任务管理器 还不存在，所以先存这里，由 任务管理器.Start() 来取
    /// （见 <see cref="取挂起的进度"/>）。
    /// </summary>
    public static string 待恢复任务进度 = "";
    public static string 待恢复对话标记 = "";

    /// <summary>任务管理器就绪后调它，取走挂起的进度（取完即清，只恢复一次）</summary>
    public static void 取挂起的进度(out string 任务进度, out string 对话标记)
    {
        任务进度 = 待恢复任务进度;
        对话标记 = 待恢复对话标记;
        待恢复任务进度 = "";
        待恢复对话标记 = "";
    }

    /// <summary>当前正在玩的这份档（菜单里选完带进游戏场景）</summary>
    public static SaveData 当前存档;
    public static int 当前槽位 = -1;
}

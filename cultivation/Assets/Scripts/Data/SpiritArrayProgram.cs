using System;
using System.Collections.Generic;
using UnityEngine;

public enum SpiritArrayOp
{
    身前布阵 = 0, 目标头顶 = 1, 顺序打包 = 2, 多重施法 = 3,
    反向多重 = 4, // 仅保留旧存档的数值，载入时迁移；不再提供此节点。
    生成灵弹 = 5, 追踪 = 6, 分裂 = 7, 强化 = 8, 加速 = 9, 修正方向 = 10,
    逆向打包 = 11
}

/// <summary>一个编排节点。子序列只用于打包或多重；前摇由规则定义，不写入玩家参数。</summary>
[Serializable]
public sealed class SpiritArrayNode
{
    public SpiritArrayOp 操作 = SpiritArrayOp.生成灵弹;
    public float 参数 = 1.5f;
    public float 延迟 = .9f;
    public float 外倾角 = 18;
    public bool 反向;
    public bool 自定义间距;
    public float 节点间距 = .48f;
    public bool 自定义起点距离;
    public float 起点距离 = 1.1f;
    [SerializeReference] public List<SpiritArrayNode> 子阵 = new List<SpiritArrayNode>();
    public static SpiritArrayNode Create(SpiritArrayOp op) => new SpiritArrayNode {
        操作 = op, 参数 = op == SpiritArrayOp.分裂 ? 3 : op == SpiritArrayOp.追踪 ? 180 : 1.5f
    };
}

[Serializable]
public sealed class SpiritArrayProgram
{
    public string id;
    public string 名称 = "灵阵编排";
    public List<SpiritArrayNode> 序列 = new List<SpiritArrayNode>();
    public SpiritArrayProgram Copy() => JsonUtility.FromJson<SpiritArrayProgram>(JsonUtility.ToJson(this));
}

/// <summary>编排约束、消耗与默认方案共用一份规则，UI 不另算一套。</summary>
public static class SpiritArrayRules
{
    static SpiritArrayDefinition[] templates;
    public static SpiritArrayDefinition[] Templates => templates ?? (templates = Resources.LoadAll<SpiritArrayDefinition>("SpiritArrays"));
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { templates = null; }
    public const float 前摇 = .3f, 阵距 = .48f, 阵半径 = 1.15f, 存活 = 9f;
    public const float 灵弹速度倍率 = 2.3f, 灵弹初速 = 12f * 灵弹速度倍率, 功能完成消散 = 1f;
    public const int 最大节点 = 64, 最大深度 = 6, 最大分支 = 8;
    static readonly string[] names = { "身前布阵", "目标头顶布阵", "顺序打包", "多重施法", "旧多重（已迁移）", "生成灵弹", "追踪阵", "分裂阵", "强化阵", "加速阵", "方向修正阵", "逆向打包" };
    public static readonly SpiritArrayOp[] 可用节点 = {SpiritArrayOp.身前布阵,SpiritArrayOp.目标头顶,SpiritArrayOp.顺序打包,SpiritArrayOp.逆向打包,SpiritArrayOp.多重施法,SpiritArrayOp.生成灵弹,SpiritArrayOp.追踪,SpiritArrayOp.分裂,SpiritArrayOp.强化,SpiritArrayOp.加速,SpiritArrayOp.修正方向};
    public static string Name(SpiritArrayOp op) => (int)op >= 0 && (int)op < names.Length ? names[(int)op] : "未知节点";
    public static bool Pack(SpiritArrayOp op) => op == SpiritArrayOp.顺序打包 || op == SpiritArrayOp.逆向打包;
    public static bool Container(SpiritArrayOp op) => Pack(op) || op == SpiritArrayOp.多重施法 || op == SpiritArrayOp.反向多重;
    public static bool Gate(SpiritArrayOp op) => op >= SpiritArrayOp.追踪 && op <= SpiritArrayOp.修正方向;
    public static float Size(SpiritArrayOp op) => op==SpiritArrayOp.多重施法?1.5f:op==SpiritArrayOp.强化?1.12f:op==SpiritArrayOp.生成灵弹?.78f:op==SpiritArrayOp.加速?.86f:op==SpiritArrayOp.分裂?1.04f:1;
    public static float Radius(SpiritArrayOp op) => 阵半径 * Size(op);
    public static float Step(SpiritArrayOp op) => Pack(op)||op==SpiritArrayOp.身前布阵||op==SpiritArrayOp.目标头顶?0:阵距 * Size(op);
    public static float Step(SpiritArrayNode node)=>node.自定义间距?node.节点间距:Step(node.操作);
    public static float OriginDistance(SpiritArrayNode node)=>node.自定义起点距离?node.起点距离:node.操作==SpiritArrayOp.目标头顶?3.6f:1.1f;
    public static void Migrate(SpiritArrayProgram program)
    {
        void Walk(List<SpiritArrayNode> nodes){if(nodes==null)return;foreach(var n in nodes){if(n==null)continue;if(n.操作==SpiritArrayOp.反向多重)n.操作=SpiritArrayOp.多重施法;Walk(n.子阵);}}
        if(program!=null)Walk(program.序列);
    }
    public static float Cost(SpiritArrayOp op) => op == SpiritArrayOp.生成灵弹 ? 8 : Gate(op) ? (op == SpiritArrayOp.分裂 ? 6 : 3) : 0;
    public static string Describe(SpiritArrayOp op)
    {
        switch (op) {
            case SpiritArrayOp.身前布阵: return "沿锁定/引导方向按“距角色距离”定位编排起点，下一阵再沿朝向偏移节点间距。角色施法中心距地 1.1 米。";
            case SpiritArrayOp.目标头顶: return "在锁定目标上方按“距目标距离”定位起点，下一阵再向下偏移节点间距；无锁定不开始。";
            case SpiritArrayOp.顺序打包: return "依次执行子阵，先支付首个实际施法节点，后续节点运行时再支付。";
            case SpiritArrayOp.逆向打包: return "仍按 a→b→c 执行，节点沿阵列后方展开；灵弹发射与法阵朝向仍朝目标，不反转。";
            case SpiritArrayOp.多重施法: return "同时展开全部子阵，再执行下一节点。外倾角控制分支方向；首轮灵力统一检查。";
            case SpiritArrayOp.反向多重: return "先执行下一节点，再同时展开子阵。外倾角控制分支方向。";
            case SpiritArrayOp.生成灵弹: return "展开后按设定延迟，从阵心发射灵弹；可反向发射。每次生成消耗 8 灵力。";
            case SpiritArrayOp.追踪: return "穿阵后追踪当前锁定敌人。参数为每秒最大转向角；每个生成物消耗 3 灵力。";
            case SpiritArrayOp.分裂: return "穿阵后分成指定数量（2～5）的灵弹。外倾角控制散射；每个生成物消耗 6 灵力。";
            case SpiritArrayOp.强化: return "穿阵后按倍率强化伤害（总倍率上限 8）；每个生成物消耗 3 灵力。";
            case SpiritArrayOp.加速: return "穿阵后按倍率改变速度（总速度上限 138 米/秒）；每个生成物消耗 3 灵力。";
            default: return "把穿阵灵弹方向修正为该阵的朝向；每个生成物消耗 3 灵力。";
        }
    }
    public static bool Validate(SpiritArrayProgram program, out string error)
    {
        int count = 0; error = null;
        if (program == null || program.序列 == null || program.序列.Count == 0) { error = "请先添加一个灵阵节点"; return false; }
        if (!Check(program.序列, 0, ref count, out error)) return false;
        return true;
    }
    static bool Check(List<SpiritArrayNode> nodes, int depth, ref int count, out string error)
    {
        error = null;
        if (depth > 最大深度) { error = "嵌套最多 6 层"; return false; }
        for (int i = 0; i < nodes.Count; i++) {
            var node = nodes[i];
            if (++count > 最大节点) { error = "每套编排最多 64 个节点"; return false; }
            if (node == null || !Enum.IsDefined(typeof(SpiritArrayOp), node.操作)) { error = "存在未知灵阵节点"; return false; }
            if (float.IsNaN(node.参数) || float.IsInfinity(node.参数) || node.参数 < .1f || node.参数 > 360 || float.IsNaN(node.延迟) || float.IsInfinity(node.延迟) || node.延迟 < 0 || node.延迟 > 5 || float.IsNaN(node.外倾角) || float.IsInfinity(node.外倾角) || Mathf.Abs(node.外倾角) > 75) { error = "参数超出范围：倍率 0.1～360、延迟 0～5 秒、外倾角 ±75°"; return false; }
            if(float.IsNaN(node.节点间距)||float.IsInfinity(node.节点间距)||node.节点间距<0||node.节点间距>20||float.IsNaN(node.起点距离)||float.IsInfinity(node.起点距离)||node.起点距离<0||node.起点距离>50){error="节点间距限 0～20 米，起点距离限 0～50 米";return false;}
            bool multiple = node.操作 == SpiritArrayOp.多重施法 || node.操作 == SpiritArrayOp.反向多重;
            if (multiple && (node.子阵 == null || node.子阵.Count < 1 || node.子阵.Count > 最大分支)) { error = "多重施法需要 1～8 个子阵"; return false; }
            if (multiple) foreach (var branch in node.子阵)
                if (branch == null || (!Gate(branch.操作) && branch.操作!=SpiritArrayOp.生成灵弹 && !Pack(branch.操作))) { error = "多重分支请使用单阵、顺序打包或逆向打包；嵌套多重请放入打包"; return false; }
            if ((node.操作 == SpiritArrayOp.身前布阵 || node.操作 == SpiritArrayOp.目标头顶) && i == nodes.Count - 1) { error = Name(node.操作) + "后面还需要一个节点"; return false; }
            if (Container(node.操作)) {
                if (node.子阵 == null || node.子阵.Count == 0) { error = "打包或多重的子阵不能为空"; return false; }
                if (!Check(node.子阵, depth + 1, ref count, out error)) return false;
            } else if (node.子阵 != null && node.子阵.Count > 0) { error = "普通节点不能包含子阵"; return false; }
        }
        return true;
    }
    public static SpiritArrayProgram Get(UIPanelData data, SpiritArrayDefinition definition)
    {
        if (data.灵阵编排 == null) data.灵阵编排 = new List<SpiritArrayProgram>();
        var found = data.灵阵编排.Find(p => p != null && p.id == definition.灵阵id);
        var result=(found ?? definition.默认编排)?.Copy();Migrate(result);return result;
    }
    public static void Store(UIPanelData data, SpiritArrayProgram program)
    {
        data.灵阵编排.RemoveAll(p => p == null || p.id == program.id);
        data.灵阵编排.Add(program.Copy()); data.RaiseChanged();
    }
}

using UnityEngine;

/// <summary>外观网格、材质与随动配置；沿用玩家人体 Animator。模型按资源路径或直接引用加载。
/// 默认外观直接拥有，境界着装先赠送对应学习物品，使用后才拥有并装备。
/// </summary>
[CreateAssetMenu(fileName = "外观_", menuName = "修仙/外观", order = 30)]
public class AppearanceDefinition : ScriptableObject
{
    [Header("身份")]
    public string id = "";

    [Tooltip("列表里显示的名字")]
    public string 外观名 = "";

    [Header("模型")]
    [Tooltip("模型所在资源路径（`Assets/resources/` 下、不带扩展名）。\n" +
             "例：`NPC/Human/成男村民/成男村民`、`外观/修仙者`。\n" +
             "⚠️ 只要那底下同目录还有同名 FBX，就必须用 Resources.LoadAll 挑 prefab（项目里踩过这个坑）")]
    public string 模型资源路径 = "";

    [Tooltip("也可以直接填预制体（填了就优先用它，忽略上面的路径）")]
    public GameObject 模型预制体;

    [Tooltip("衣摆和宽袖的附加骨骼蒙皮；不替换人体 Animator。")]
    public AppearanceClothProfile 衣服随动;

    [Header("UI")]
    public Sprite 图标;

    [TextArea(2, 4)]
    public string 介绍 = "";

    [Header("拥有 / 解锁")]
    [Tooltip("勾上 = 开局就拥有并装备（村中少年就是靠它做默认外观）")]
    public bool 默认拥有 = false;

    [Tooltip("达到该境界等级赠送一次对应的学外观物品；使用物品后才解锁。0 表示不按境界赠物。")]
    [Min(0)] public int 境界解锁等级;

    [Tooltip("兼容其他外观的条件标记（分号分隔）；当前两套境界着装不通过此字段解锁。")]
    public string 解锁标记 = "";

    [Tooltip("默认或标记解锁时是否换上；学习物品的换装由学外观效果控制")]
    public bool 解锁即装备 = true;

    /// <summary>现在解锁了吗（标记齐了 / 或者本来就是默认拥有）</summary>
    public bool 已解锁
    {
        get
        {
            if (默认拥有) return true;
            var 需要 = DialogueDefinition.拆标记(解锁标记);
            if (需要.Length == 0) return false;
            for (int i = 0; i < 需要.Length; i++)
                if (!对话标记.具备(需要[i])) return false;
            return true;
        }
    }

    /// <summary>
    /// 取模型预制体：优先用直接填的那个，否则按 <see cref="模型资源路径"/> 在 Resources 里找。
    ///
    /// **必须用 LoadAll 再挑 prefab**：项目里 `Assets/resources/NPC/...` 下同时放着同名 FBX，
    /// 直接 `Resources.Load&lt;GameObject&gt;(路径)` 会拿到 FBX 而不是 prefab（NpcPrefabs 的注释里有完整教训）。
    /// </summary>
    public GameObject 取模型()
    {
        if (模型预制体 != null) return 模型预制体;
        if (string.IsNullOrEmpty(模型资源路径)) return null;

        var 全部 = Resources.LoadAll<GameObject>(模型资源路径);
        for (int i = 0; i < 全部.Length; i++)
        {
            var go = 全部[i];
            if (go == null) continue;
            if (go.GetComponent<Animator>() != null || go.GetComponentInChildren<Animator>(true) != null) return go;
        }
        for (int i = 0; i < 全部.Length; i++) if (全部[i] != null) return 全部[i];

        Debug.LogWarning("[外观] 资源路径里找不到模型：" + 模型资源路径, this);
        return null;
    }

    /// <summary>列表里显示的名字</summary>
    public string DisplayName => string.IsNullOrEmpty(外观名) ? name : 外观名;

    void OnValidate()
    {
        if (string.IsNullOrEmpty(id)) id = name;
    }
}

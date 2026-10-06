using UnityEngine;

/// <summary>法宝定义：持有/装备、真 3D 展示，以及镇妖葫收服参数。</summary>
[CreateAssetMenu(fileName = "Treasure_", menuName = "修仙/法宝", order = 8)]
public class TreasureDefinition : ScriptableObject, IPanelEntry
{
    [Header("法宝")]
    [Tooltip("法宝名称")]
    public string 法宝名称 = "新法宝";

    [Tooltip("法宝id，全局唯一")]
    public string 法宝id = "";

    [Tooltip("法宝品阶")]
    public QualityTier 品阶 = QualityTier.凡品;

    [Header("UI 用")]
    public Sprite 图标;

    [TextArea(2, 8)]
    public string 介绍 = "";

    [Header("收服法宝")]
    public GameObject 模型;
    public string 模型资源路径 = "法宝/镇妖葫/镇妖葫模型";
    public Vector3 瓶口局部位置 = new Vector3(0, .96f, 0);
    public float 放大比例 = 1.65f;
    public float 收服距离 = 25f;
    public float 最长施放时间 = 30f;
    public float 冷却时间 = 6f;
    public float 伤害倍率 = .05f;
    public GameObject 加载模型() => 模型 != null ? 模型 : Resources.Load<GameObject>(模型资源路径);

    // ---- IPanelEntry ----
    public string DisplayName => string.IsNullOrEmpty(法宝名称) ? name : 法宝名称;
    public string DisplayDescription => 介绍;
    public QualityTier DisplayTier => 品阶;
    public Sprite DisplayIcon => 图标;

    void OnValidate()
    {
        if (string.IsNullOrEmpty(法宝id)) 法宝id = name;
    }
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>拒绝拖放时仅让拖影回到起点；不回滚或修改装备数据。</summary>
public class UIInkDragReturn : MonoBehaviour
{
    public Vector2 起点;
    Vector3 from;
    float elapsed;
    CanvasGroup group;
    bool ownsBudget;
    void Start()
    {
        ownsBudget = UIInkMotion.Acquire();
        if (!ownsBudget) { Destroy(gameObject); return; }
        from = transform.position; group = gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false;
        var font = GetComponentInChildren<Text>()?.font;
        var cross = UIBuildUtils.CreateText("InkRejected", transform, font, "×", 30, TextAnchor.MiddleCenter, new Color(.62f, .31f, .24f));
        UIBuildUtils.Stretch(cross.rectTransform); cross.raycastTarget = false;
    }
    void Update()
    {
        if (!ownsBudget) return;
        elapsed += Time.unscaledDeltaTime;
        float t = UIInkMotion.Timing.Cubic(elapsed / .18f);
        transform.position = Vector3.Lerp(from, 起点, t); group.alpha = 1 - t;
        if (elapsed >= .18f || UIInkMotion.减少动效) Destroy(gameObject);
    }
    void OnDestroy() { if (ownsBudget) { UIInkMotion.Release(); ownsBudget = false; } }
}

using UnityEngine;

/// <summary>无业务、无命中的短暂离场绘制层。关闭逻辑和时间缩放不等待它。</summary>
public class UIInkCloseVisual : MonoBehaviour
{
    float elapsed;
    bool ownsBudget;
    float startWidth = 1;
    public void Begin()
    {
        ownsBudget = true;
        var paper = GetComponentInChildren<UIInkReveal>();
        if (paper == null) return;
        startWidth = paper.进度;
        foreach (var graphic in GetComponentsInChildren<UnityEngine.UI.Graphic>())
        {
            if (graphic.name == "Dim" || graphic.name.Contains("幕布") || graphic.name == "暗色底") continue;
            var clip = graphic.GetComponent<UIInkClipRect>();
            if (clip == null) clip = graphic.gameObject.AddComponent<UIInkClipRect>();
            clip.面板 = paper;
        }
    }
    void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        var group = GetComponent<CanvasGroup>();
        group.alpha = 1 - UIInkMotion.Timing.Quad(elapsed / UIInkMotion.Timing.Close);
        if (elapsed >= .06f)
            foreach (var reveal in GetComponentsInChildren<UIInkReveal>()) reveal.进度 = startWidth * (1 - UIInkMotion.Timing.InOut(Mathf.Clamp01((elapsed - .06f) / .12f)));
        if (UIInkMotion.减少动效 || elapsed >= UIInkMotion.Timing.Close) Destroy(gameObject);
    }
    void OnDestroy() { if (ownsBudget) { UIInkMotion.Release(); ownsBudget = false; } }
}

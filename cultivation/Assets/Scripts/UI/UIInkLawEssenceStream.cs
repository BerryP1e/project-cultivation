using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>精粹从炉口汇入计数图标：单个UI网格批量绘制，数量随产出增长，暂停时照常播放。</summary>
public sealed class UIInkLawEssenceStream : MaskableGraphic
{
    sealed class Mote { public Vector2 from, bend; public float start, duration, size; }
    readonly List<Mote> motes = new List<Mote>();
    readonly UIVertex[] quad = new UIVertex[4];
    Sprite sprite; RectTransform destination; Action arrived;
    public int 上次粒子数 { get; private set; }
    public int 待汇入粒子数 => motes.Count;
    public override Texture mainTexture => sprite ? sprite.texture : Texture2D.whiteTexture;
    public static int 产出粒子数(long amount) => amount <= 0 ? 0 : 8 + Mathf.CeilToInt(7 * (float)Math.Log10(1d + amount));
    public void 初始化(Sprite source, RectTransform target, Action arrival)
    { sprite = source; destination = target; arrived = arrival; raycastTarget = false; SetMaterialDirty(); }
    public void 发射(Vector3 mouth, long amount, float heatDuration)
    {
        上次粒子数 = 产出粒子数(amount);
        var from = (Vector2)rectTransform.InverseTransformPoint(mouth);
        for (int i = 0; i < 上次粒子数; i++)
        {
            float phase = i * 2.399963f;
            motes.Add(new Mote { from = from + new Vector2(Mathf.Sin(phase) * 12, Mathf.Cos(phase) * 4),
                bend = new Vector2(Mathf.Sin(phase) * 65, 65 + Mathf.Repeat(i * 17.3f, 90)),
                start = Time.unscaledTime + heatDuration + i * .012f,
                duration = .85f + Mathf.Repeat(i * .173f, .45f), size = 4 + Mathf.Repeat(i * .73f, 3) });
        }
        SetVerticesDirty();
    }
    Vector2 Position(Mote mote, Vector2 target, float t)
    {
        // 先从炉口升起再向图标收束；不遮挡属性和按钮，也不创建逐粒子的游戏对象。
        float u = 1 - t;
        return u * u * mote.from + 2 * u * t * (Vector2.Lerp(mote.from, target, .4f) + mote.bend) + t * t * target;
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); if (!destination) return;
        var target = (Vector2)rectTransform.InverseTransformPoint(destination.position);
        var uv = sprite ? DataUtility.GetOuterUV(sprite) : new Vector4(0, 0, 1, 1);
        foreach (var mote in motes)
        {
            float t = (Time.unscaledTime - mote.start) / mote.duration; if (t < 0 || t >= 1) continue;
            float fade = Mathf.Clamp01(t * 10) * Mathf.Clamp01((1 - t) * 8);
            var p = Position(mote, target, t); var tail = Position(mote, target, Mathf.Max(0, t - .045f));
            AddSprite(vh, tail, mote.size * 1.4f, new Color(.64f, .83f, .71f, fade * .35f), uv);
            AddSprite(vh, p, mote.size * (1 + Mathf.Sin(t * Mathf.PI) * .4f), new Color(1, .95f, .72f, fade), uv);
        }
    }
    void AddSprite(VertexHelper vh, Vector2 p, float size, Color tint, Vector4 uv)
    {
        for (int i = 0; i < 4; i++)
        {
            bool right = i == 2 || i == 3, top = i == 1 || i == 2;
            quad[i] = UIVertex.simpleVert; quad[i].position = p + new Vector2(right ? size : -size, top ? size : -size);
            quad[i].color = tint; quad[i].uv0 = new Vector2(right ? uv.z : uv.x, top ? uv.w : uv.y);
        }
        vh.AddUIVertexQuad(quad);
    }
    void Update()
    {
        if (motes.Count == 0) return;
        bool landed = false;
        for (int i = motes.Count - 1; i >= 0; i--) if (Time.unscaledTime >= motes[i].start + motes[i].duration)
        { motes.RemoveAt(i); landed = true; }
        if (landed) arrived?.Invoke(); SetVerticesDirty();
    }
    protected override void OnDisable() { motes.Clear(); base.OnDisable(); }
}

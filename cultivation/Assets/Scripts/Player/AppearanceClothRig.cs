using System;
using UnityEngine;

/// <summary>只驱动附加衣骨，人体动作仍由原 Animator 控制；世界空间惯性、限角和身体胶囊约束。</summary>
[DefaultExecutionOrder(20000)]
public sealed class AppearanceClothRig : MonoBehaviour
{
    [SerializeField] AppearanceClothProfile 配置;
    [SerializeField] Transform[] 人体骨;
    [SerializeField] Transform[] 衣骨;
    Vector3[] 末端, 速度;
    Vector3 上帧锚点;
    float 上帧时间 = -1;
    bool 已初始化;
    public AppearanceClothProfile 当前配置 => 配置;
    public int 衣骨数 => 衣骨 == null ? 0 : 衣骨.Length;
    public int 头发骨数
    {
        get { int count=0;if(配置)foreach(var bone in 配置.衣骨)if(bone.头发)count++;return count; }
    }

    public static void 应用(SkinnedMeshRenderer renderer, AppearanceClothProfile profile, Transform[] bodyBones)
    {
        var rig = renderer.GetComponent<AppearanceClothRig>();
        if (rig) rig.清除();
        if (!profile) { renderer.bones = bodyBones; return; }
        if (profile.人体骨名.Length != bodyBones.Length)
            throw new InvalidOperationException("衣服骨架数量不匹配");
        for (int i = 0; i < bodyBones.Length; i++)
            if (!bodyBones[i] || bodyBones[i].name != profile.人体骨名[i])
                throw new InvalidOperationException("衣服骨序不匹配：" + i);
        if (!rig) rig = renderer.gameObject.AddComponent<AppearanceClothRig>();
        rig.配置 = profile;
        rig.人体骨 = (Transform[])bodyBones.Clone();
        rig.衣骨 = new Transform[profile.衣骨.Length];
        var combined = new Transform[bodyBones.Length + profile.衣骨.Length];
        Array.Copy(bodyBones, combined, bodyBones.Length);
        for (int i = 0; i < rig.衣骨.Length; i++)
        {
            var spec = profile.衣骨[i];
            var bone = new GameObject("OutfitCloth_" + spec.名称).transform;
            bone.gameObject.layer = renderer.gameObject.layer;
            bone.SetParent(spec.上级衣骨 < 0 ? bodyBones[spec.人体骨索引] : rig.衣骨[spec.上级衣骨], false);
            bone.localPosition = spec.局部位置;
            bone.localRotation = spec.局部旋转;
            rig.衣骨[i] = combined[bodyBones.Length + i] = bone;
        }
        renderer.sharedMesh = profile.蒙皮;
        renderer.bones = combined;
        rig.enabled = true;
        rig.重置随动();
    }

    /// <summary>面板克隆用自己的骨架，不能直接引用游戏角色的衣骨。</summary>
    public static void 同步预览(SkinnedMeshRenderer source, SkinnedMeshRenderer target)
    {
        var rig = source.GetComponent<AppearanceClothRig>();
        var names = rig && rig.配置 ? rig.配置.人体骨名 : null;
        int count = names == null ? source.bones.Length : names.Length;
        var old = target.bones;
        var body = new Transform[count];
        if (old.Length < count) throw new InvalidOperationException("预览人体骨缺失");
        Array.Copy(old, body, count);
        // 无行为的克隆可能还保留旧衣骨；先清理，只清理约定前缀的附加骨根。
        if (!target.GetComponent<AppearanceClothRig>())
            for (int i = count; i < old.Length; i++)
                if (old[i] && old[i].name.StartsWith("OutfitCloth_", StringComparison.Ordinal)
                    && !old[i].parent.name.StartsWith("OutfitCloth_", StringComparison.Ordinal))
                    DestroyNode(old[i].gameObject);
        应用(target, rig ? rig.配置 : null, body);
        target.sharedMesh = source.sharedMesh;
        var previewRig = target.GetComponent<AppearanceClothRig>();
        if (previewRig) previewRig.enabled = false; // 面板在手动动画之后、Render 之前推进。
    }

    public void 清除()
    {
        if (衣骨 != null && 配置)
            for (int i = 0; i < 衣骨.Length; i++)
                if (衣骨[i] && 配置.衣骨[i].上级衣骨 < 0) DestroyNode(衣骨[i].gameObject);
        衣骨 = null; 配置 = null; 人体骨 = null;
        末端 = 速度 = null;
        已初始化 = false;
    }

    static void DestroyNode(GameObject node)
    {
        node.SetActive(false);
        if (Application.isPlaying) Destroy(node); else DestroyImmediate(node);
    }

    void OnEnable() { 已初始化 = false; }
    void LateUpdate()
    {
        if (!配置 || 衣骨 == null || 衣骨.Length == 0) return;
        // 预览自己推进手动动画，真实角色用 scaled time，暂停时衣服也暂停。
        推进(Time.deltaTime, Time.time);
    }

    public void 重置随动()
    {
        if (!配置 || 衣骨 == null) return;
        if (末端 == null || 末端.Length != 衣骨.Length)
        { 末端 = new Vector3[衣骨.Length]; 速度 = new Vector3[衣骨.Length]; }
        for (int i = 0; i < 衣骨.Length; i++)
        {
            衣骨[i].localRotation = 配置.衣骨[i].局部旋转;
            末端[i] = 衣骨[i].TransformPoint(配置.衣骨[i].末端偏移);
            速度[i] = Vector3.zero;
        }
        上帧锚点 = 人体骨[1].position;
        上帧时间 = -1;
        已初始化 = true;
    }

    public void 推进(float dt, float clock)
    {
        if (!配置 || 衣骨 == null) return;
        if (!已初始化 || (人体骨[1].position - 上帧锚点).sqrMagnitude > 4 || dt > .12f
            || clock < 上帧时间) { 重置随动(); 上帧时间 = clock; return; }
        if (dt <= 0) return;
        // 小步弹簧在低帧率下保持稳定；每节局部姿态从静止值起算，避免累计旋转。
        int steps = Mathf.Clamp(Mathf.CeilToInt(dt / (1f / 90)), 1, 12);
        float h = dt / steps;
        float scale = Mathf.Abs(transform.lossyScale.x);
        for (int step = 0; step < steps; step++)
            for (int i = 0; i < 衣骨.Length; i++)
            {
                var bone = 衣骨[i]; var spec = 配置.衣骨[i];
                float frequency=spec.头发?6:配置.回弹频率;
                float spring=Mathf.Pow(frequency*Mathf.PI*2,2);
                float retention=Mathf.Exp(-(spec.头发?1:配置.阻尼)*frequency*Mathf.PI*4*h);
                bone.localRotation = spec.局部旋转;
                var head = bone.position;
                var rest = bone.TransformVector(spec.末端偏移);
                var previous = 末端[i];
                // 风只作小幅随动，不移动衣服根部，也不改变人体骨骼。
                var wind = new Vector3(Mathf.Sin(clock * 1.3f + i * .7f), .12f, Mathf.Cos(clock * .9f + i * .4f))
                    * 配置.微风强度 * scale;
                var predicted = previous + 速度[i] * (h * retention)
                    + (head + rest + wind * .08f - previous) * spring * (h * h);
                var end=predicted;
                // 长发更收敛，根部仍固定在头骨；重复投影兼顾段长、限角和身体避让。
                for(int iteration=0;iteration<3;iteration++){
                    end = 身体约束(end, spec.衣袖, spec.头发, scale);
                    var direction = Vector3.RotateTowards(rest, end - head, (spec.头发?6:配置.单节限角) * Mathf.Deg2Rad, 0);
                    end=head+direction.normalized*rest.magnitude;
                }
                bone.rotation = Quaternion.FromToRotation(rest, end - head) * bone.rotation;
                速度[i] = (end - previous) / h;
                末端[i] = end;
            }
        上帧锚点 = 人体骨[1].position;
        上帧时间 = clock;
    }

    Vector3 身体约束(Vector3 point, bool sleeve, bool hair, float scale)
    {
        point = 胶囊外(point, 人体骨[1].position, 人体骨[23].position, (hair?.082f:.06f) * scale);
        if(hair){
            var up=(人体骨[26].position-人体骨[24].position).normalized;
            point=胶囊外(point,人体骨[24].position,人体骨[26].position+up*(.12f*scale),.075f*scale);
            point=胶囊外(point,人体骨[31].position,人体骨[55].position,.06f*scale);
            point=胶囊外(point,人体骨[31].position,人体骨[36].position,.033f*scale);
            point=胶囊外(point,人体骨[55].position,人体骨[60].position,.033f*scale);
        }
        else if (!sleeve)
        {
            point = 胶囊外(point, 人体骨[3].position, 人体骨[4].position, .035f * scale);
            point = 胶囊外(point, 人体骨[4].position, 人体骨[5].position, .03f * scale);
            point = 胶囊外(point, 人体骨[12].position, 人体骨[13].position, .035f * scale);
            point = 胶囊外(point, 人体骨[13].position, 人体骨[15].position, .03f * scale);
        }
        else
        {
            point = 胶囊外(point, 人体骨[31].position, 人体骨[36].position, .025f * scale);
            point = 胶囊外(point, 人体骨[55].position, 人体骨[60].position, .025f * scale);
        }
        return point;
    }

    static Vector3 胶囊外(Vector3 p, Vector3 a, Vector3 b, float radius)
    {
        var axis = b - a;
        var closest = a + axis * Mathf.Clamp01(Vector3.Dot(p - a, axis) / Mathf.Max(.000001f, axis.sqrMagnitude));
        var offset = p - closest;
        float distance = offset.magnitude;
        return distance < radius && distance > .00001f ? closest + offset * (radius / distance) : p;
    }
}

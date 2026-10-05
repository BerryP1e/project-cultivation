using UnityEngine;

/// <summary>只处理神通特效的生命周期和体型，不参与伤害公式。</summary>
public static class AbilityVfxUtility
{
    public static GameObject 生成(string 路径, Transform 父, float 缩放, bool 循环 = false)
    {
        var 资源 = Resources.Load<GameObject>(路径);
        if (资源 == null) { Debug.LogWarning("[神通] 特效缺失：" + 路径); return null; }
        var 物体 = Object.Instantiate(资源, 父);
        物体.transform.localPosition = Vector3.zero;
        物体.transform.localRotation = Quaternion.identity;
        物体.transform.localScale = Vector3.one * 缩放;
        foreach (var 粒子 in 物体.GetComponentsInChildren<ParticleSystem>(true))
        {
            var 主 = 粒子.main;
            主.scalingMode = ParticleSystemScalingMode.Hierarchy;
            主.simulationSpace = ParticleSystemSimulationSpace.Local;
            if (循环)
            {
                粒子.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                主.loop = true;
                // 原素材多为单次 burst；周期至少等于寿命，避免叠出越来越厚的护罩。
                主.duration = Mathf.Max(主.duration, 主.startLifetime.constantMax);
            }
            粒子.Play();
        }
        return 物体;
    }

    public static Bounds 身体边界(Transform 根)
    {
        Bounds 边界 = new Bounds(根.position + Vector3.up * .8f, new Vector3(.7f, 1.6f, .7f));
        bool 有 = false;
        foreach (var 网格 in 根.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!网格.enabled || !网格.gameObject.activeInHierarchy) continue;
            if (!有) { 边界 = 网格.bounds; 有 = true; }
            else 边界.Encapsulate(网格.bounds);
        }
        return 边界;
    }

    public static Vector3 命中点(Transform 根) => 身体边界(根).center;

    public static Transform 胸骨(Transform 根)
    {
        foreach (var 动画 in 根.GetComponentsInChildren<Animator>())
            if (动画.isHuman && 动画.avatar != null)
            {
                var 骨 = 动画.GetBoneTransform(HumanBodyBones.UpperChest) ?? 动画.GetBoneTransform(HumanBodyBones.Chest);
                if (骨 != null) return 骨;
            }
        // Tripo Generic 骨架：优先上胸，不使用人物根节点假装跟随胸部。
        string[] 名称 = { "Spine02", "Spine2", "Spine01", "Spine1", "spine_02", "Chest", "mixamorig:Spine2" };
        foreach (string 名 in 名称)
            foreach (var 骨 in 根.GetComponentsInChildren<Transform>())
                if (骨.name == 名) return 骨;
        return 根;
    }

    // PurifierBeam 原生沿 +Z，梁中心位于 Z=37.5，长度 75；横截面与长度分别标定。
    public static void 对准光束(GameObject 光束, Vector3 起点, Vector3 终点, float 粗细 = .07f)
    {
        if (光束 == null) return;
        Vector3 差 = 终点 - 起点;
        光束.transform.position = 起点;
        光束.transform.rotation = Quaternion.FromToRotation(Vector3.forward, 差.normalized);
        光束.transform.localScale = new Vector3(粗细, 粗细, Mathf.Max(.001f, 差.magnitude / 75f));
    }
}

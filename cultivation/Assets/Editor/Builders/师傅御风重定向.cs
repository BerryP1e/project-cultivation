using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 把【凭虚御风】的四个动作**重定向到师傅自己的骨架**上（逐帧烘焙成 Generic 片段），
/// 并给师傅的 Animator 挂一个**只覆盖这四个动作**的 AnimatorOverrideController。
///
/// ## 病根（2026-10-03 用户报「师傅御风时是躺着过来的」）
///
/// 三段事实拼起来才是真相，缺一段都会修错方向：
///
/// 1. `御风_升空/Idle/前进/落地`（<see cref="YufengClipBuilder"/> 生成）是
///    **Humanoid 肌肉曲线**（`path=""` + 肌肉名），只有**带 Avatar 的 Animator** 才播得动。
/// 2. NPC 的 Tripo FBX 全是 **Generic**（`Animator.avatar = null`），
///    所以 NPC 用的是当年**烘到大师兄骨架上的** `大师兄_御风_*`（`path="Armature/BoneRoot/..."` 的
///    绝对局部旋转）。**绝对旋转是绑死骨架的** —— 只有静止姿势一致的骨架才能原样复用。
/// 3. **师傅的骨架和大师兄不是一套朝向**：实测两套骨架同名骨骼的静止局部旋转
///    **31 根差 >20°，最大差 165°**（`R_Thumb1`）。于是同一段绝对旋转套到师傅身上，
///    整个人就翻了 —— 实测「头→髋」方向与世界上方夹角从 **5° 变成 117°**（头比髋还低）= 躺倒。
///
/// ## 修法：照大师兄当年那条路，只是换成师傅自己的 Avatar
///
/// ① 把师傅的 FBX 导入设置改成 **Humanoid + 自建 Avatar**（用他自己的静止姿势，
///    ⚠️ **绝不能用别人的 Avatar**：骨骼路径相同但静止姿势不同，借 Avatar 一样会翻）；
/// ② 用 `<see cref="HumanPoseHandler"/>` 把四个**人形片段**逐帧重定向到师傅骨架上，
///    读回每根骨骼的局部 TRS，写成这一套骨架自己的 **Generic 片段**；
/// ③ 建 `AnimatorOverrideController`（基 = `大师兄.controller`）**只换这四个动作**，
///    挂到师傅 prefab 的 Animator 上 —— **Animator.avatar 仍然保持 null**，
///    所以 Idle/Walk/Run/… 那 9 个原样能用（与大师兄当年完全一致的做法）。
///
/// ## 为什么不像玩家那样直接给 NPC 挂 Avatar
///
/// 控制器里另外 9 个片段是 Generic（`成男村民.fbx` 的路径曲线），**人形骨架的 Animator
/// 播不了它们**。给 NPC 挂人形 Avatar = 那 9 个动作全废。所以只能「烘焙 + 覆盖」这条路。
///
/// ## 用法
///
/// 菜单：`修仙 / NPC 资产 / 师傅御风重定向（一键）`（ASCIL：Cultivation / Retarget Master Yufeng）
/// 先跑 `只检查` 可以只看不改（会打印 Avatar 是否可用、现有片段绑骨情况）。
/// </summary>
public static class 师傅御风重定向
{
    // ---- 师傅 ----
    const string 师傅Fbx = "Assets/TripoModels/太虚宗掌门（师傅）/太虚宗掌门（师傅）.fbx";
    const string 师傅Prefab = "Assets/resources/NPC/Human/太虚宗掌门/太虚宗掌门.prefab";
    const string 师傅输出前缀 = "太虚宗掌门_御风_";

    // ---- 通用 ----
    const string 基控制器 = "Assets/Animations/NPC/大师兄.controller";
    const string 覆盖控制器 = "Assets/Animations/NPC/太虚宗掌门.controller";
    const string 片段目录 = "Assets/Animations/凭虚御风";
    const string 源前缀 = "御风_";

    /// <summary>要重定向的四个动作（后缀）：源 = `御风_<后缀>.anim`，产出 = `<前缀><后缀>.anim`</summary>
    static readonly string[] 四个动作 = { "升空", "Idle", "前进", "落地" };

    const int Fps = 30;

    /// <summary>骨骼局部值与静止值差多少才算"这根骨骼被动作动了"</summary>
    const float 位置阈值 = 0.0005f;
    const float 旋转阈值 = 0.0005f;

    [MenuItem("修仙/NPC 资产/师傅御风重定向（一键：建Avatar + 烘焙 + 换控制器）", false, 260)]
    public static void 一键()
    {
        var avatar = 确保Avatar();
        if (avatar == null) return;

        var 产出 = 烘焙(avatar);
        if (产出 == null || 产出.Count == 0) return;

        换控制器();
        验证(产出);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[师傅御风] 完成：四个片段 + 覆盖控制器都已就位");
    }

    [MenuItem("修仙/NPC 资产/师傅御风重定向 · 只检查", false, 261)]
    public static void 只检查()
    {
        var imp = AssetImporter.GetAtPath(师傅Fbx) as ModelImporter;
        Debug.Log("[师傅御风·检查] FBX 导入：type=" + (imp == null ? "拿不到" : imp.animationType.ToString())
                  + " avatarSetup=" + (imp == null ? "-" : imp.avatarSetup.ToString()));

        var avatar = 取现有Avatar();
        Debug.Log("[师傅御风·检查] Avatar=" + (avatar == null ? "没有"
            : avatar.name + " isHuman=" + avatar.isHuman + " isValid=" + avatar.isValid));

        var 控 = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(覆盖控制器);
        Debug.Log("[师傅御风·检查] 覆盖控制器=" + (控 == null ? "还没有" : 覆盖控制器));

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(师傅Prefab);
        var anim = prefab != null ? prefab.GetComponent<Animator>() : null;
        Debug.Log("[师傅御风·检查] prefab Animator：avatar=" + (anim == null || anim.avatar == null ? "null（对的）" : anim.avatar.name)
                  + " controller=" + (anim == null || anim.runtimeAnimatorController == null ? "null" : anim.runtimeAnimatorController.name));

        foreach (var 名 in 四个动作)
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(片段目录 + "/" + 师傅输出前缀 + 名 + ".anim");
            Debug.Log("[师傅御风·检查] " + 师傅输出前缀 + 名 + " = " + (c == null ? "还没有" : "有（" + c.length.ToString("F2") + "s）"));
        }
    }

    // ================================================================ ① Avatar

    static Avatar 取现有Avatar()
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(师傅Fbx))
            if (o is Avatar a) return a;
        return null;
    }

    /// <summary>
    /// 保证师傅的 FBX 是「Humanoid + 自建 Avatar」。已经是对的就直接返回。
    /// ⚠️ 必须是**他自己的** Avatar。
    /// </summary>
    static Avatar 确保Avatar()
    {
        var imp = AssetImporter.GetAtPath(师傅Fbx) as ModelImporter;
        if (imp == null)
        {
            Debug.LogError("[师傅御风] 拿不到 ModelImporter：" + 师傅Fbx);
            return null;
        }

        bool 要改 = imp.animationType != ModelImporterAnimationType.Human
                    || imp.avatarSetup == ModelImporterAvatarSetup.NoAvatar;

        if (要改)
        {
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.SaveAndReimport();
            Debug.Log("[师傅御风] 已把 " + 师傅Fbx + " 改成 Humanoid + 自建 Avatar 并重新导入");
        }

        var avatar = 取现有Avatar();
        if (avatar == null)
        {
            Debug.LogError("[师傅御风] 改完导入设置仍然没有 Avatar —— 打开 FBX 的 Rig 页手动确认一下");
            return null;
        }
        if (!avatar.isHuman || !avatar.isValid)
        {
            Debug.LogError("[师傅御风] Avatar 不合法（isHuman=" + avatar.isHuman + " isValid=" + avatar.isValid
                           + "）：人形自动映射没成功。要么手动去 Rig 页映射骨骼，要么退回「按骨骼名做坐标换算」的方案。"
                           + "**没有继续烘焙**。");
            return null;
        }

        int 人骨 = avatar.humanDescription.human != null ? avatar.humanDescription.human.Length : 0;
        Debug.Log("[师傅御风] Avatar=" + avatar.name + " isHuman=True isValid=True 人骨=" + 人骨);
        return avatar;
    }

    // ================================================================ ② 烘焙

    /// <summary>
    /// 逐帧把四个人形片段重定向到师傅骨架上，写成师傅自己的 Generic 片段。
    /// 返回产出片段（后缀 → 片段）。
    /// </summary>
    static Dictionary<string, AnimationClip> 烘焙(Avatar avatar)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(师傅Prefab);
        if (prefab == null) { Debug.LogError("[师傅御风] 找不到 prefab：" + 师傅Prefab); return null; }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;

        var anim = go.GetComponent<Animator>();
        if (anim == null) { Debug.LogError("[师傅御风] prefab 上没有 Animator"); Object.DestroyImmediate(go); return null; }

        // 采样期间把 Animator 停掉：它会每帧按自己的控制器写姿势，和烘焙抢方向盘
        anim.runtimeAnimatorController = null;
        anim.enabled = false;
        anim.avatar = avatar;

        // 骨骼清单（相对 prefab 根的路径 = 片段里要写的 path）
        var 骨骼 = new List<Transform>();
        var 路径 = new Dictionary<Transform, string>();
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            if (t == go.transform) continue;
            骨骼.Add(t);
            路径[t] = 相对路径(go.transform, t);
        }
        Debug.Log("[师傅御风] 目标骨架：骨骼 " + 骨骼.Count + " 根");

        // 静止姿势（= prefab 里存的姿势）：动作没动到的骨骼就不写曲线，留它用静止值
        var 静止位 = new Dictionary<Transform, Vector3>();
        var 静止转 = new Dictionary<Transform, Quaternion>();
        foreach (var t in 骨骼) { 静止位[t] = t.localPosition; 静止转[t] = t.localRotation; }

        var handler = new HumanPoseHandler(avatar, go.transform);
        var 产出 = new Dictionary<string, AnimationClip>();

        try
        {
            foreach (var 名 in 四个动作)
            {
                string 源路径 = 片段目录 + "/" + 源前缀 + 名 + ".anim";
                var 源 = AssetDatabase.LoadAssetAtPath<AnimationClip>(源路径);
                if (源 == null) { Debug.LogError("[师傅御风] 找不到源片段：" + 源路径); continue; }
                if (!源.humanMotion)
                {
                    Debug.LogError("[师傅御风] " + 源.name + " 不是人形片段（humanMotion=false）—— 没法重定向，跳过");
                    continue;
                }

                var clip = 烘一片(源, handler, 骨骼, 路径, 静止位, 静止转);
                if (clip == null) continue;

                string 出路径 = 片段目录 + "/" + 师傅输出前缀 + 名 + ".anim";
                var 旧 = AssetDatabase.LoadAssetAtPath<AnimationClip>(出路径);
                if (旧 != null) AssetDatabase.DeleteAsset(出路径);
                AssetDatabase.CreateAsset(clip, 出路径);
                产出[名] = clip;
                Debug.Log("[师傅御风] ✔ " + clip.name + "（" + clip.length.ToString("F2") + "s，"
                          + AnimationUtility.GetCurveBindings(clip).Length + " 条曲线）→ " + 出路径);
            }
        }
        finally
        {
            handler.Dispose();
            Object.DestroyImmediate(go);
        }

        return 产出;
    }

    static AnimationClip 烘一片(AnimationClip 源, HumanPoseHandler handler,
                               List<Transform> 骨骼, Dictionary<Transform, string> 路径,
                               Dictionary<Transform, Vector3> 静止位, Dictionary<Transform, Quaternion> 静止转)
    {
        // 源片段的人形数据：肌肉曲线（path=""）+ 根位移/根旋转
        var 肌肉曲线 = new Dictionary<string, AnimationCurve>();
        foreach (var b in AnimationUtility.GetCurveBindings(源))
        {
            if (!string.IsNullOrEmpty(b.path)) continue;
            var c = AnimationUtility.GetEditorCurve(源, b);
            if (c != null) 肌肉曲线[b.propertyName] = c;
        }
        var 根位 = new AnimationCurve[3];
        var 根转 = new AnimationCurve[4];
        for (int i = 0; i < 3; i++) 肌肉曲线.TryGetValue("RootT." + "xyz"[i], out 根位[i]);
        for (int i = 0; i < 4; i++) 肌肉曲线.TryGetValue("RootQ." + "xyzw"[i], out 根转[i]);

        // 逐帧重定向，先把每根骨骼的采样值存下来，最后再决定给谁写曲线
        int 帧数 = Mathf.Max(2, Mathf.RoundToInt(源.length * Fps));
        var 位采样 = new Dictionary<Transform, Vector3[]>();
        var 转采样 = new Dictionary<Transform, Quaternion[]>();
        foreach (var t in 骨骼) { 位采样[t] = new Vector3[帧数 + 1]; 转采样[t] = new Quaternion[帧数 + 1]; }

        var pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
        for (int f = 0; f <= 帧数; f++)
        {
            float t = 源.length * f / 帧数;

            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                string mn = HumanTrait.MuscleName[i];
                pose.muscles[i] = 肌肉曲线.TryGetValue(mn, out var c) && c != null ? c.Evaluate(t) : 0f;
            }
            pose.bodyPosition = new Vector3(取(根位[0], t), 取(根位[1], t), 取(根位[2], t));
            pose.bodyRotation = new Quaternion(取(根转[0], t), 取(根转[1], t), 取(根转[2], t), 取(根转[3], t));
            if (pose.bodyRotation.x == 0f && pose.bodyRotation.y == 0f
                && pose.bodyRotation.z == 0f && pose.bodyRotation.w == 0f)
                pose.bodyRotation = Quaternion.identity;

            handler.SetHumanPose(ref pose);

            foreach (var tr in 骨骼) { 位采样[tr][f] = tr.localPosition; 转采样[tr][f] = tr.localRotation; }
        }

        // 写曲线：只写"被动作动过"的骨骼（和大师兄那四个片段的风格一致：没动的骨骼留静止值）
        var clip = new AnimationClip { name = 师傅输出前缀 + 源.name.Replace(源前缀, ""), frameRate = Fps };
        int 写了 = 0;
        foreach (var tr in 骨骼)
        {
            string p = 路径[tr];
            bool 动过 = false;
            for (int f = 0; f <= 帧数; f++)
            {
                if ((位采样[tr][f] - 静止位[tr]).magnitude > 位置阈值) { 动过 = true; break; }
                if (Quaternion.Angle(转采样[tr][f], 静止转[tr]) > 旋转阈值 * 57.29578f) { 动过 = true; break; }
            }
            if (!动过) continue;
            写了++;

            var 位曲线 = new AnimationCurve[3];
            var 转曲线 = new AnimationCurve[4];
            for (int i = 0; i < 3; i++) 位曲线[i] = new AnimationCurve();
            for (int i = 0; i < 4; i++) 转曲线[i] = new AnimationCurve();

            for (int f = 0; f <= 帧数; f++)
            {
                float t = 源.length * f / 帧数;
                var q = 转采样[tr][f];
                for (int i = 0; i < 4; i++) 转曲线[i].AddKey(new Keyframe(t, i == 0 ? q.x : i == 1 ? q.y : i == 2 ? q.z : q.w));
                var v = 位采样[tr][f];
                for (int i = 0; i < 3; i++) 位曲线[i].AddKey(new Keyframe(t, i == 0 ? v.x : i == 1 ? v.y : v.z));
            }

            for (int i = 0; i < 4; i++)
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(p, typeof(Transform), "localRotation." + "xyzw"[i]), 转曲线[i]);
            for (int i = 0; i < 3; i++)
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(p, typeof(Transform), "localPosition." + "xyz"[i]), 位曲线[i]);
        }
        Debug.Log("[师傅御风]   " + clip.name + "：动了 " + 写了 + "/" + 骨骼.Count + " 根骨骼");

        var 设置 = AnimationUtility.GetAnimationClipSettings(clip);
        设置.loopTime = AnimationUtility.GetAnimationClipSettings(源).loopTime;
        AnimationUtility.SetAnimationClipSettings(clip, 设置);
        return clip;
    }

    static float 取(AnimationCurve c, float t) => c != null ? c.Evaluate(t) : 0f;

    static string 相对路径(Transform 根, Transform 子)
    {
        if (子 == 根) return "";
        string p = 子.name;
        var t = 子.parent;
        while (t != null && t != 根) { p = t.name + "/" + p; t = t.parent; }
        return p;
    }

    // ================================================================ ③ 覆盖控制器

    /// <summary>
    /// 建/更新覆盖控制器：基 = 大师兄.controller，只把四个御风动作换成师傅自己的。
    /// ⚠️ 只覆盖这四个 —— 其余 9 个动作大师兄怎么演师傅就怎么演（那些是路径曲线，两套骨架都能吃）。
    /// </summary>
    static void 换控制器()
    {
        var 基 = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(基控制器);
        if (基 == null) { Debug.LogError("[师傅御风] 找不到基控制器：" + 基控制器); return; }

        var ov = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(覆盖控制器);
        if (ov == null)
        {
            ov = new AnimatorOverrideController(基);
            AssetDatabase.CreateAsset(ov, 覆盖控制器);
        }
        else ov.runtimeAnimatorController = 基;

        var 对 = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        ov.GetOverrides(对);
        int 换了几条 = 0;
        for (int i = 0; i < 对.Count; i++)
        {
            var 原 = 对[i].Key;
            if (原 == null) continue;
            foreach (var 名 in 四个动作)
            {
                if (原.name != "大师兄_御风_" + 名) continue;
                var 新 = AssetDatabase.LoadAssetAtPath<AnimationClip>(片段目录 + "/" + 师傅输出前缀 + 名 + ".anim");
                if (新 == null) continue;
                对[i] = new KeyValuePair<AnimationClip, AnimationClip>(原, 新);
                换了几条++;
            }
        }
        ov.ApplyOverrides(对);
        EditorUtility.SetDirty(ov);
        Debug.Log("[师傅御风] 覆盖控制器 " + 覆盖控制器 + "：换了 " + 换了几条 + "/4 条（基=" + 基控制器 + "）");

        // 挂到 prefab 上（改资产，不动场景里的实例）
        var 内容 = PrefabUtility.LoadPrefabContents(师傅Prefab);
        try
        {
            var anim = 内容.GetComponent<Animator>();
            if (anim == null) { Debug.LogError("[师傅御风] prefab 上没有 Animator"); return; }
            anim.runtimeAnimatorController = ov;
            if (anim.avatar != null)
            {
                Debug.LogWarning("[师傅御风] prefab 的 Animator 原来挂着 avatar「" + anim.avatar.name
                                 + "」—— 已清空（人形 Avatar 会让那 9 个 Generic 动作播不动）");
                anim.avatar = null;
            }
            PrefabUtility.SaveAsPrefabAsset(内容, 师傅Prefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(内容);
        }
        Debug.Log("[师傅御风] 已把覆盖控制器挂到 " + 师傅Prefab);
    }

    // ================================================================ ④ 验证

    /// <summary>
    /// 用师傅自己的片段和大师兄的片段各跑一遍，量「头→髋」与世界上方的夹角：
    /// 站/飞都该在 90° 以内（头在髋上方），**接近或超过 90° = 躺倒**。
    /// </summary>
    static void 验证(Dictionary<string, AnimationClip> 产出)
    {
        var 大师兄Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/resources/NPC/Human/大师兄/大师兄.prefab");
        var 师傅 = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(师傅Prefab));
        var 大师兄 = 大师兄Prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(大师兄Prefab) : null;
        师傅.transform.position = new Vector3(0, 40, 0);
        if (大师兄 != null) 大师兄.transform.position = new Vector3(3, 40, 0);

        var a = 师傅.GetComponent<Animator>(); if (a != null) { a.runtimeAnimatorController = null; a.enabled = false; }

        foreach (var 名 in 四个动作)
        {
            var 剪 = AssetDatabase.LoadAssetAtPath<AnimationClip>(片段目录 + "/" + 师傅输出前缀 + 名 + ".anim");
            if (剪 == null) continue;
            剪.SampleAnimation(师傅, 0.5f * Mathf.Min(1f, 剪.length));
            Debug.Log("[师傅御风·验证] " + 剪.name + "：头→髋与上方夹角 = " + 夹角(师傅).ToString("F1") + "°"
                      + (夹角(师傅) > 85f ? "  ← 还是躺的 ✗" : "  ← 头在上 ✓"));
        }
        var 大师兄剪 = AssetDatabase.LoadAssetAtPath<AnimationClip>(片段目录 + "/大师兄_御风_前进.anim");
        if (大师兄 != null && 大师兄剪 != null)
        {
            大师兄剪.SampleAnimation(大师兄, 0.5f);
            Debug.Log("[师傅御风·验证] （对照）大师兄_御风_前进 = " + 夹角(大师兄).ToString("F1") + "°");
        }

        Object.DestroyImmediate(师傅);
        if (大师兄 != null) Object.DestroyImmediate(大师兄);
    }

    static float 夹角(GameObject go)
    {
        Transform 髋 = null, 头 = null;
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Hip" && 髋 == null) 髋 = t;
            if (t.name == "Head" && 头 == null) 头 = t;
        }
        if (髋 == null || 头 == null) return -1f;
        return Vector3.Angle((头.position - 髋.position).normalized, Vector3.up);
    }
}

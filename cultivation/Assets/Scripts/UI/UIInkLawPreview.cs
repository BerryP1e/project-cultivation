using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>法则页独立御风预览；周围灵粒向倾斜仪轨聚拢，循轨后消散。</summary>
public sealed class UIInkLawPreview : MonoBehaviour
{
    public Transform 右手 => hand;
    public int 活跃粒子数 { get; private set; }
    Transform actor, hand, orbit;
    Transform appearanceSource, clonedVisual;
    PlayableGraph graph;
    AnimationClipPlayable idlePlayable;
    float length, clock, emission;
    ParticleSystem particles;
    Material particleMaterial;
    readonly List<Material> ownedMaterials = new List<Material>();
    readonly List<Mote> motes = new List<Mote>(192);
    readonly ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[192];
    struct Mote { public float born, life, phase, size; public Vector3 source; }

    public void 初始化(Transform source, Transform lawOrbit)
    {
        appearanceSource = source;
        orbit = lawOrbit;
        if (source)
        {
            var holder = new GameObject("FloatingActor"); holder.transform.SetParent(transform, false); holder.SetActive(false);
            actor = holder.transform;
            var clone = Instantiate(source.gameObject, actor, false); clone.name = "WindIdleVisual";
            clonedVisual = clone.transform;
            // 在激活前移除游戏逻辑，舞台只保留骨架、Animator 和渲染器。
            foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(behaviour);
            foreach (var collider in clone.GetComponentsInChildren<Collider>(true)) DestroyImmediate(collider);
            foreach (var body in clone.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(body);
            foreach (var light in clone.GetComponentsInChildren<Light>(true)) DestroyImmediate(light);
            foreach (var camera in clone.GetComponentsInChildren<Camera>(true)) DestroyImmediate(camera);
            foreach (var audio in clone.GetComponentsInChildren<AudioSource>(true)) DestroyImmediate(audio);
            foreach (Transform node in clone.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
            clone.transform.localPosition = Vector3.zero; clone.transform.localRotation = Quaternion.identity;
            var clip = System.Array.Find(source.GetComponentInChildren<Animator>(true)?.runtimeAnimatorController?.animationClips ?? new AnimationClip[0], x => x.name == "御风八向_悬停");
            if (!clip) clip = Resources.Load<AnimationClip>("技能动作/御风八向/灵虚剑决/御风八向_悬停");
            var animator = clone.GetComponentsInChildren<Animator>(true);
            Animator human = null;
            foreach (var candidate in animator)
            {
                candidate.runtimeAnimatorController = null; candidate.applyRootMotion = false; candidate.cullingMode = AnimatorCullingMode.AlwaysAnimate; candidate.enabled = true;
                if (candidate.isHuman && candidate.avatar && candidate.avatar.isValid) human = candidate;
            }
            holder.SetActive(true);
            if (human && clip)
            {
                human.Rebind();
                graph = PlayableGraph.Create("LawWindIdle"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                idlePlayable = AnimationClipPlayable.Create(graph, clip); idlePlayable.SetApplyFootIK(false); length = clip.length;
                AnimationPlayableOutput.Create(graph, "WindIdle", human).SetSourcePlayable(idlePlayable); graph.Play(); graph.Evaluate(0);
                hand = human.GetBoneTransform(HumanBodyBones.RightHand);
            }
            var brush = Shader.Find("Cultivation/UI/FormationModelBrush");
            Bounds bounds = new Bounds(); bool any = false;
            foreach (var skin in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.updateWhenOffscreen = true;
                if (!skin.enabled || !skin.sharedMesh || !skin.gameObject.activeInHierarchy) continue;
                var baked = new Mesh(); skin.BakeMesh(baked);
                var matrix = actor.worldToLocalMatrix * skin.transform.localToWorldMatrix;
                foreach (var vertex in baked.vertices) { var p = matrix.MultiplyPoint3x4(vertex); if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; } else bounds.Encapsulate(p); }
                Destroy(baked);
            }
            foreach (var renderer in clone.GetComponentsInChildren<Renderer>(true))
            {
                // 武器和旧附加特效不参与人物展示。
                if (!(renderer is SkinnedMeshRenderer)) { renderer.enabled = false; continue; }
                var mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var original = mats[i]; var mat = new Material(brush); ownedMaterials.Add(mat);
                    if (original) { if (original.HasProperty("_MainTex")) mat.mainTexture = original.mainTexture; if (original.HasProperty("_Color")) mat.color = original.color; }
                    mats[i] = mat;
                }
                renderer.sharedMaterials = mats;
            }
            if (human)
            {
                // 用骨骼标定高度，避免 BakeMesh 在带缩放骨架上重复计算缩放。
                var head = actor.InverseTransformPoint(human.GetBoneTransform(HumanBodyBones.Head).position);
                var left = actor.InverseTransformPoint(human.GetBoneTransform(HumanBodyBones.LeftFoot).position);
                var right = actor.InverseTransformPoint(human.GetBoneTransform(HumanBodyBones.RightFoot).position);
                var hips = actor.InverseTransformPoint(human.GetBoneTransform(HumanBodyBones.Hips).position);
                float bottom = Mathf.Min(left.y, right.y);
                float scale = 2.6f / Mathf.Max(.1f, head.y - bottom + human.humanScale * .32f);
                clone.transform.localPosition = -new Vector3(hips.x, bottom, hips.z) * scale; clone.transform.localScale *= scale;
            }
            else if (any) { float scale = 2.5f / Mathf.Max(.1f, bounds.size.y); clone.transform.localPosition = -new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale; clone.transform.localScale *= scale; }
            actor.localRotation = Quaternion.Euler(0, 175, 0);
            actor.localScale = Vector3.one * .5f;
        }
        var emitter = new GameObject("AmbientToLawMotes", typeof(ParticleSystem)); emitter.layer = 31; emitter.transform.SetParent(transform, false);
        particles = emitter.GetComponent<ParticleSystem>();
        var main = particles.main; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = buffer.Length;
        var auto = particles.emission; auto.enabled = false; particles.Play();
        particleMaterial = new Material(Shader.Find("Sprites/Default"));
        var sprite = InkUITheme.Load("SkillsDynamic/fx-star-particle"); if (sprite) particleMaterial.mainTexture = sprite.texture;
        particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMaterial;
    }

    public void 推进(float delta, float angle, bool reduced)
    {
        clock += reduced ? 0 : delta;
        if (graph.IsValid()) { idlePlayable.SetTime(clock % Mathf.Max(.01f, length)); graph.Evaluate(0); }
        if (actor) actor.localPosition = new Vector3(0, .55f + Mathf.Sin(clock * .95f) * .045f, 0);
        if (!particles || !orbit) return;
        emission += reduced ? 0 : delta * 30;
        while (emission >= 1)
        {
            emission -= 1; if (motes.Count >= buffer.Length) continue;
            float phase = Random.Range(0, Mathf.PI * 2), spread = Random.Range(1.22f, 1.65f);
            var source = orbit.TransformPoint(new Vector3(Mathf.Sin(phase) * 2.05f * spread, Random.Range(-.35f, .4f), Mathf.Cos(phase) * 1.28f * spread));
            motes.Add(new Mote { born = clock, life = Random.Range(4.8f, 6.4f), phase = phase, size = Random.Range(.055f, .10f), source = source });
        }
        for (int i = motes.Count - 1; i >= 0; i--) if (clock - motes[i].born >= motes[i].life) motes.RemoveAt(i);
        for (int i = 0; i < motes.Count; i++)
        {
            var mote = motes[i]; float age = clock - mote.born, u = age / mote.life;
            float a = mote.phase + age * .82f;
            var destination = orbit.TransformPoint(new Vector3(Mathf.Sin(a) * 2.05f, Mathf.Sin(age * 2 + mote.phase) * .035f, Mathf.Cos(a) * 1.28f));
            float join = Mathf.SmoothStep(0, 1, age / 1.25f);
            var position = Vector3.Lerp(mote.source, destination, join) + Vector3.up * (Mathf.Sin(join * Mathf.PI) * .18f);
            float alpha = Mathf.Clamp01(age / .18f) * Mathf.Clamp01((mote.life - age) / 1.6f);
            var color = i % 4 == 0 ? new Color(.94f, .86f, .61f, alpha) : new Color(.72f, .91f, .84f, alpha);
            buffer[i] = new ParticleSystem.Particle { position = position, startSize = mote.size * (1 - u * .45f), startColor = color, startLifetime = 10, remainingLifetime = 10 };
        }
        活跃粒子数 = motes.Count; particles.SetParticles(buffer, motes.Count);
    }
    void 同步外观(AppearanceDefinition unused)
    {
        if (!appearanceSource || !clonedVisual) return;
        foreach (var skin in appearanceSource.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            string path = ""; var node = skin.transform;
            while (node != appearanceSource && node) { path = node.name + (path.Length == 0 ? "" : "/" + path); node = node.parent; }
            var destination = path.Length == 0 ? clonedVisual : clonedVisual.Find(path);
            var target = destination ? destination.GetComponent<SkinnedMeshRenderer>() : null;
            if (!target || !skin.sharedMesh) continue;
            target.sharedMesh = skin.sharedMesh;
            var mats = skin.sharedMaterials;
            foreach (var old in target.sharedMaterials) if (ownedMaterials.Remove(old)) Destroy(old);
            var replacement = new Material[mats.Length];
            for (int i = 0; i < mats.Length; i++) {
                var material = new Material(Shader.Find("Cultivation/UI/FormationModelBrush")); ownedMaterials.Add(material);
                if (mats[i]) { if (mats[i].HasProperty("_MainTex")) material.mainTexture = mats[i].mainTexture; if (mats[i].HasProperty("_Color")) material.color = mats[i].color; }
                replacement[i] = material;
            }
            target.sharedMaterials = replacement;
        }
    }
    void OnEnable() { 玩家外观.外观变化 += 同步外观; 同步外观(null); }
    void OnDisable() { 玩家外观.外观变化 -= 同步外观; motes.Clear(); emission = 0; 活跃粒子数 = 0; if (particles) particles.Clear(); }
    void OnDestroy() { if (graph.IsValid()) graph.Destroy(); foreach (var material in ownedMaterials) Destroy(material); if (particleMaterial) Destroy(particleMaterial); }
}

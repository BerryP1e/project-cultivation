using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>共用网格和材质，分层阵纹与符轨；每次施放只拥有节点和属性块。</summary>
public sealed class SpiritArrayVisual : IDisposable
{
    public SpiritArrayDefinition Definition { get; private set; }
    GameObject root;
    readonly List<Transform> layers=new List<Transform>();
    Transform pulse;
    Renderer pulseRenderer;
    float radius=1,flashAt=-100;
    SpiritArrayOp operation;
    Renderer[] renderers;
    MaterialPropertyBlock properties = new MaterialPropertyBlock();
    Color color;
    float flash;
    static Mesh orb;
    public bool Exists => root;
    public static SpiritArrayVisual Array(SpiritArrayDefinition definition, Vector3 point, Vector3 direction, SpiritArrayOp op)
    {
        EnsureMeshes();
        var v = new SpiritArrayVisual { Definition = definition, color = Tint(op), root = new GameObject("SpiritArray_" + op) };
        v.root.transform.SetPositionAndRotation(point, Quaternion.LookRotation(direction, Mathf.Abs(direction.y) > .98f ? Vector3.forward : Vector3.up));
        v.radius=SpiritArrayRules.Radius(op);v.operation=op;
        for(int i=0;i<SpiritArrayGeometry.Count(op);i++)v.layers.Add(v.Part(SpiritArrayGeometry.Name(op,i),SpiritArrayGeometry.Layer(op,i),definition.灵光材质,v.radius,(i-.5f)*.055f*v.radius));
        v.pulse=v.Part("ActivationWave",SpiritArrayGeometry.Pulse,definition.灵光材质,v.radius,0);v.pulseRenderer=v.pulse.GetComponent<Renderer>();
        v.renderers = v.root.GetComponentsInChildren<Renderer>(); v.Animate(0, 0, 1); return v;
    }
    public static SpiritArrayVisual Projectile(SpiritArrayDefinition definition, Vector3 point)
    {
        EnsureMeshes();
        var v = new SpiritArrayVisual { Definition = definition, color = new Color(.45f, .85f, 1), root = new GameObject("SpiritArrayProjectile") };
        v.root.transform.position = point;
        v.Part("Core", orb, definition.灵光材质, .10f, 0);
        var trail = v.root.AddComponent<TrailRenderer>(); trail.sharedMaterial = definition.灵光材质;
        trail.time = .24f; trail.startWidth = .09f; trail.endWidth = 0; trail.minVertexDistance = .05f;
        trail.startColor = new Color(.55f, .9f, 1, .8f); trail.endColor = new Color(.3f, .6f, 1, 0);
        trail.shadowCastingMode = ShadowCastingMode.Off; trail.receiveShadows = false;
        v.renderers = v.root.GetComponentsInChildren<Renderer>(); v.Animate(0, 1, 1); return v;
    }
    Transform Part(string name, Mesh mesh, Material material, float scale, float z)
    {
        var go = new GameObject(name); go.transform.SetParent(root.transform, false);
        go.transform.localScale = Vector3.one * scale; go.transform.localPosition = Vector3.forward * z;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        return go.transform;
    }
    public void Flash(bool success) { flash = success ? 1 : -1;flashAt=Time.time; }
    public void Animate(float age, float unfold, float fade)
    {
        if (!root) return;
        float progress = Mathf.SmoothStep(0, 1, unfold);
        root.transform.localScale = Vector3.one * Mathf.Lerp(.06f, 1, progress);
        float active=Mathf.Clamp01(1-(Time.time-flashAt)/.6f),boost=flash>0?active:0;
        for(int i=0;i<layers.Count;i++){
            layers[i].localRotation=Quaternion.Euler(Mathf.Sin(age*.7f+i)*3,Mathf.Cos(age*.6f+i)*3,age*SpiritArrayGeometry.Speed(operation,i)+boost*20);
            layers[i].localPosition=Vector3.forward*((i-.5f)*.055f+Mathf.Sin(age*1.6f+i)*.018f)*radius;
        }
        properties.SetColor("_TintColor",Color.Lerp(color,flash<0?new Color(1,.12f,.05f):new Color(2.2f,2.1f,1.5f),active*.85f));
        properties.SetFloat("_Opacity",fade*progress*(.68f+boost*.3f));
        foreach(var r in renderers)if(r&&r!=pulseRenderer)r.SetPropertyBlock(properties);
        if(pulseRenderer){pulse.localScale=Vector3.one*radius*Mathf.Lerp(1.75f,.3f,active);properties.SetFloat("_Opacity",fade*active*.9f);pulseRenderer.SetPropertyBlock(properties);}
    }

    public void Move(Vector3 point, Vector3 velocity)
    {
        if (!root) return;
        root.transform.position = point;
        if (velocity.sqrMagnitude > .001f) root.transform.rotation = Quaternion.LookRotation(velocity);
    }
    public void Dispose() { if (root) { root.SetActive(false); UnityEngine.Object.Destroy(root); } root = null; }
    public static Color Tint(SpiritArrayOp op)
    {
        switch (op) {
            case SpiritArrayOp.生成灵弹:return new Color(.65f,.9f,1.35f);
            case SpiritArrayOp.加速:return new Color(.25f,.9f,1.5f);
            case SpiritArrayOp.强化: return new Color(1.5f, .55f, .18f);
            case SpiritArrayOp.分裂: return new Color(.8f, .42f, 1);
            case SpiritArrayOp.追踪: return new Color(.5f, 1, .75f);
            default: return new Color(1, .84f, .45f);
        }
    }
    static void EnsureMeshes()
    {
        if(orb)return;
        orb=new Mesh{name="SpiritArraySharedOrb",vertices=new[]{Vector3.up,Vector3.down,Vector3.left,Vector3.right,Vector3.forward,Vector3.back},triangles=new[]{0,4,3,0,3,5,0,5,2,0,2,4,1,3,4,1,5,3,1,2,5,1,4,2},uv=new[]{Vector2.one*.5f,Vector2.one*.5f,Vector2.one*.5f,Vector2.one*.5f,Vector2.one*.5f,Vector2.one*.5f},colors=new[]{Color.white,Color.white,Color.white,Color.white,Color.white,Color.white}};
    }
}

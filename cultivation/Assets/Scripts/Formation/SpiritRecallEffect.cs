using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Scatter the spirit's silhouette into motes, then draw them back to its owner.</summary>
public class SpiritRecallEffect : MonoBehaviour
{
    Transform owner;
    ParticleSystem system;
    ParticleSystem.Particle[] particles;
    Vector3[] origins;
    float[] delays, sizes;
    float age, startedAt;
    Material material;
    Texture2D texture;

    public static void Play(GameObject spirit, Transform owner)
    {
        if (spirit == null || owner == null) return;
        var points = new List<Vector3>();
        foreach (var renderer in spirit.GetComponentsInChildren<Renderer>(true)) {
            Mesh mesh = null; bool baked = false;
            if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null) {
                mesh = new Mesh(); skin.BakeMesh(mesh); baked = true;
            } else if (renderer is MeshRenderer) {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null && filter.sharedMesh.isReadable) mesh = filter.sharedMesh;
            }
            if (mesh == null) continue;
            var vertices = mesh.vertices; int step = Mathf.Max(1,vertices.Length / 180);
            for (int i=0; i<vertices.Length && points.Count<480; i+=step)
                points.Add(renderer.transform.TransformPoint(vertices[i]));
            if (baked) Destroy(mesh);
        }
        if (points.Count == 0) {
            var bounds = new Bounds(spirit.transform.position+Vector3.up*.5f,Vector3.one);
            foreach(var renderer in spirit.GetComponentsInChildren<Renderer>()) if(!(renderer is ParticleSystemRenderer)) bounds.Encapsulate(renderer.bounds);
            for(int i=0;i<160;i++) points.Add(bounds.center+Vector3.Scale(Random.insideUnitSphere,bounds.extents));
        }
        var go = new GameObject("真灵粒子回收");
        go.AddComponent<SpiritRecallEffect>().Initialize(owner,points.ToArray());
    }

    void Initialize(Transform target, Vector3[] points)
    {
        owner=target; origins=points;
        system=gameObject.AddComponent<ParticleSystem>(); system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=system.main; main.loop=false; main.playOnAwake=false; main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.simulationSpeed=0; main.maxParticles=points.Length;
        var emission=system.emission; emission.enabled=false;
        var shape=system.shape; shape.enabled=false;
        var renderer=system.GetComponent<ParticleSystemRenderer>(); renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
        material=new Material(Shader.Find("Particles/Standard Unlit"));
        texture=new Texture2D(32,32,TextureFormat.RGBA32,false);
        var pixels=new Color[32*32];
        for(int y=0;y<32;y++)for(int x=0;x<32;x++) {
            float r=new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).magnitude;
            pixels[y*32+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-r),2));
        }
        texture.SetPixels(pixels); texture.Apply();
        material.mainTexture=texture; material.SetFloat("_Mode",2);
        material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha); material.SetInt("_DstBlend",(int)BlendMode.One);
        material.SetInt("_ZWrite",0); material.EnableKeyword("_ALPHABLEND_ON"); material.renderQueue=3000;
        renderer.sharedMaterial=material;
        particles=new ParticleSystem.Particle[points.Length]; delays=new float[points.Length]; sizes=new float[points.Length];
        for(int i=0;i<points.Length;i++) { delays[i]=Random.Range(0,.18f); sizes[i]=Random.Range(.035f,.085f); }
        startedAt=Time.realtimeSinceStartup; system.Play(); UpdateParticles();
    }
    void Update()
    {
        age=Time.realtimeSinceStartup-startedAt;
        if(owner==null || age>=1.12f) { Destroy(gameObject); return; }
        UpdateParticles();
    }
    void UpdateParticles()
    {
        var end=owner.position+Vector3.up*.85f;
        for(int i=0;i<particles.Length;i++) {
            float t=Mathf.Clamp01((age-delays[i])/.85f), ease=t*t*(3-2*t), wave=Mathf.Sin(t*Mathf.PI);
            float phase=i*2.39996f;
            var curl=new Vector3(Mathf.Cos(phase+t*6),.45f,Mathf.Sin(phase+t*6))*.2f*wave;
            particles[i].position=Vector3.Lerp(origins[i],end,ease)+curl;
            particles[i].startSize=sizes[i]*(1-.65f*t);
            particles[i].startColor=new Color(.53f,.92f,.85f,(1-t)*.9f);
            particles[i].remainingLifetime=2; particles[i].startLifetime=2;
        }
        system.SetParticles(particles,particles.Length);
    }
    void OnDestroy() { if(material!=null)Destroy(material); if(texture!=null)Destroy(texture); }
}

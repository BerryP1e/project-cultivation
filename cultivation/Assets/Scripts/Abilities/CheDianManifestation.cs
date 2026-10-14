using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>低成本鬼火、头部起始的粒子凝形、整身粒化回收。日常不采样骨架。</summary>
public sealed class CheDianManifestation : IDisposable
{
    struct Mote { public Vector3 start, offset; public int bone; public float age, life; }
    readonly CheDianYeTingDefinition definition;
    readonly Transform head, body;
    readonly Renderer[] skins;
    readonly Material[][] original;
    readonly MaterialPropertyBlock[] blocks;
    readonly List<Material> owned=new List<Material>();
    readonly Transform[] destinations;
    readonly Mote[] motes=new Mote[768];
    readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[768];
    readonly ParticleSystem stream;
    readonly ParticleSystem flame, ember;
    readonly GameObject streamRoot;
    readonly MaterialPropertyBlock ghostBlock=new MaterialPropertyBlock();
    float elapsed, progress, dissolve, clock;
    bool retracting;
    int count;
    public GameObject Ghost { get; }
    public bool Hidden => retracting&&dissolve>=1;
    public float RevealProgress => progress;
    public int MoteCount => count;
    public CheDianManifestation(GameObject dragon,CheDianYeTingDefinition d,Transform head)
    {
        definition=d;this.head=head;
        var transforms=dragon.GetComponentsInChildren<Transform>(true);
        foreach(var t in transforms)if(t.name=="Bip001")body=t;
        destinations=dragon.GetComponentInChildren<SkinnedMeshRenderer>(true).bones;
        destinations=(Transform[])destinations.Clone();
        Array.Sort(destinations,(a,b)=>BoneStage(a).CompareTo(BoneStage(b)));
        skins=dragon.GetComponentsInChildren<Renderer>(true);original=new Material[skins.Length][];blocks=new MaterialPropertyBlock[skins.Length];
        var shader=Resources.Load<Shader>("Abilities/CheDianYeTing/CheDianManifest");
        for(int i=0;i<skins.Length;i++)
        {
            original[i]=skins[i].sharedMaterials;blocks[i]=new MaterialPropertyBlock();var mats=new Material[original[i].Length];
            for(int n=0;n<mats.Length;n++){var source=original[i][n];var m=new Material(shader);if(source&&source.HasProperty("_MainTex"))m.mainTexture=source.mainTexture;if(source&&source.HasProperty("_Color"))m.color=source.color;mats[n]=m;owned.Add(m);}skins[i].sharedMaterials=mats;
        }
        Ghost=new GameObject("掣电烨霆_随行鬼火");Ghost.layer=dragon.layer;Ghost.transform.SetParent(dragon.transform.parent,true);
        flame=System("幽蓝鬼焰",Ghost.transform,d.BreathPlumeMaterial,.8f,25,new Color(.12f,.32f,1,.5f),true);
        ember=System("鬼火星屑",Ghost.transform,d.BreathMaterial,.035f,45,new Color(.45f,.85f,1,.8f),true);
        streamRoot=new GameObject("掣电烨霆_凝形与回收粒子");streamRoot.layer=dragon.layer;streamRoot.transform.SetParent(dragon.transform.parent,true);
        stream=System("凝形星屑",streamRoot.transform,d.BreathMaterial,.045f,0,new Color(.5f,.85f,1,1),false);
        Ghost.transform.position=dragon.transform.position+Vector3.up*1.4f;
    }
    ParticleSystem System(string name,Transform parent,Material material,float size,float rate,Color color,bool automatic)
    {
        var go=new GameObject(name);go.layer=parent.gameObject.layer;go.transform.SetParent(parent,false);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;main.loop=true;main.playOnAwake=false;main.maxParticles=768;main.simulationSpace=automatic?ParticleSystemSimulationSpace.Local:ParticleSystemSimulationSpace.World;main.useUnscaledTime=parent.gameObject.layer==31;main.startLifetime=.9f;main.startSize=new ParticleSystem.MinMaxCurve(size*.6f,size*1.4f);main.startSpeed=automatic?.25f:0;main.startColor=color;
        var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.18f;
        var emission=ps.emission;emission.rateOverTime=rate;
        var noise=ps.noise;noise.enabled=automatic;noise.strength=.25f;noise.frequency=.8f;
        var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;
        ps.Play(false);return ps;
    }
    public void ShowGhost(bool show){Ghost.SetActive(show);if(show){flame.Play(false);ember.Play(false);}else{flame.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);ember.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);}}
    public void Reveal(bool fromGhost)
    {
        if(retracting)progress=Mathf.Min(progress,1-dissolve);
        retracting=false;elapsed=0;dissolve=0;if(fromGhost)progress=0;
        if(fromGhost||progress<1)Burst(false);ShowGhost(false);
    }
    public void Dissolve(){if(retracting)return;retracting=true;elapsed=0;Burst(true);}
    // Anatomical order survives wing motion and a curled tail, unlike a world-space plane.
    public static float BoneStage(Transform bone)
    {
        if(!bone)return .48f;
        string name=bone.name;
        if(name.Contains("Tail")){int depth=0;for(var t=bone;t&&t.name.Contains("Tail");t=t.parent)depth++;return Mathf.Min(.97f,.68f+depth*.05f);}
        if(name.Contains("Head"))return .05f;if(name.Contains("Neck"))return .12f;
        if(name.Contains("Spine2"))return .27f;if(name.Contains("Spine1"))return .38f;
        if(name.Contains("UpperArm"))return .38f;if(name.Contains("Forearm"))return .52f;
        if(name.Contains("Finger")||name.EndsWith(" Hand"))return .65f;
        if(name.Contains("Thigh"))return .58f;if(name.Contains("Calf"))return .70f;
        if(name.Contains("Foot")||name.Contains("Toe"))return .82f;
        return .48f;
    }
    public void HideImmediately(){progress=0;dissolve=1;count=0;stream.Clear();ShowGhost(true);}
    void Burst(bool inward)
    {
        count=motes.Length;
        for(int i=0;i<count;i++)
        {
            int bone=i%Mathf.Max(1,destinations.Length);var offset=UnityEngine.Random.insideUnitSphere*UnityEngine.Random.Range(.08f,.5f);
            var origin=inward?(destinations.Length>0?destinations[bone].position:head.position)+offset:Ghost.transform.position+UnityEngine.Random.insideUnitSphere*.45f;
            float stage=destinations.Length>0?BoneStage(destinations[bone]):.05f;
            motes[i]=new Mote{start=origin,offset=offset,bone=bone,age=-(inward?(1-stage)*definition.ReturnToGhostSeconds*.75f:stage*definition.ManifestSeconds*.6f),life=inward?UnityEngine.Random.Range(.7f,1.1f)*definition.ReturnToGhostSeconds/3.2f:UnityEngine.Random.Range(.4f,.85f)};
        }
    }
    public void Tick(float dt,Vector3 ghostPosition)
    {
        clock+=dt;elapsed+=dt;Ghost.transform.position=ghostPosition+Vector3.up*(Mathf.Sin(clock*1.8f)*.12f);
        if(retracting)dissolve=Mathf.Clamp01(elapsed/definition.ReturnToGhostSeconds);else progress=Mathf.Clamp01(progress+dt/Mathf.Max(.1f,definition.ManifestSeconds));
        if(skins.Length>0&&skins[0].gameObject.activeInHierarchy)
        {
            var backward=body?(body.position-head.position).normalized:-head.forward;float span=body?Mathf.Max(2,Vector3.Distance(body.position,head.position)*1.8f):5;
            for(int i=0;i<skins.Length;i++)if(skins[i]){var b=blocks[i];b.Clear();b.SetVector("_HeadWS",head.position);b.SetVector("_BackWS",new Vector4(backward.x,backward.y,backward.z,span));b.SetFloat("_Manifest",progress);b.SetFloat("_Dissolve",dissolve);skins[i].SetPropertyBlock(b);}
        }
        ghostBlock.Clear();ghostBlock.SetFloat("_Phase",clock);flame.GetComponent<Renderer>().SetPropertyBlock(ghostBlock);
        if(count==0)return;
        int alive=0,visible=0;
        for(int i=0;i<count;i++)
        {
            var mote=motes[i];mote.age+=dt;if(mote.age>=mote.life)continue;
            if(mote.age<0){if(retracting&&destinations.Length>0)mote.start=destinations[mote.bone].position+mote.offset;motes[alive++]=mote;continue;}
            float u=mote.age/mote.life;
            var target=retracting?Ghost.transform.position:(destinations.Length>0?destinations[mote.bone].position:head.position)+mote.offset;
            var outward=(mote.start-Ghost.transform.position).normalized*Mathf.Sin(u*Mathf.PI)*.6f;
            var p=Vector3.Lerp(mote.start,target,Mathf.SmoothStep(0,1,u))+outward*(retracting?.2f:1);
            particles[visible++]=new ParticleSystem.Particle{position=p,startLifetime=2,remainingLifetime=1,startSize=.025f+.07f*Mathf.Sin(Mathf.PI*u),startColor=new Color(.3f,.65f,1,Mathf.Sin(Mathf.PI*u))};
            motes[alive]=mote;alive++;
        }
        count=alive;stream.SetParticles(particles,visible);
    }
    public void Dispose(){if(Ghost)UnityEngine.Object.Destroy(Ghost);if(streamRoot)UnityEngine.Object.Destroy(streamRoot);for(int i=0;i<skins.Length;i++)if(skins[i]){skins[i].sharedMaterials=original[i];skins[i].SetPropertyBlock(null);}foreach(var material in owned)UnityEngine.Object.Destroy(material);owned.Clear();}
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Layered electrical plume; core fades first, the outer mist bridges the next spray.</summary>
public sealed class CheDianBreathVisual : IDisposable
{
    struct Impact { public GameObject root; public float born; }
    public GameObject Root { get; }
    public bool Dissolving { get; private set; }
    public bool Ended => Dissolving && dissolveTime >= definition.BreathDissolveSeconds;
    public float DissolveAmount => Dissolving ? Mathf.Clamp01(dissolveTime / Mathf.Max(.01f, definition.BreathDissolveSeconds)) : 0;
    public int StrandCount => activeStrands;
    public int Tier { get; private set; } = 2;
    public float HeadFlameGain { get; private set; } = 1;
    readonly CheDianYeTingDefinition definition;
    readonly LineRenderer core, halo, distortion;
    readonly LineRenderer[] strands = new LineRenderer[48];
    readonly ParticleSystem charge, sparks, plume, smoke, backFlame, backSparks;
    readonly ParticleSystem[] systems;
    readonly Renderer[] renderers;
    readonly MaterialPropertyBlock[] blocks;
    readonly Vector3[] curve = new Vector3[32];
    readonly List<Impact> impacts = new List<Impact>();
    float clock, length, visibleLength, dissolveTime, cloudBudget, flameBudget, lastHit = -10;
    Vector3 lastHitPoint;
    bool fired;
    int activeStrands=28;
    float density=1, spread=1;
    public CheDianBreathVisual(CheDianYeTingDefinition d)
    {
        definition = d; Root = new GameObject("掣电烨霆_雷霆吐息"); Root.layer = 2;
        core = Beam("稳定光芯", .45f, new Color(.7f,.92f,1,.95f), d.BreathMaterial);
        halo = Beam("电焰光晕", 1.65f, new Color(.16f,.42f,1,.24f), d.BreathMaterial);
        distortion = Beam("热流扭曲", 3.5f, new Color(1,1,1,.65f), d.BreathDistortionMaterial);
        for (int i=0;i<strands.Length;i++)
        {
            bool lightning = i>=18;
            strands[i] = Beam(lightning?"弥漫雷霆":"游动电流",lightning?.035f:.065f,
                lightning?new Color(.72f,.68f,1,.85f):new Color(.22f,.57f,1,.6f),d.BreathMaterial);
            strands[i].positionCount = curve.Length;
        }
        charge = Particles("口部聚电",d.BreathMaterial,.065f,.4f,0,75,new Color(.5f,.65f,1,.8f),false);
        sparks = Particles("高速电芒",d.BreathMaterial,.035f,.22f,70,680,new Color(.55f,.85f,1,.9f),true);
        plume = Particles("弥漫外焰",d.BreathPlumeMaterial,1,.65f,0,0,new Color(.2f,.12f,.8f,.4f),false);
        smoke = Particles("游动雷雾",d.BreathPlumeMaterial,1.4f,1,0,0,new Color(.17f,.11f,.38f,.17f),false);
        backFlame = Particles("反冲异色外焰",d.BreathPlumeMaterial,.7f,.6f,4,85,new Color(1,.045f,.12f,.5f),false);
        backFlame.transform.localRotation=Quaternion.Euler(0,180,0);
        backSparks = Particles("逆向反冲星火",d.BreathMaterial,.045f,.4f,12,180,new Color(1,.18f,.08f,.85f),true);
        backSparks.transform.localRotation=Quaternion.Euler(0,180,0);
        var shape=backFlame.shape;shape.angle=27;shape.radius=.22f;
        shape=backSparks.shape;shape.angle=35;shape.radius=.18f;
        foreach(var ps in new[]{plume,smoke,backFlame}){var noise=ps.noise;noise.enabled=true;noise.strength=new ParticleSystem.MinMaxCurve(.35f);noise.frequency=.65f;noise.scrollSpeed=.7f;noise.octaveCount=2;}
        systems=new[]{charge,sparks,plume,smoke,backFlame,backSparks};
        renderers=Root.GetComponentsInChildren<Renderer>(); blocks=new MaterialPropertyBlock[renderers.Length];
        for(int i=0;i<blocks.Length;i++)blocks[i]=new MaterialPropertyBlock();
        HideLines();
    }
    public void SetTier(int tier)
    {
        Tier=Mathf.Clamp(tier,1,4);
        density=Tier==1?.70f:Tier==2?1:Tier==3?1.48f:2.05f;
        spread=Tier==1?.85f:Tier==2?1:Tier==3?1.14f:1.30f;
        HeadFlameGain=Tier==1?.65f:Tier==2?1:Tier==3?1.65f:2.45f;
        activeStrands=Tier==1?18:Tier==2?28:Tier==3?36:48;
        core.widthMultiplier=.45f*spread;halo.widthMultiplier=1.65f*spread;distortion.widthMultiplier=3.5f*spread;
        var emission=sparks.emission;emission.rateOverTime=680*density;
        emission=backFlame.emission;emission.rateOverTime=85*HeadFlameGain;
        emission=backSparks.emission;emission.rateOverTime=180*HeadFlameGain;
        emission=charge.emission;emission.rateOverTime=75*Mathf.Sqrt(HeadFlameGain);
        var main=backFlame.main;float size=.7f*Mathf.Sqrt(HeadFlameGain);main.startSize=new ParticleSystem.MinMaxCurve(size*.5f,size*1.4f);
        main=backSparks.main;main.startSpeed=12*Mathf.Sqrt(HeadFlameGain);main.startSize=new ParticleSystem.MinMaxCurve(.0225f,.063f*Mathf.Sqrt(HeadFlameGain));
        var shape=backFlame.shape;shape.angle=27+Mathf.Max(0,Tier-2)*5;shape.radius=.22f*Mathf.Sqrt(HeadFlameGain);
        shape=backSparks.shape;shape.angle=35+Mathf.Max(0,Tier-2)*5;shape.radius=.18f*Mathf.Sqrt(HeadFlameGain);
        for(int i=activeStrands;i<strands.Length;i++)strands[i].enabled=false;
    }
    LineRenderer Beam(string name,float width,Color color,Material material)
    {
        var node=new GameObject(name);node.layer=Root.layer;node.transform.SetParent(Root.transform,false);
        var line=node.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=false;line.positionCount=2;
        line.widthMultiplier=width;line.startColor=line.endColor=color;line.numCapVertices=5;
        line.widthCurve=new AnimationCurve(new Keyframe(0,.35f),new Keyframe(.15f,.85f),new Keyframe(.8f,1),new Keyframe(1,.55f));
        line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return line;
    }
    ParticleSystem Particles(string name,Material material,float size,float life,float speed,float rate,Color color,bool stretch)
    {
        var node=new GameObject(name);node.layer=Root.layer;node.transform.SetParent(Root.transform,false);
        var ps=node.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var m=ps.main;m.loop=true;m.playOnAwake=false;m.simulationSpace=ParticleSystemSimulationSpace.Local;m.maxParticles=1800;
        m.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
        m.startLifetime=new ParticleSystem.MinMaxCurve(life*.7f,life);m.startSpeed=speed;m.startSize=new ParticleSystem.MinMaxCurve(size*.5f,size*1.4f);m.startColor=color;
        var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.radius=.12f;shape.angle=2.5f;
        var emission=ps.emission;emission.rateOverTime=rate;
        var alpha=ps.colorOverLifetime;alpha.enabled=true;var gradient=new Gradient();
        gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.8f,.5f),new GradientAlphaKey(0,1)});alpha.color=gradient;
        var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.renderMode=stretch?ParticleSystemRenderMode.Stretch:ParticleSystemRenderMode.Billboard;renderer.velocityScale=.018f;renderer.lengthScale=3;
        return ps;
    }
    void HideLines(){core.enabled=halo.enabled=distortion.enabled=false;foreach(var line in strands)line.enabled=false;}
    public void Tick(float dt)
    {
        clock+=dt;
        if(Dissolving) dissolveTime+=dt;
        for(int i=0;i<renderers.Length;i++)if(renderers[i])
        {
            var r=renderers[i];var block=blocks[i];float end=LayerEnd(r);
            float progress=Dissolving?Mathf.SmoothStep(0,1,Mathf.Clamp01(dissolveTime/Mathf.Max(.01f,end))):0;
            block.Clear();block.SetFloat("_Phase",clock);block.SetFloat("_Dissolve",progress);r.SetPropertyBlock(block);
        }
        for(int i=impacts.Count-1;i>=0;i--)if(clock-impacts[i].born>1.1f){if(impacts[i].root)UnityEngine.Object.Destroy(impacts[i].root);impacts.RemoveAt(i);}
        if(Dissolving)
        {
            Draw(0,visibleLength);
            // Only the outer mist lingers as a soft bridge; no travelling axial cutoff.
            cloudBudget+=dt*110*density*(1-DissolveAmount);EmitCloud(smoke,ref cloudBudget,false);
            flameBudget+=dt*90*density*Mathf.Clamp01(1-dissolveTime/Mathf.Max(.01f,definition.BreathDissolveSeconds*.55f));EmitCloud(plume,ref flameBudget,true);
            if(Ended){HideLines();foreach(var ps in systems)ps.Clear();}
            return;
        }
        if(!fired)return;
        cloudBudget+=dt*190*density;flameBudget+=dt*420*density;
        EmitCloud(smoke,ref cloudBudget,false);EmitCloud(plume,ref flameBudget,true);
    }
    float LayerEnd(Renderer r)
    {
        float span=definition.BreathDissolveSeconds;
        if(r==core)return span*.06f;
        if(r==halo)return span*.14f;
        if(r==distortion)return span*.2f;
        if(r.gameObject==smoke.gameObject)return span;
        if(r.gameObject==plume.gameObject)return span*.55f;
        return span*.32f;
    }
    void EmitCloud(ParticleSystem ps,ref float budget,bool flame)
    {
        while(budget>=1){budget--;float u=UnityEngine.Random.value;float angle=UnityEngine.Random.value*Mathf.PI*2;
            float radius=Mathf.Lerp(.18f,flame?1.15f:1.65f,Mathf.Sqrt(u))*UnityEngine.Random.Range(.5f,1)*spread;
            var p=new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,u*visibleLength);
            float lifetime=flame?.6f:1;float axial=Mathf.Min(flame?12:3,Mathf.Max(0,visibleLength-p.z)/lifetime*.75f);
            var emit=new ParticleSystem.EmitParams{position=p,velocity=new Vector3(p.x,p.y,axial),startSize=UnityEngine.Random.Range(flame?1.0f:.9f,flame?2.3f:2.5f)*spread,startLifetime=lifetime};ps.Emit(emit,1);
        }
    }
    public void ShowCharge(Vector3 mouth,float amount)
    {
        if(Dissolving){Dissolving=false;dissolveTime=0;fired=false;HideLines();foreach(var p in systems)p.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);}
        Root.transform.position=mouth;var m=charge.main;m.startSize=Mathf.Lerp(.04f,.2f,amount)*Mathf.Sqrt(HeadFlameGain);if(!charge.isPlaying)charge.Play();
    }
    public void ShowBeam(Vector3 from,Vector3 to,float intensity)
    {
        var delta=to-from;length=visibleLength=delta.magnitude;
        Root.transform.SetPositionAndRotation(from,Quaternion.LookRotation(delta.sqrMagnitude>.001f?delta:Vector3.forward));
        fired=true;charge.Stop(false,ParticleSystemStopBehavior.StopEmitting);
        for(int i=1;i<systems.Length;i++)if(!systems[i].isPlaying)systems[i].Play();
        var m=sparks.main;m.startSpeed=Mathf.Max(8,length/.22f);
        Draw(0,visibleLength);
    }
    void Draw(float first,float last)
    {
        if(last-first<.03f){HideLines();return;}
        core.enabled=halo.enabled=distortion.enabled=true;
        core.SetPosition(0,Vector3.forward*first);core.SetPosition(1,Vector3.forward*last);
        halo.SetPosition(0,Vector3.forward*first);halo.SetPosition(1,Vector3.forward*last);
        distortion.SetPosition(0,Vector3.forward*first);distortion.SetPosition(1,Vector3.forward*last);
        for(int thread=0;thread<activeStrands;thread++)
        {
            bool lightning=thread>=18;var line=strands[thread];line.enabled=true;
            float phase=thread*2.39996f;float speed=lightning?14:5.5f;
            float head=Mathf.Repeat(clock*(.7f+thread*.035f)+thread*.271f,1.4f);
            float extent=lightning?.26f:.45f;
            float start=Mathf.Max(first,(head-extent)*last),end=Mathf.Min(last,head*last);
            if(end-start<.02f){line.enabled=false;continue;}
            for(int i=0;i<curve.Length;i++)
            {
                float u=i/(float)(curve.Length-1),z=Mathf.Lerp(start,end,u),v=z/Mathf.Max(.01f,length);
                float envelope=Mathf.Sin(Mathf.PI*u)*Mathf.Lerp(.2f,lightning?1.7f:1.1f,Mathf.Sqrt(v))*spread;
                float roll=phase+u*4-clock*speed;
                float jag=lightning?(Mathf.PerlinNoise(i*.71f+thread,Mathf.Floor(clock*13))-.5f)*.65f:0;
                float x=lightning?(Mathf.PerlinNoise(u*5+phase,clock*4)-.5f)*2:Mathf.Cos(roll);
                float y=lightning?(Mathf.PerlinNoise(u*4+17+phase,clock*3)-.5f)*2:Mathf.Sin(roll*1.13f);
                curve[i]=new Vector3(x*envelope+jag,y*envelope-jag,z);
            }
            line.SetPositions(curve);
        }
    }
    public void BeginDissolve()
    {
        if(Dissolving)return;Dissolving=true;dissolveTime=0;fired=false;
        foreach(var ps in systems)ps.Stop(false,ParticleSystemStopBehavior.StopEmitting);
        smoke.Play(false);plume.Play(false); // Zero automatic emission; animate the fading manual outer plume.
    }
    public void Fade(float unused) => BeginDissolve();
    public void Hit(Vector3 point)
    {
        if(!definition.BreathImpact||clock-lastHit<.2f&&Vector3.Distance(point,lastHitPoint)<1||impacts.Count>=8)return;
        lastHit=clock;lastHitPoint=point;
        // World-fixed contact: moving the muzzle must not drag yesterday's impact along with it.
        var impact=UnityEngine.Object.Instantiate(definition.BreathImpact,point,Quaternion.identity,Root.transform.parent);impact.transform.localScale=Vector3.one*.9f*spread;
        foreach(var t in impact.GetComponentsInChildren<Transform>())t.gameObject.layer=Root.layer;
        foreach(var light in impact.GetComponentsInChildren<Light>()){light.intensity*=.6f;if(Root.layer==31)light.cullingMask=1<<31;}
        foreach(var ps in impact.GetComponentsInChildren<ParticleSystem>()){var m=ps.main;m.scalingMode=ParticleSystemScalingMode.Hierarchy;m.useUnscaledTime=Root.layer==31;m.stopAction=ParticleSystemStopAction.None;}
        impacts.Add(new Impact{root=impact,born=clock});
    }
    public void Dispose(){foreach(var impact in impacts)if(impact.root)UnityEngine.Object.Destroy(impact.root);impacts.Clear();if(Root)UnityEngine.Object.Destroy(Root);}
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>适配ExtremeFX的装饰生成/淡出，避免改第三方资源或全局物理配置。</summary>
public sealed class ExtremeVfxAdapter : MonoBehaviour
{
    sealed class Ending {public GameObject node;public float end;public bool stopped;public Renderer[] renderers;public Color[] colors;public Light light;public float intensity,decay;}
    sealed class Spawn {public EFX_Spawner source;public float next;public int count;}
    sealed class Atlas {public Renderer renderer;public EFX_AnimationUV source;}
    sealed class Scroll {public Renderer renderer;public EFX_AnimationUV2 source;public Vector4 original;}
    sealed class Slash {public Transform visual;public float maxScale;}
    readonly List<Ending> endings=new List<Ending>();
    readonly List<Spawn> spawns=new List<Spawn>();
    readonly List<Atlas> atlases=new List<Atlas>();
    readonly List<Scroll> scrolls=new List<Scroll>();
    readonly List<Slash> slashes=new List<Slash>();
    MaterialPropertyBlock block;
    float born,visualScale;int depth;Transform owner;
    public float 散布半径;
    public void 初始化(int 深度,float 表现缩放=1f,Transform 生命周期根=null)
    {
        block=new MaterialPropertyBlock();depth=深度;born=Time.time;visualScale=Mathf.Max(.001f,表现缩放);owner=生命周期根?生命周期根:transform;
        // 装饰碎片不参与角色碰撞；伤害与命中判定仍由战斗系统负责。
        foreach(var collider in GetComponentsInChildren<Collider>(true))collider.enabled=false;
        var explosions=GetComponentsInChildren<EFX_ExplosionObject>(true);
        var generators=GetComponentsInChildren<EFX_Spawner>(true);
        var animations=GetComponentsInChildren<EFX_AnimationUV>(true);
        var scrolling=GetComponentsInChildren<EFX_AnimationUV2>(true);
        foreach(var source in GetComponentsInChildren<EFX_Slash>(true)){
            source.enabled=false;var mesh=source.GetComponent<MeshFilter>();var renderer=source.GetComponent<MeshRenderer>();if(!mesh||!renderer)continue;
            var visual=new GameObject("SlashVisual");visual.transform.SetParent(source.transform,false);visual.AddComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;visual.AddComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;renderer.enabled=false;
            slashes.Add(new Slash{visual=visual.transform,maxScale=3f/Mathf.Max(.01f,mesh.sharedMesh.bounds.size.x*source.transform.lossyScale.x)});
        }
        foreach(var ps in GetComponentsInChildren<ParticleSystem>(true)){
            // 原包Shape只缩放发射形状。Flash等节点的0.0001缩放不是粒径倍率。
            // 保留源模式，单独缩放粒径/空间速度，时长、发射曲线和模拟速度不变。
            var m=ps.main;
            if(m.scalingMode==ParticleSystemScalingMode.Shape){
                if(m.startSize3D){m.startSizeXMultiplier*=visualScale;m.startSizeYMultiplier*=visualScale;m.startSizeZMultiplier*=visualScale;}
                else m.startSizeMultiplier*=visualScale;
                m.startSpeedMultiplier*=visualScale;m.gravityModifierMultiplier*=visualScale;
                var velocity=ps.velocityOverLifetime;if(velocity.enabled){velocity.xMultiplier*=visualScale;velocity.yMultiplier*=visualScale;velocity.zMultiplier*=visualScale;velocity.radialMultiplier*=visualScale;}
                var force=ps.forceOverLifetime;if(force.enabled){force.xMultiplier*=visualScale;force.yMultiplier*=visualScale;force.zMultiplier*=visualScale;}
            }
        }
        foreach(var trail in GetComponentsInChildren<TrailRenderer>(true))trail.widthMultiplier*=visualScale;
        foreach(var motion in GetComponentsInChildren<EFX_Randommove>(true)){motion.SpeedMin*=visualScale;motion.SpeedMax*=visualScale;}
        foreach(var wind in GetComponentsInChildren<EFX_Wind>(true)){wind.speed*=visualScale;wind.speedRedirect*=visualScale;}
        foreach(var source in GetComponentsInChildren<EFX_ParticleSetting>(true)){
            source.enabled=false;
            source.transform.position+=source.PositionOffset*visualScale;
            if(source.RandomRotation)source.transform.localRotation=source.transform==transform?source.transform.localRotation*Quaternion.AngleAxis(Random.Range(-45f,45f),Vector3.forward):Random.rotation;
            var rs=source.GetComponentsInChildren<Renderer>(true);
            var colors=new Color[rs.Length];for(int i=0;i<rs.Length;i++)colors[i]=Tint(rs[i]);
            var light=source.GetComponent<Light>();if(light)light.range*=visualScale;
            endings.Add(new Ending{node=source.gameObject,end=Mathf.Max(.05f,source.LifeTime),renderers=rs,colors=colors,light=light,intensity=light?light.intensity:0,decay=source.LightIntensityMult});
        }
        foreach(var source in GetComponentsInChildren<EFX_Explosion>(true))source.enabled=false;
        foreach(var source in explosions){
            source.enabled=false;
            int count=source.random?Random.Range(1,Mathf.Max(2,source.Num)):source.Num;
            for(int i=0;i<count&&i<32;i++){
                var random=new Vector3(Random.Range(-source.postitionoffset,source.postitionoffset),Random.Range(-source.postitionoffset,source.postitionoffset),Random.Range(-source.postitionoffset,source.postitionoffset))*.1f;
                var go=Child(source.Objcet,source.transform,random*visualScale,Random.rotation,source.Scale>0?(Vector3?)Vector3.one*Random.Range(source.ScaleMin,source.Scale):null);
                if(!go)continue;
                var body=go.GetComponent<Rigidbody>();if(body){body.AddForce(new Vector3(Random.Range(-source.Force.x,source.Force.x),Random.Range(-source.Force.y,source.Force.y),Random.Range(-source.Force.z,source.Force.z))*visualScale);}
                Destroy(go,Mathf.Max(.001f,source.LifeTimeObject));
            }
        }
        foreach(var source in generators){source.enabled=false;spawns.Add(new Spawn{source=source,next=Mathf.Max(0,source.SpawnRate)});}
        foreach(var source in animations){source.enabled=false;atlases.Add(new Atlas{source=source,renderer=source.GetComponent<Renderer>()});}
        foreach(var source in scrolling){source.enabled=false;var r=source.GetComponent<Renderer>();if(r&&r.sharedMaterial)scrolls.Add(new Scroll{source=source,renderer=r,original=r.sharedMaterial.GetVector("_MainTex_ST")});}
    }
    static Color Tint(Renderer r){var m=r.sharedMaterial;return !m?Color.white:m.HasProperty("_TintColor")?m.GetColor("_TintColor"):m.HasProperty("_Color")?m.color:Color.white;}
    GameObject Child(GameObject prefab,Transform parent,Vector3 offset,Quaternion rotation,Vector3? nativeScale=null)
    {
        if(!prefab||depth>=6)return null;
        // 原脚本使用世界偏移和绝对缩放，不继承发射器的小缩放/随机旋转。
        var go=Instantiate(prefab,parent.position+offset,rotation);go.transform.localScale=(nativeScale??prefab.transform.localScale)*visualScale;
        // 挂到静止的生命周期根，避免移动雷光携带其子雷光产生双倍位移。
        go.transform.SetParent(owner,true);go.AddComponent<ExtremeVfxAdapter>().初始化(depth+1,visualScale,owner);return go;
    }
    void Update()
    {
        float age=Time.time-born;
        foreach(var slash in slashes){if(!slash.visual)continue;float grow=Mathf.SmoothStep(0,1,age/.1f),thin=1-Mathf.SmoothStep(0,1,age/.45f);slash.visual.localScale=new Vector3(Mathf.Lerp(1,slash.maxScale,grow),Mathf.Max(.015f,thin),1);}
        foreach(var spawn in spawns){var s=spawn.source;if(!s||spawn.count>=s.LimitObject||age<spawn.next)continue;
            var offset=(new Vector3(Random.Range(-s.PositionRandomSize.x,s.PositionRandomSize.x),Random.Range(-s.PositionRandomSize.y,s.PositionRandomSize.y),Random.Range(-s.PositionRandomSize.z,s.PositionRandomSize.z))+s.PositionOffset)*visualScale;
            if(散布半径>0){var disk=Random.insideUnitCircle*散布半径*visualScale;offset=new Vector3(disk.x,offset.y,disk.y);}
            var go=Child(s.ObjectSpawn,s.transform,offset,s.transform.rotation);if(go)Destroy(go,Mathf.Max(.001f,s.LifeTimeObject));spawn.count++;spawn.next=age+Mathf.Max(0,s.SpawnRate);
        }
        foreach(var atlas in atlases){var s=atlas.source;var r=atlas.renderer;if(!s||!r||!s.play)continue;int total=Mathf.Max(1,s.uvAnimationTileX*s.uvAnimationTileY),frame=(int)(age*s.framesPerSecond);
            if(!s.loop&&frame>=total-1){frame=total-1;if(s.Hidewhenstopplaying)r.enabled=false;}else frame%=total;float x=1f/Mathf.Max(1,s.uvAnimationTileX),y=1f/Mathf.Max(1,s.uvAnimationTileY);
            r.GetPropertyBlock(block);block.SetVector("_MainTex_ST",new Vector4(x,y,(frame%s.uvAnimationTileX)*x,1-y-(frame/s.uvAnimationTileX)*y));r.SetPropertyBlock(block);
        }
        foreach(var scroll in scrolls){if(!scroll.source||!scroll.renderer)continue;var st=scroll.original;st.z=Mathf.Repeat(st.z+age*scroll.source.speed,1);scroll.renderer.GetPropertyBlock(block);block.SetVector("_MainTex_ST",st);scroll.renderer.SetPropertyBlock(block);}
        foreach(var ending in endings){if(ending.light)ending.light.intensity=Mathf.Max(0,ending.intensity+ending.decay*age);float fade=Mathf.Min(.12f,ending.end*.2f);if(!ending.node||age<ending.end-fade)continue;
            if(!ending.stopped){ending.stopped=true;foreach(var ps in ending.node.GetComponentsInChildren<ParticleSystem>(true))ps.Stop(false,ParticleSystemStopBehavior.StopEmitting);}
            float alpha=1-Mathf.SmoothStep(0,1,(age-ending.end+fade)/fade);
            for(int i=0;i<ending.renderers.Length;i++){var r=ending.renderers[i];if(!r)continue;r.GetPropertyBlock(block);var color=ending.colors[i]*alpha;block.SetColor("_TintColor",color);block.SetColor("_Color",color);r.SetPropertyBlock(block);}
            if(age>=ending.end)Destroy(ending.node);
        }
    }
}

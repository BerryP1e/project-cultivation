using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>剑形粒子：升空、汇聚、消散均按独立起终点插值。</summary>
public class QingshanSwordParticles : MonoBehaviour
{
    Vector3[] origins,destinations;
    ParticleSystem.Particle[] points;
    ParticleSystem system;
    Material material;Texture2D texture;
    float age,duration;bool fadeOut;
    static Vector3[] shapeVertices;
    Transform trackedSword;Vector3[] trackedPoints;float trackedScale;
    static Vector3[] ShapeVertices { get {
        if(shapeVertices!=null)return shapeVertices;
        var asset=Resources.Load<TextAsset>("法宝/青山剑/青山剑粒子形状");
        if(asset==null)return shapeVertices=new Vector3[0];
        using(var reader=new System.IO.BinaryReader(new System.IO.MemoryStream(asset.bytes))){
            shapeVertices=new Vector3[reader.ReadInt32()];
            for(int i=0;i<shapeVertices.Length;i++)shapeVertices[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
        }return shapeVertices;
    }}
    public void TrackSword(Transform sword,float fullScale){
        trackedSword=sword;trackedScale=fullScale;trackedPoints=new Vector3[destinations.Length];
        for(int i=0;i<trackedPoints.Length;i++)trackedPoints[i]=sword.InverseTransformPoint(destinations[i]);
    }
    public static QingshanSwordParticles Play(Vector3[] starts,Vector3[] ends,float seconds,bool fade){
        var effect=new GameObject("青山剑_灵光粒子").AddComponent<QingshanSwordParticles>();effect.Initialize(starts,ends,seconds,fade);return effect;
    }
    void Initialize(Vector3[] starts,Vector3[] ends,float seconds,bool fade){
        origins=starts;destinations=ends;duration=seconds;fadeOut=fade;
        system=gameObject.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=system.main;main.loop=false;main.playOnAwake=false;main.simulationSpeed=0;main.maxParticles=starts.Length;main.simulationSpace=ParticleSystemSimulationSpace.World;
        var emission=system.emission;emission.enabled=false;var shape=system.shape;shape.enabled=false;
        material=new Material(Shader.Find("Particles/Standard Unlit"));texture=new Texture2D(32,32,TextureFormat.RGBA32,false);
        var colors=new Color[1024];for(int y=0;y<32;y++)for(int x=0;x<32;x++){float r=new Vector2(x-15.5f,y-15.5f).magnitude/15.5f;colors[y*32+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-r),2));}
        texture.SetPixels(colors);texture.Apply();material.mainTexture=texture;material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);material.SetInt("_DstBlend",(int)BlendMode.One);material.SetInt("_ZWrite",0);material.EnableKeyword("_ALPHABLEND_ON");material.renderQueue=3000;
        var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        points=new ParticleSystem.Particle[starts.Length];system.Play();Draw();
    }
    void Update(){if(UiEscRegistry.SceneInputBlocked)return;age+=Time.deltaTime;if(age>=duration){Destroy(gameObject);return;}Draw();}
    void Draw(){float t=Mathf.Clamp01(age/duration);for(int i=0;i<points.Length;i++){
        float p=Mathf.SmoothStep(0,1,t);float angle=i*2.39996f+t*6;var curl=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle*.7f),Mathf.Sin(angle))*.22f*Mathf.Sin(t*Mathf.PI);
        var destination=trackedSword!=null?Matrix4x4.TRS(trackedSword.position,trackedSword.rotation,Vector3.one*trackedScale).MultiplyPoint3x4(trackedPoints[i]):destinations[i];
        points[i].position=Vector3.Lerp(origins[i],destination,p)+curl;points[i].startSize=fadeOut?.065f*(1-.6f*t):.05f+.04f*t;
        float alpha=fadeOut?1-t:Mathf.Min(1,t*5)*(1-Mathf.Clamp01((t-.8f)/.2f));points[i].startColor=new Color(.42f,.85f,1,alpha);
        points[i].startLifetime=2;points[i].remainingLifetime=2;
    }system.SetParticles(points,points.Length);}
    static void Extents(Transform sword,out float min,out float max,out float width){
        min=float.MaxValue;max=float.MinValue;width=0;
        if(ShapeVertices.Length>0){foreach(var p in ShapeVertices){var scaled=Vector3.Scale(p,sword.lossyScale);min=Mathf.Min(min,scaled.z);max=Mathf.Max(max,scaled.z);width=Mathf.Max(width,Mathf.Abs(scaled.x));}return;}
        foreach(var filter in sword.GetComponentsInChildren<MeshFilter>(true)){
            if(filter.sharedMesh==null)continue;var b=filter.sharedMesh.bounds;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2){var world=filter.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)));var local=Quaternion.Inverse(sword.rotation)*(world-sword.position);min=Mathf.Min(min,local.z);max=Mathf.Max(max,local.z);width=Mathf.Max(width,Mathf.Abs(local.x));}
        }
        if(min==float.MaxValue){min=-.6f*sword.lossyScale.x;max=.6f*sword.lossyScale.x;width=.15f*sword.lossyScale.x;}
    }
    public static Vector3 TipLocal(){
        var vertices=ShapeVertices;if(vertices.Length==0)return new Vector3(0,0,.6f);
        float max=float.MinValue;foreach(var point in vertices)max=Mathf.Max(max,point.z);
        Vector3 tip=Vector3.zero;int count=0;foreach(var point in vertices)if(point.z>=max-.015f){tip+=point;count++;}
        tip/=Mathf.Max(1,count);tip.z=max;return tip;
    }
    public static float BladeLength(Transform sword){Extents(sword,out var min,out var max,out var width);return max-min;}
    public static float CenterOffset(Transform sword){Extents(sword,out var min,out var max,out var width);return (min+max)*.5f;}
    public static float TipLength(Transform sword){Extents(sword,out var min,out var max,out var width);return max;}
    public static void SwordPoints(Transform sword,int count,List<Vector3> output){
        if(ShapeVertices.Length>0){for(int i=0;i<count;i++){int index=(int)((i+.5f)*ShapeVertices.Length/count)%ShapeVertices.Length;output.Add(sword.TransformPoint(ShapeVertices[index]));}return;}
        Extents(sword,out var min,out var max,out var width);
        for(int i=0;i<count;i++){
            float t=(i+.5f)/count;float z=Mathf.Lerp(min,max,t);
            float half=t<.22f?width*.2f:t<.28f?width:width*.3f*Mathf.Clamp01((1-t)*3);
            var p=new Vector3(Mathf.Sin(i*2.39996f)*half,Mathf.Cos(i*2.39996f)*width*.08f,z);
            output.Add(sword.position+sword.rotation*p);
        }
    }
    void OnDestroy(){if(material!=null)Destroy(material);if(texture!=null)Destroy(texture);}
}

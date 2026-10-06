using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>世界水平面裁切实际剑身，穿过平面的部分转为粒子；不缩放剩余剑身。</summary>
public class QingshanSwordDissolve : MonoBehaviour
{
    readonly List<Material> materials=new List<Material>();
    Vector3[] samples;bool[] emitted;ParticleSystem system;GameObject cloud;Material particleMaterial;Texture2D texture;
    public float CutHeight{get;private set;}
    public int EmittedCount{get;private set;}
    public void Initialize(){
        var shader=Resources.Load<Shader>("法宝/青山剑/青山剑贯穿消散");
        foreach(var renderer in GetComponentsInChildren<MeshRenderer>(true)){
            var sources=renderer.sharedMaterials;var copies=new Material[sources.Length];
            for(int i=0;i<sources.Length;i++){var mat=new Material(shader);mat.CopyPropertiesFromMaterial(sources[i]);mat.SetFloat("_CutEnabled",0);copies[i]=mat;materials.Add(mat);}renderer.sharedMaterials=copies;
        }
        var points=new List<Vector3>();QingshanSwordParticles.SwordPoints(transform,2400,points);samples=new Vector3[points.Count];emitted=new bool[points.Count];
        for(int i=0;i<points.Count;i++)samples[i]=transform.InverseTransformPoint(points[i]);
        cloud=new GameObject("万剑归宗_贯穿消散粒子");system=cloud.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=system.main;main.loop=false;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=4000;main.startLifetime=1.2f;main.startSpeed=0;main.startSize=.12f;main.gravityModifier=-.025f;
        var emission=system.emission;emission.enabled=false;var shape=system.shape;shape.enabled=false;
        texture=new Texture2D(32,32,TextureFormat.RGBA32,false);var pixels=new Color[1024];
        for(int y=0;y<32;y++)for(int x=0;x<32;x++){float r=new Vector2(x-15.5f,y-15.5f).magnitude/15.5f;pixels[y*32+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-r),2));}texture.SetPixels(pixels);texture.Apply();
        particleMaterial=new Material(Shader.Find("Particles/Standard Unlit"));particleMaterial.mainTexture=texture;particleMaterial.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);particleMaterial.SetInt("_DstBlend",(int)BlendMode.One);particleMaterial.SetInt("_ZWrite",0);particleMaterial.EnableKeyword("_ALPHABLEND_ON");particleMaterial.renderQueue=3000;system.GetComponent<ParticleSystemRenderer>().sharedMaterial=particleMaterial;
        var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(.3f,.8f,1),0),new GradientColorKey(new Color(.15f,.4f,1),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});var color=system.colorOverLifetime;color.enabled=true;color.color=gradient;system.Play();
    }
    public void UpdateCut(float height){
        CutHeight=height;foreach(var material in materials){material.SetFloat("_CutEnabled",1);material.SetFloat("_CutHeight",height);}
        for(int i=0;i<samples.Length;i++){
            if(emitted[i])continue;var p=transform.TransformPoint(samples[i]);if(p.y>height)continue;
            emitted[i]=true;EmittedCount++;p.y=height;
            system.Emit(new ParticleSystem.EmitParams{position=p,velocity=Random.insideUnitSphere*.65f+Vector3.up*.4f,startSize=Random.Range(.09f,.17f),startLifetime=Random.Range(.8f,1.2f),startColor=new Color(.35f,.85f,1)},1);
        }
    }
    void OnDestroy(){foreach(var material in materials)if(material!=null)Destroy(material);if(cloud!=null){system.Stop(false,ParticleSystemStopBehavior.StopEmitting);Destroy(cloud,1.3f);}if(particleMaterial!=null)Destroy(particleMaterial,1.4f);if(texture!=null)Destroy(texture,1.4f);}
}

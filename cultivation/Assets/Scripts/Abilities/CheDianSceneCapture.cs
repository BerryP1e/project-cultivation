using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>按相机保存不透明画面，供吐息折射使用；避开命名 GrabPass 的临时纹理复用。</summary>
internal static class CheDianSceneCapture
{
    sealed class Capture
    {
        public Camera camera;
        public RenderTexture colour;
        public CommandBuffer commands;
        public void Dispose(){if(camera&&commands!=null)camera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha,commands);commands?.Release();if(colour){colour.Release();Object.Destroy(colour);}}
    }
    static readonly HashSet<Renderer> users=new HashSet<Renderer>();
    static readonly Dictionary<Camera,Capture> cameras=new Dictionary<Camera,Capture>();
    static readonly List<Camera> stale=new List<Camera>();
    const string Texture="_CheDianSceneColour",Ready="_CheDianSceneColourReady";
    public static void Acquire(Renderer renderer){if(users.Add(renderer)&&users.Count==1)Camera.onPreRender+=Prepare;}
    public static void Release(Renderer renderer){users.Remove(renderer);if(users.Count==0)Reset();}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset(){Camera.onPreRender-=Prepare;foreach(var capture in cameras.Values)capture.Dispose();cameras.Clear();users.Clear();Shader.SetGlobalFloat(Ready,0);}
    static void Prepare(Camera camera)
    {
        if(!camera||GraphicsSettings.currentRenderPipeline)return;
        stale.Clear();foreach(var pair in cameras)if(!pair.Key)stale.Add(pair.Key);foreach(var key in stale){cameras[key].Dispose();cameras.Remove(key);}
        var planes=GeometryUtility.CalculateFrustumPlanes(camera);bool visible=false;
        foreach(var renderer in users)if(renderer&&renderer.enabled&&!renderer.forceRenderingOff&&renderer.gameObject.activeInHierarchy&&(camera.cullingMask&(1<<renderer.gameObject.layer))!=0&&GeometryUtility.TestPlanesAABB(planes,renderer.bounds)){visible=true;break;}
        if(!visible){if(cameras.TryGetValue(camera,out var unused)){unused.Dispose();cameras.Remove(camera);}Shader.SetGlobalFloat(Ready,0);return;}
        int width=Mathf.Max(1,camera.pixelWidth),height=Mathf.Max(1,camera.pixelHeight);
        var format=camera.allowHDR?RenderTextureFormat.ARGBHalf:RenderTextureFormat.ARGB32;
        if(cameras.TryGetValue(camera,out var existing)&&(existing.colour.width!=width||existing.colour.height!=height||existing.colour.format!=format)){existing.Dispose();cameras.Remove(camera);}
        if(!cameras.TryGetValue(camera,out var capture))
        {
            capture=new Capture{camera=camera,colour=new RenderTexture(width,height,0,format){name="Dragon refraction "+camera.name,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave},commands=new CommandBuffer{name="Dragon per-camera opaque colour"}};capture.colour.Create();
            // 全局 onPreRender 在地图 OnPreCull 之后执行，追加到同一事件，先完成地图雾再拷贝。
            camera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha,capture.commands);cameras.Add(camera,capture);
        }
        capture.commands.Clear();capture.commands.Blit(BuiltinRenderTextureType.CameraTarget,capture.colour);capture.commands.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);capture.commands.SetGlobalTexture(Texture,capture.colour);capture.commands.SetGlobalVector(Texture+"_TexelSize",new Vector4(1f/width,1f/height,width,height));capture.commands.SetGlobalFloat(Ready,1);
    }
}

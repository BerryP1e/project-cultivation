using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Cultivation.WorldAuthoring
{
    // Built-in adapter for the workshop's same depth/normal environment contour mask.
    [RequireComponent(typeof(Camera))]
    public sealed class WorldMapInk : MonoBehaviour
    {
        public WorldMapRenderer world;
        Camera camera3d;CommandBuffer commands;RenderTexture normals,ids;Material mask,occluder,composite;
        readonly Dictionary<Material,Material> cutouts=new Dictionary<Material,Material>();
        void OnPreCull()
        {
            if(GraphicsSettings.currentRenderPipeline||!world||world.Data==null)return;
            if(!camera3d)camera3d=GetComponent<Camera>();
            if(!mask)
            {
                var shader=world.Palette.inkMaskShader;if(!shader)shader=Shader.Find("Cultivation/WorldAuthoring/InkMask");var compose=world.Palette.inkCompositeShader;if(!compose)compose=Shader.Find("Cultivation/OutlineComposite");if(!shader||!compose)return;
                mask=new Material(shader);mask.SetFloat("_ID",.25f);occluder=new Material(shader);occluder.SetFloat("_ID",0);composite=new Material(compose);
                composite.SetFloat("_PlayerID",.25f);composite.SetColor("_PlayerColor",new Color(.025f,.031f,.039f));composite.SetFloat("_Radius",1.45f);composite.SetFloat("_Soft",.45f);composite.SetFloat("_Strength",.88f);composite.SetFloat("_DepthThr",.02f);composite.SetFloat("_NormalThr",.40f);composite.SetFloat("_IDThr",.02f);composite.SetFloat("_WidthByDepth",.38f);composite.SetFloat("_DryBrush",.10f);composite.SetFloat("_InkFade",.22f);
                commands=new CommandBuffer{name="World map environment contours"};camera3d.AddCommandBuffer(CameraEvent.AfterForwardOpaque,commands);
            }
            int width=Mathf.Max(1,camera3d.pixelWidth),height=Mathf.Max(1,camera3d.pixelHeight);if(!normals||normals.width!=width||normals.height!=height){ReleaseTargets();normals=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGBHalf);normals.filterMode=FilterMode.Bilinear;ids=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32);ids.filterMode=FilterMode.Point;}
            commands.Clear();commands.SetRenderTarget(ids);commands.ClearRenderTarget(false,true,Color.clear);commands.SetRenderTarget(normals);commands.ClearRenderTarget(true,true,new Color(.5f,.5f,.5f,1));commands.SetRenderTarget(new RenderTargetIdentifier[]{normals,ids},normals);
            mask.SetFloat("_CamFar",camera3d.farClipPlane);occluder.SetFloat("_CamFar",camera3d.farClipPlane);var planes=GeometryUtility.CalculateFrustumPlanes(camera3d);
            foreach(var subject in WorldInkTarget.Active)
            {
                if(!subject||!subject.isActiveAndEnabled)continue;
                foreach(var renderer in subject.Renderers)
                {
                    if(!renderer||!renderer.enabled||renderer is ParticleSystemRenderer||!GeometryUtility.TestPlanesAABB(planes,renderer.bounds))continue;
                    var materials=renderer.sharedMaterials;for(int sub=0;sub<materials.Length;sub++)
                    {
                        var original=materials[sub];var draw=subject.occluder?occluder:mask;
                        if(!subject.occluder&&original&&original.HasProperty("_Cutoff")&&original.GetFloat("_Cutoff")>0){if(!cutouts.TryGetValue(original,out draw)){draw=new Material(mask);draw.SetTexture("_BaseMap",original.GetTexture("_BaseMap"));draw.SetFloat("_Cutoff",original.GetFloat("_Cutoff"));draw.SetFloat("_Cull",original.GetFloat("_Cull"));draw.SetTextureScale("_BaseMap",original.GetTextureScale("_BaseMap"));draw.SetTextureOffset("_BaseMap",original.GetTextureOffset("_BaseMap"));cutouts[original]=draw;}draw.SetFloat("_CamFar",camera3d.farClipPlane);}commands.DrawRenderer(renderer,draw,sub,0);
                    }
                }
            }
            commands.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
        }
        void OnRenderImage(RenderTexture source,RenderTexture destination)
        {
            if(!composite||!normals||GraphicsSettings.currentRenderPipeline){Graphics.Blit(source,destination);return;}
            composite.SetTexture("_MaskTex",normals);composite.SetTexture("_IDTex",ids);composite.SetVector("_MaskTex_TexelSize",new Vector4(1f/normals.width,1f/normals.height,normals.width,normals.height));Graphics.Blit(source,destination,composite);
        }
        void ReleaseTargets(){if(normals)RenderTexture.ReleaseTemporary(normals);if(ids)RenderTexture.ReleaseTemporary(ids);normals=ids=null;}
        void OnDisable(){if(commands!=null){if(camera3d)camera3d.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque,commands);commands.Release();commands=null;}ReleaseTargets();DestroyMaterial(mask);DestroyMaterial(occluder);DestroyMaterial(composite);mask=occluder=composite=null;foreach(var material in cutouts.Values)DestroyMaterial(material);cutouts.Clear();}
        static void DestroyMaterial(Material material){if(!material)return;if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
    }
}

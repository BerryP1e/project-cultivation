using UnityEngine;
using UnityEngine.Rendering;

namespace Cultivation.WorldAuthoring
{
    /// <summary>工坊与正式地图共用的灯光和后期基线，不改技能材质或粒子尺寸。</summary>
    public static class WorldMapLook
    {
        public static void Apply(WorldMapPalette palette,Camera camera,bool outdoor)
        {
            if(!camera)return;
            camera.allowHDR=true;camera.depthTextureMode|=DepthTextureMode.Depth;
            var bloom=camera.GetComponent<GameGlobalBloom>()??camera.gameObject.AddComponent<GameGlobalBloom>();
            bloom.阈值=1.05f;bloom.软膝=.45f;bloom.强度=.28f;
            var grade=camera.GetComponent<GameGlobalGrade>()??camera.gameObject.AddComponent<GameGlobalGrade>();grade.ApplyWorldStyle();
            var preview=camera.GetComponent<画面基线预览>();if(preview)preview.enabled=false;
            if(!outdoor)return;
            if(palette&&palette.skybox)RenderSettings.skybox=palette.skybox;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.52f,.59f,.65f);
            RenderSettings.ambientEquatorColor=new Color(.38f,.41f,.41f);
            RenderSettings.ambientGroundColor=new Color(.23f,.25f,.24f);
            RenderSettings.ambientIntensity=1;
            RenderSettings.fog=false;
            Light sun=RenderSettings.sun;
            if(!sun||sun.gameObject.scene!=camera.gameObject.scene)
                foreach(var light in Object.FindObjectsOfType<Light>())if(light.gameObject.scene==camera.gameObject.scene&&light.type==LightType.Directional&&(light.cullingMask&1)!=0){sun=light;break;}
            if(sun){sun.intensity=1.10f;sun.color=new Color(1,.97f,.91f);sun.shadows=LightShadows.Soft;sun.shadowStrength=.72f;RenderSettings.sun=sun;}
        }
    }
}

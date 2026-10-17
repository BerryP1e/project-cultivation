using UnityEngine;

namespace ASP
{
    static class ASPShaderIDs
    {
        static readonly int ASPMaterialTexture = Shader.PropertyToID("_ASPMaterialTexture");
        static readonly int ASPMaterialDepthTexture = Shader.PropertyToID("_ASPMaterialDepthTexture");
        static readonly int ASPDepthOffsetShadowTexture = Shader.PropertyToID("_ASPDepthOffsetShadowTexture");
        static readonly int MaterialID = Shader.PropertyToID("_MaterialID");
        
        static readonly int FaceFrontDirection = Shader.PropertyToID("_FaceFrontDirection");
        static readonly int FaceRightDirection = Shader.PropertyToID("_FaceRightDirection");
        static readonly int OverrideLightDirToggle = Shader.PropertyToID("_OverrideLightDirToggle");
        static readonly int FakeLightEuler = Shader.PropertyToID("_FakeLightEuler");
        
        static readonly int Dithering = Shader.PropertyToID("_Dithering");
        static readonly int DitherTexelSize = Shader.PropertyToID("_DitherTexelSize");
        static readonly int FOVShiftX = Shader.PropertyToID("_FOVShiftX");
        
        static readonly int UseSimpleAABBCutOffForCharacterShadow = Shader.PropertyToID("_UseSimpleAABBCutOffForCharacterShadow");
    }
}


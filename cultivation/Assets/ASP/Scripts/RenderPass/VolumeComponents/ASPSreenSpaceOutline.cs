using System;
using ASPUtil;
using UnityEngine.Rendering.Universal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace ASP
{
    public enum ScreenSpaceOutlineDebugMode : int
    {
            MaterialEdge = 0,
            AlbedoEdge = 1,
            DepthEdge = 2,
            NormalsEdge = 3,
            OuterEdge = 4,
            VertexColorMask = 5,
            CombinedResultAndVertexColorMask = 6,
            CombinedResult = 7,
    }
//[Enum(Material, 0, Luma, 1, Depth, 2, Normals, 3, OuterLine, 4, VertexColorMask, 5 , Result, 6)]
    [Serializable, VolumeComponentMenu("Post-processing/ASP Screen Space Outline")]
    public class ASPSreenSpaceOutline : VolumeComponent, IPostProcessComponent
    {
        [Header("General")] 
        [FormerlySerializedAs("EnableOutlineEffect")] public BoolParameter EnableOutline = new BoolParameter(false, false);
        [FormerlySerializedAs("EnableOutlineFXAA")] public BoolParameter ApplyFXAA = new BoolParameter(false);
        public ClampedFloatParameter OutlineWidth = new ClampedFloatParameter(1f, 0.5f, 5f);
        public ColorParameter OutlineColor = new ColorParameter(Color.black);
        [Header("----------------------------------------------------------------------------------------------------------------")]
        [Header("Outer Edge")] 
        public BoolParameter EnableOuterline = new BoolParameter(true);
        [Header("Material Edge")] 
        public BoolParameter EnableMaterialEdge = new BoolParameter(true);
        [Space(10)]
        public ClampedFloatParameter MaterialEdgeThreshold = new ClampedFloatParameter(1.0f, 0.05f, 1.0f);
        public ClampedFloatParameter MaterialEdgeWeight = new ClampedFloatParameter(5.0f, 0f, 15f);
        public MinIntParameter MaterialEdgeBias = new MinIntParameter(2, 1);
        
        [Header("Albedo Edge")] 
        public BoolParameter EnableAlbedoEdge = new BoolParameter(true);
        [Space(10)]
        public ClampedFloatParameter AlbedoEdgeThreshold = new ClampedFloatParameter(1.0f, 0.05f, 1.0f);
        public ClampedFloatParameter AlbedoEdgeWeight = new ClampedFloatParameter(5.0f, 0f, 10f);
        public MinIntParameter AlbedoEdgeBias = new MinIntParameter(2, 1);

        [Header("Depth Edge")] 
        public BoolParameter EnableDepthEdge = new BoolParameter(true);
        [Space(10)]
        public ClampedFloatParameter DepthEdgeThreshold = new ClampedFloatParameter(1.0f, 0.05f, 1.0f);
        public ClampedFloatParameter DepthEdgeWeight = new ClampedFloatParameter(20.0f, 0f, 50f);
        public MinIntParameter DepthEdgeBias = new MinIntParameter(2, 2);

        [Header("Normals Edge")] 
        public BoolParameter EnableNormalsEdge = new BoolParameter(true);
        [Space(10)]
        public ClampedFloatParameter NormalsEdgeThreshold = new ClampedFloatParameter(1.0f, 0.05f, 1.0f);
        public ClampedFloatParameter NormalsEdgeWeight = new ClampedFloatParameter(1.0f, 0f, 8.0f);
        public MinIntParameter NormalsEdgeBias = new MinIntParameter(3, 2);
        [Header("----------------------------------------------------------------------------------------------------------------")]
        [Header("Color & Weight Distance Fade")]
        public BoolParameter FadingWeghtByDistance = new BoolParameter(true);
        public BoolParameter FadingColorByDistance = new BoolParameter(true);
        public FloatRangeParameter ColorWeightFadingStartEndDistance = new FloatRangeParameter(new Vector2(2f,15f), 0.0f, 50f);
        
        [Header("Outline Width Distance Fade")]
        public BoolParameter FadingWidthByDistance = new BoolParameter(false);
        public FloatRangeParameter WidthFadingStartEndDistance = new FloatRangeParameter(new Vector2(2f,15f), 0.0f, 50f);
        [Header("----------------------------------------------------------------------------------------------------------------")]
        [Header("Debug")]
        public BoolParameter EnableDebugMode = new BoolParameter(false);
        public ColorParameter DebugBackground = new ColorParameter(Color.white);
        
        public ScreenSpaceOutlineDebugModeParameter ScreenSpaceOutlineDebugMode =
            new ScreenSpaceOutlineDebugModeParameter(ASP.ScreenSpaceOutlineDebugMode.CombinedResult);
        public ASPSreenSpaceOutline()
        {
            displayName = "Screen Space Character Outline";
        }
        
        public bool IsActive()
        {
            return EnableOutline.value;
        }


        /// <inheritdoc/>
        [Obsolete("Unused #from(2023.1)", false)]
        public bool IsTileCompatible() => false;
    }

    [Serializable]
    public sealed class ScreenSpaceOutlineDebugModeParameter : VolumeParameter<ScreenSpaceOutlineDebugMode>
    {
        
        public ScreenSpaceOutlineDebugModeParameter(ScreenSpaceOutlineDebugMode value, bool overrideState = false)
            : base(value, overrideState)
        {
            
        }
    }
}
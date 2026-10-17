using System;
using UnityEngine.Rendering.Universal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace ASP
{
    public enum ToneMapCurveType : int
    {
        Filmic = 1,
        KhronosNeutral = 2,
    }

    [Serializable, VolumeComponentMenu("Post-processing/ASP Tone Mapping")]
    public class ASPToneMap : VolumeComponent, IPostProcessComponent
    {
        [SerializeField]public ToneMapCurveTypeParameter ToneMapType =
        new ToneMapCurveTypeParameter(ToneMapCurveType.Filmic);
        
        [FormerlySerializedAs("FilmicExposure")] public ClampedFloatParameter Exposure = new ClampedFloatParameter(1.0f, 0.2f, 7f);
        public BoolParameter IgnoreCharacterPixels = new BoolParameter(false);
        public ClampedFloatParameter CharacterPixelsToneMapStrength = new ClampedFloatParameter(0.0f, 0, 1.0f);
        public ASPToneMap()
        {
            displayName = "ASP Tone Mapping";
        }

        public bool IsActive()
        {
            return (int)ToneMapType.value >= 0 && ToneMapType.overrideState;
        }

        [Obsolete("Unused #from(2023.1)", false)]
        public bool IsTileCompatible()
        {

            return false;
        }
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <see cref="ToneMapCurveType"/> value.
    /// </summary>
    [Serializable]
    public sealed class ToneMapCurveTypeParameter : VolumeParameter<ToneMapCurveType>
    {
        /// <summary>
        /// Creates a new <see cref="ToneMapCurveTypeParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public ToneMapCurveTypeParameter(ToneMapCurveType value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }
}
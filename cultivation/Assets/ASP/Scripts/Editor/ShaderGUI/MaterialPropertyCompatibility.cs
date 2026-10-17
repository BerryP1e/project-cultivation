// Unity 2022 compatibility for the MaterialProperty API used by this imported LWGUI.
using UnityEditor;
using UnityEngine.Rendering;

namespace LWGUI
{
    internal static class MaterialPropertyCompatibility
    {
        internal static ShaderPropertyType CompatibleType(this MaterialProperty property)
        {
            switch (property.type)
            {
                case MaterialProperty.PropType.Color: return ShaderPropertyType.Color;
                case MaterialProperty.PropType.Vector: return ShaderPropertyType.Vector;
                case MaterialProperty.PropType.Float: return ShaderPropertyType.Float;
                case MaterialProperty.PropType.Range: return ShaderPropertyType.Range;
                case MaterialProperty.PropType.Texture: return ShaderPropertyType.Texture;
                case MaterialProperty.PropType.Int: return ShaderPropertyType.Int;
                default: return ShaderPropertyType.Float;
            }
        }
        internal static ShaderPropertyFlags CompatibleFlags(this MaterialProperty property)
            => (ShaderPropertyFlags)(int)property.flags;
    }
}

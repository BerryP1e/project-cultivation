using UnityEngine;
namespace Cultivation.WorldAuthoring
{
    public sealed class WorldMapAsset : ScriptableObject
    {
        [TextArea(4,20)] public string sourceJson;
        public WorldMapPalette palette;
    }
}

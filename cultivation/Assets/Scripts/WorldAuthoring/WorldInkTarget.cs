using UnityEngine;
namespace Cultivation.WorldAuthoring
{
    public sealed class WorldInkTarget : MonoBehaviour
    {
        public bool occluder;
        Renderer[] renderers;
        public Renderer[] Renderers=>renderers??(renderers=GetComponentsInChildren<Renderer>());
        public static readonly System.Collections.Generic.HashSet<WorldInkTarget> Active=new System.Collections.Generic.HashSet<WorldInkTarget>();
        void OnEnable(){Active.Add(this);}
        void OnDisable(){Active.Remove(this);}
    }
}

using UnityEngine;
namespace Cultivation.WorldAuthoring
{
    public sealed class WorldInkTarget : MonoBehaviour
    {
        public bool occluder;
        public bool 树木;
        public float 接地高度,根部淡化高度;
        Renderer[] renderers;
        Material[][] materials;
        public Renderer[] Renderers=>renderers??(renderers=GetComponentsInChildren<Renderer>());
        public Material[][] 材质列表{get{if(materials==null){var list=Renderers;materials=new Material[list.Length][];for(int i=0;i<list.Length;i++)materials[i]=list[i]?list[i].sharedMaterials:new Material[0];}return materials;}}
        public bool 仅遮挡(Material material)=>occluder||(树木&&material&&material.HasProperty("_Cutoff")&&material.GetFloat("_Cutoff")>0);
        public static readonly System.Collections.Generic.HashSet<WorldInkTarget> Active=new System.Collections.Generic.HashSet<WorldInkTarget>();
        void OnEnable(){renderers=null;materials=null;Active.Add(this);}
        void OnDisable(){Active.Remove(this);}
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cultivation.WorldAuthoring
{
    public sealed class WorldPlacementPreview : IDisposable
    {
        GameObject root;
        string selected;
        readonly List<Material> materials=new List<Material>();
        public GameObject Root=>root;

        public void Show(WorldPaletteEntry entry,Vector3 position,float yaw,float scale,bool valid)
        {
            if(!root || selected!=entry.id)Build(entry);
            root.SetActive(true);root.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));root.transform.localScale=Vector3.one*scale;
            var colour=valid?new Color(.72f,1,.80f,.62f):new Color(1,.15f,.10f,.68f);
            foreach(var material in materials)material.SetColor("_GhostTint",colour);
        }
        public void Hide(){if(root)root.SetActive(false);}
        void Build(WorldPaletteEntry entry)
        {
            Dispose();selected=entry.id;root=new GameObject("Placement preview: "+entry.label);
            var model=UnityEngine.Object.Instantiate(entry.prefab,root.transform,false);
            foreach(var script in model.GetComponentsInChildren<MonoBehaviour>(true)){script.enabled=false;UnityEngine.Object.Destroy(script);}
            foreach(var collider in model.GetComponentsInChildren<Collider>(true))collider.enabled=false;
            foreach(var body in model.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.detectCollisions=false;UnityEngine.Object.Destroy(body);}
            foreach(var light in model.GetComponentsInChildren<Light>(true))light.enabled=false;
            var renderers=model.GetComponentsInChildren<Renderer>(true);bool found=false;Bounds bounds=default;int slot=0;
            foreach(var renderer in renderers)
            {
                if(!found){bounds=renderer.bounds;found=true;}else bounds.Encapsulate(renderer.bounds);
                var slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++)
                {
                    var source=slot<entry.materials.Length?entry.materials[slot]:slots[i];slot++;
                    var material=new Material(Shader.Find("Cultivation/WorldAuthoring/PlacementPreview"));
                    if(source){material.SetTexture("_BaseMap",source.GetTexture("_BaseMap"));material.SetColor("_BaseColor",source.GetColor("_BaseColor"));material.SetFloat("_Cutoff",source.GetFloat("_Cutoff"));material.SetFloat("_Cull",source.GetFloat("_Cull"));material.SetTextureScale("_BaseMap",source.GetTextureScale("_BaseMap"));material.SetTextureOffset("_BaseMap",source.GetTextureOffset("_BaseMap"));}
                    slots[i]=material;materials.Add(material);
                }
                renderer.sharedMaterials=slots;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
            if(found&&!entry.module&&!entry.preservePivot)model.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=2;
        }
        public void Dispose()
        {
            if(root){root.SetActive(false);UnityEngine.Object.Destroy(root);root=null;}
            foreach(var material in materials)if(material)UnityEngine.Object.Destroy(material);materials.Clear();selected=null;
        }
    }
}

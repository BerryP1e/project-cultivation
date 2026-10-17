using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Cultivation.WorldAuthoring
{
    // A visual-only copy of the real Player_Visual; never boots gameplay or save systems.
    public sealed class WorldPlayerReference : IDisposable
    {
        public GameObject Root {get;private set;}
        PlayableGraph graph;
        AnimationClipPlayable clip;
        float time,length;
        public void Show(WorldMapPalette palette,Vector3 position,float yaw)
        {
            if(!Root)
            {
                if(!palette.referencePlayer)return;
                Root=new GameObject("Player size reference");var model=UnityEngine.Object.Instantiate(palette.referencePlayer,Root.transform,false);model.SetActive(true);
                var animator=model.GetComponentInChildren<Animator>(true);
                if(animator&&palette.referenceIdle)
                {
                    animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    graph=PlayableGraph.Create("World authoring player reference");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    clip=AnimationClipPlayable.Create(graph,palette.referenceIdle);length=palette.referenceIdle.length;
                    var output=AnimationPlayableOutput.Create(graph,"Reference idle",animator);output.SetSourcePlayable(clip);graph.Play();graph.Evaluate(0);
                }
                bool found=false;Bounds bounds=default;
                foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    if(renderer is SkinnedMeshRenderer skin)
                    {
                        var baked=new Mesh();skin.BakeMesh(baked);
                        foreach(var vertex in baked.vertices){var world=skin.transform.TransformPoint(vertex);if(!found){bounds=new Bounds(world,Vector3.zero);found=true;}else bounds.Encapsulate(world);}UnityEngine.Object.Destroy(baked);
                    }
                    else if(!found){bounds=renderer.bounds;found=true;}else bounds.Encapsulate(renderer.bounds);
                }
                if(found)model.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                foreach(var t in Root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=2;
                Root.AddComponent<WorldInkTarget>();
            }
            Root.SetActive(true);Root.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
        }
        public void Tick(float delta){if(graph.IsValid()&&length>0){time=(time+delta)%length;clip.SetTime(time);graph.Evaluate(0);}}
        public void Hide(){if(Root)Root.SetActive(false);}
        public void Dispose(){if(graph.IsValid())graph.Destroy();if(Root)UnityEngine.Object.Destroy(Root);Root=null;}
    }
}

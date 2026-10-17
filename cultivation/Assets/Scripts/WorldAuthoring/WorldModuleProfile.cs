using UnityEngine;

namespace Cultivation.WorldAuthoring
{
    public sealed class WorldModuleProfile : ScriptableObject
    {
        public int revision;
        public string sourceHash,family;
        public GameObject prefab;
        public Vector3[] points,directions;
        public float groundHalfWidth=.2f;
        public float DistanceToBody(Vector3 local)
        {
            var p=new Vector2(local.x,local.z);float best=float.MaxValue;
            for(int i=0;i<points.Length;i++)
            {
                var a=points.Length==2&&Vector3.Dot(directions[0],directions[1])<-.9f?new Vector2(points[0].x,points[0].z):Vector2.zero;
                var b=new Vector2(points[i].x,points[i].z);var d=b-a;
                float t=d.sqrMagnitude>.0001f?Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude):0;best=Mathf.Min(best,Vector2.Distance(p,a+d*t));
            }
            return best-groundHalfWidth;
        }
    }
}

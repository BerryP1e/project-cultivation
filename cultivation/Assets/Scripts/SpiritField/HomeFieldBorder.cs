using UnityEngine;

/// <summary>稳定的浅沟与散石；无碰撞，不阻挡玩家和傀儡。</summary>
public sealed class HomeFieldBorder : MonoBehaviour
{
    static Material stone, ditch;
    Mesh mesh;
    void OnDestroy(){if(mesh)Destroy(mesh);}
    public static void 建造(Transform parent, int seed)
    {
        if (!stone) stone = new Material(Shader.Find("Standard")) { color = new Color(.40f,.42f,.36f) };
        if (!ditch) ditch = new Material(Shader.Find("Standard")) { color = new Color(.20f,.18f,.13f) };
        var random = new System.Random(seed * 1009 + 317);
        float half = 灵田规格.床边长 * .5f;
        var vertices=new System.Collections.Generic.List<Vector3>();
        var triangles=new System.Collections.Generic.List<int>();
        Vector3[] unit={Vector3.up,Vector3.down,Vector3.left,Vector3.right,Vector3.forward,Vector3.back};
        int[] faces={0,4,3,0,3,5,0,5,2,0,2,4,1,3,4,1,5,3,1,2,5,1,4,2};
        for (int i = 0; i < 18; i++)
        {
            float a = Mathf.PI * 2 * (i+(float)random.NextDouble()*.35f) / 18;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var p = dir * ((half + .065f) / Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y)));
            p *= 1 - .10f * Mathf.Pow(Mathf.Abs(dir.x * dir.y) * 2, 6);
            float size = .08f + (float)random.NextDouble() * .09f;
            int offset=vertices.Count;var turn=Quaternion.Euler(0,(float)random.NextDouble()*360,0);
            foreach(var v in unit)vertices.Add(new Vector3(p.x,.025f,p.y)+turn*Vector3.Scale(v,new Vector3(size*.7f,size*.4f,size*.6f)));
            foreach(int f in faces)triangles.Add(offset+f);
        }
        var groove=new System.Collections.Generic.List<int>();
        int grooveStart=vertices.Count;
        for(int i=0;i<64;i++)
        {
            float a=i*Mathf.PI/32;var direction=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
            float radius=(half+.055f)/Mathf.Max(Mathf.Abs(direction.x),Mathf.Abs(direction.y));
            radius*=1-.10f*Mathf.Pow(Mathf.Abs(direction.x*direction.y)*2,6);
            var p=direction*radius;
            vertices.Add(new Vector3(p.x,.009f,p.y));vertices.Add(new Vector3(p.x,.009f,p.y)+new Vector3(direction.x,0,direction.y)*.075f);
            int next=(i+1)%64*2+grooveStart,start=grooveStart+i*2;
            groove.AddRange(new[]{start,next,start+1,start+1,next,next+1});
        }
        var go=new GameObject("浅沟与散石");go.transform.SetParent(parent,false);
        var owner=go.AddComponent<HomeFieldBorder>();owner.mesh=new Mesh{name="灵田边缘"};owner.mesh.SetVertices(vertices);owner.mesh.subMeshCount=2;owner.mesh.SetTriangles(triangles,0);owner.mesh.SetTriangles(groove,1);owner.mesh.RecalculateNormals();owner.mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh=owner.mesh;go.AddComponent<MeshRenderer>().sharedMaterials=new[]{stone,ditch};
    }
}

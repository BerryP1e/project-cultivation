using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class WorldGrassMeshBuilder
{
    // Each blade has a closed triangular section and a curved centreline.
    // There are no alpha cards whose rectangular edge can face the camera.
    public static Mesh Build(string path)
    {
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
        var vertices=new List<Vector3>();var colours=new List<Color>();var uv=new List<Vector2>();var triangles=new List<int>();
        var random=new System.Random(76153);
        for(int blade=0;blade<24;blade++)
        {
            float angle=(float)random.NextDouble()*Mathf.PI*2;
            float radius=Mathf.Sqrt((float)random.NextDouble())*.29f;
            var root=new Vector3(Mathf.Cos(angle)*radius,-.025f,Mathf.Sin(angle)*radius);
            float facing=(float)random.NextDouble()*Mathf.PI*2,height=.42f+(float)random.NextDouble()*.40f,width=.065f+(float)random.NextDouble()*.035f;
            var bend=new Vector3(Mathf.Cos(facing),0,Mathf.Sin(facing))*(.18f+(float)random.NextDouble()*.17f);
            var across=new Vector3(-Mathf.Sin(facing),0,Mathf.Cos(facing));
            int start=vertices.Count;
            for(int segment=0;segment<4;segment++)
            {
                float t=segment/4f;
                var centre=root+Vector3.up*(height*t)+bend*t*t;
                float halfWidth=width*(1-t)*(.72f+.42f*Mathf.Sin(t*Mathf.PI));
                for(int face=0;face<3;face++)
                {
                    float theta=face*Mathf.PI*2/3;
                    vertices.Add(centre+across*(Mathf.Cos(theta)*halfWidth)+bend.normalized*(Mathf.Sin(theta)*halfWidth*.27f));
                    colours.Add(new Color(t,t,t,1));uv.Add(new Vector2(face/2f,t));
                }
            }
            int tip=vertices.Count;vertices.Add(root+Vector3.up*height+bend);colours.Add(Color.white);uv.Add(new Vector2(.5f,1));
            for(int segment=0;segment<3;segment++)for(int face=0;face<3;face++)
            {
                int a=start+segment*3+face,b=start+segment*3+(face+1)%3,c=a+3,d=b+3;
                triangles.Add(a);triangles.Add(b);triangles.Add(c);triangles.Add(b);triangles.Add(d);triangles.Add(c);
            }
            for(int face=0;face<3;face++){triangles.Add(start+9+face);triangles.Add(start+9+(face+1)%3);triangles.Add(tip);}
            triangles.Add(start+2);triangles.Add(start+1);triangles.Add(start);
        }
        mesh.name="SolidGrass";mesh.SetVertices(vertices);mesh.SetColors(colours);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();mesh.UploadMeshData(false);
        EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssetIfDirty(mesh);return mesh;
    }
}

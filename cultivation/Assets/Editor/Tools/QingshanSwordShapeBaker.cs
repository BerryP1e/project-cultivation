using UnityEngine;
using UnityEditor;

/// <summary>按实际模型三角形面积烘焙剑形粒子点，不保存场景或修改源网格。</summary>
public static class QingshanSwordShapeBaker
{
    [MenuItem("修仙/法宝/烘焙青山剑粒子形状（不动场景）")]
    public static void Bake()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("请先停止 Play");
        var prefab=Resources.Load<GameObject>("法宝/青山剑/青山剑模型");var wrapper=new GameObject("GeometryBake");var model=UnityEngine.Object.Instantiate(prefab,wrapper.transform,false);model.transform.localRotation=Quaternion.Euler(225,0,0);
        try {
        var triangles=new System.Collections.Generic.List<Vector3>();var cumulative=new System.Collections.Generic.List<float>();float total=0;
        foreach(var filter in wrapper.GetComponentsInChildren<MeshFilter>(true)) {
        using(var data=UnityEditor.MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh)) {
        var array=new Unity.Collections.NativeArray<Vector3>(data[0].vertexCount,Unity.Collections.Allocator.Temp);
        try { data[0].GetVertices(array);
        for(int sub=0;sub<data[0].subMeshCount;sub++){var descriptor=data[0].GetSubMesh(sub);if(descriptor.topology!=MeshTopology.Triangles)continue;var indices=new Unity.Collections.NativeArray<int>(descriptor.indexCount,Unity.Collections.Allocator.Temp);
        try {data[0].GetIndices(indices,sub,true);for(int i=0;i+2<indices.Length;i+=3){var a=wrapper.transform.InverseTransformPoint(filter.transform.TransformPoint(array[indices[i]]));var b=wrapper.transform.InverseTransformPoint(filter.transform.TransformPoint(array[indices[i+1]]));var c=wrapper.transform.InverseTransformPoint(filter.transform.TransformPoint(array[indices[i+2]]));float area=Vector3.Cross(b-a,c-a).magnitude*.5f;if(area<.0000001f)continue;total+=area;cumulative.Add(total);triangles.Add(a);triangles.Add(b);triangles.Add(c);}}finally{indices.Dispose();}}
        }finally{array.Dispose();}}}
        var random=new System.Random(47);int count=8192;using(var writer=new System.IO.BinaryWriter(System.IO.File.Open("Assets/resources/法宝/青山剑/青山剑粒子形状.bytes",System.IO.FileMode.Create))){writer.Write(count);for(int i=0;i<count;i++){float area=(float)random.NextDouble()*total;int index=cumulative.BinarySearch(area);if(index<0)index=~index;index=Mathf.Min(index,cumulative.Count-1)*3;float u=Mathf.Sqrt((float)random.NextDouble()),v=(float)random.NextDouble();var p=triangles[index]*(1-u)+triangles[index+1]*(u*(1-v))+triangles[index+2]*(u*v);writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}}
        UnityEditor.AssetDatabase.ImportAsset("Assets/resources/法宝/青山剑/青山剑粒子形状.bytes");Debug.Log("PASS 按三角形面积均匀烘焙 8192 个剑身表面采样点");
        }finally{UnityEngine.Object.DestroyImmediate(wrapper);}

    }
}

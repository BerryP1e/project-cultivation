using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One rigid body per severed crown, then a collectible flight. Never one body per voxel.</summary>
public sealed class VoxelTreeDebris : MonoBehaviour
{
    [SerializeField] List<Mesh> meshes=new List<Mesh>();
    Rigidbody rigid;
    Transform player;
    Vector3 flightStart;
    Quaternion flightRotation;
    float age;
    bool flying, collected;
    public bool 正在回收=>flying;
    public bool 已开始 {get;private set;}

    public static IEnumerator 建立(VoxelDestructible source,byte[] cells,float[] field,Renderer[] foliage,Mesh[] canopy,System.Action<VoxelTreeDebris> ready)
    {
        var root=new GameObject("断木_倒下与回收");root.SetActive(false);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,source.gameObject.scene);
        var debris=root.AddComponent<VoxelTreeDebris>();ready(debris);var data=source.数据;
        var bounds=new Bounds();bool any=false;int scanned=0;
        foreach(int i in data.实体索引)
        {
            if(++scanned%2048==0)yield return null;
            if(cells[i]==0)continue;
            int x=i%data.尺寸.x,y=i/data.尺寸.x%data.尺寸.y,z=i/(data.尺寸.x*data.尺寸.y);
            var p=source.transform.TransformPoint(data.原点+new Vector3(x+.5f,y+.5f,z+.5f)*data.格子边长);
            if(!any){bounds=new Bounds(p,Vector3.zero);any=true;}else bounds.Encapsulate(p);
        }
        if(!any)
        {
            bounds=new Bounds(source.transform.TransformPoint(data.原点+(Vector3)data.尺寸*data.格子边长*.5f),Vector3.zero);
            for(int z=0;z<2;z++)for(int y=0;y<2;y++)for(int x=0;x<2;x++)bounds.Encapsulate(source.transform.TransformPoint(data.原点+Vector3.Scale(new Vector3(x,y,z),(Vector3)data.尺寸)*data.格子边长));
        }
        root.transform.position=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        foreach(var key in data.分块坐标)
        {
            var mesh=VoxelChunkMesher.生成(data,cells,key,field);
            if(mesh.vertexCount==0)Destroy(mesh);
            else
            {
                debris.meshes.Add(mesh);var child=new GameObject("断木");child.transform.SetParent(root.transform,false);
                child.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);child.transform.localScale=source.transform.lossyScale;
                child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=data.材质;
            }
            yield return null;
        }
        if(foliage!=null && canopy!=null)for(int i=0;i<Mathf.Min(foliage.Length,canopy.Length);i++)
        {
            if(!foliage[i] || !foliage[i].enabled || !canopy[i])continue;
            var child=new GameObject("随断木倒下的树冠");child.transform.SetParent(root.transform,false);
            child.transform.SetPositionAndRotation(foliage[i].transform.position,foliage[i].transform.rotation);child.transform.localScale=foliage[i].transform.lossyScale;
            child.AddComponent<MeshFilter>().sharedMesh=canopy[i];child.AddComponent<MeshRenderer>().sharedMaterials=foliage[i].sharedMaterials;
            yield return null;
        }
        var capsule=root.AddComponent<CapsuleCollider>();capsule.height=Mathf.Max(.3f,bounds.size.y);
        capsule.radius=Mathf.Clamp(Mathf.Min(bounds.size.x,bounds.size.z)*.35f,.12f,.6f);capsule.center=Vector3.up*capsule.height*.5f;
        debris.rigid=root.AddComponent<Rigidbody>();debris.rigid.mass=12;debris.rigid.drag=.15f;debris.rigid.angularDrag=.35f;
        debris.rigid.interpolation=RigidbodyInterpolation.Interpolate;debris.rigid.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
        var vitals=FindObjectOfType<PlayerVitals>();debris.player=vitals?vitals.transform:null;
        if(vitals)foreach(var c in vitals.GetComponentsInChildren<Collider>())Physics.IgnoreCollision(capsule,c);
    }
    public void 开始倒下()
    {
        if(已开始)return;已开始=true;
        gameObject.SetActive(true);
        Vector3 direction=player?transform.position-player.position:Vector3.forward;direction.y=0;
        if(direction.sqrMagnitude<.001f)direction=Vector3.forward;direction.Normalize();
        rigid.AddForce(direction*1.2f+Vector3.up*.15f,ForceMode.VelocityChange);
        rigid.AddTorque(Vector3.Cross(Vector3.up,direction)*2.2f,ForceMode.VelocityChange);
    }
    void Update()
    {
        age+=Time.deltaTime;
        if(!player){Destroy(gameObject);return;}
        if(!flying && age>=2.2f)
        {
            flying=true;rigid.isKinematic=true;GetComponent<Collider>().enabled=false;
            flightStart=transform.position;flightRotation=transform.rotation;
        }
        if(!flying)return;
        float t=Mathf.Clamp01((age-2.2f)/.9f),ease=t*t*(3-2*t);
        var target=player.position+Vector3.up*1.1f;
        transform.position=Vector3.Lerp(flightStart,target,ease)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.8f);
        transform.rotation=Quaternion.Slerp(flightRotation,Quaternion.identity,ease);
        transform.localScale=Vector3.one*Mathf.Lerp(1,.025f,ease);
        if(t<1 || collected)return;
        collected=true;var inventory=FindObjectOfType<UIPanelData>();var db=QuestDatabase.取();
        if(inventory && db)foreach(var item in db.物品库)if(item && item.物品id=="item_lingmu"){inventory.给物品(item,1);break;}
        Destroy(gameObject);
    }
    void OnDestroy(){foreach(var mesh in meshes)if(mesh)Destroy(mesh);}
}

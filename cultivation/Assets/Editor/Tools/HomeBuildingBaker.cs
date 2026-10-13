using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>三种静态建筑、五种中立家畜和物品图标的可重建入口；不写任何场景。</summary>
public static class HomeBuildingBaker
{
    const string Home="Assets/resources/Home", Art="Assets/Art/Environment/Home/Production", Icons="Assets/resources/UI/HomeProduction";
    static GameObject Building(string name,string file,float size)
    {
        var root=new GameObject(name);
        try
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TripoModels/"+name+"/"+name+".fbx");
            if(!source)throw new InvalidOperationException("缺少建筑模型："+name);
            var model=UnityEngine.Object.Instantiate(source,root.transform);model.name="模型";model.transform.localPosition=Vector3.zero;
            // Tripo FBX 的坐标系补偿在导入根节点（X=-90°），归零会把围栏竖起来。
            model.transform.localRotation=source.transform.localRotation;model.transform.localScale=source.transform.localScale*size;
            foreach(var collider in model.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
            // 非凸静态三角网格保留圆栏中央空腔和入口，禁用凸包，避免用实心盒封住动物。
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true))
            { var collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;collider.convex=false; }
            if(file=="AnimalPen")Basket(root.transform);
            return PrefabUtility.SaveAsPrefabAsset(root,Home+"/"+file+".prefab");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    static Material Material(string name,Color color)
    {
        string path=Art+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}m.color=color;m.SetFloat("_Glossiness",.15f);EditorUtility.SetDirty(m);return m;
    }
    static GameObject Part(Transform parent,PrimitiveType type,string name,Vector3 p,Vector3 scale,Material material)
    {
        var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=scale;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;return go;
    }
    static void Basket(Transform parent)
    {
        var basket=new GameObject("收获储物篮");basket.transform.SetParent(parent,false);basket.transform.localPosition=new Vector3(3.35f,.02f,0);
        var wood=Material("BasketWood",new Color(.38f,.24f,.11f));
        Part(basket.transform,PrimitiveType.Cylinder,"篮底",new Vector3(0,.03f,0),new Vector3(.8f,.03f,.8f),wood);
        for(int i=0;i<24;i++)
        {
            float a=i*Mathf.PI*2/24;var go=Part(basket.transform,PrimitiveType.Cube,"竹条",new Vector3(Mathf.Cos(a)*.39f,.24f,Mathf.Sin(a)*.39f),new Vector3(.07f,.42f,.035f),wood);go.transform.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);
        }
        // 竹篮仅用于交互表现，不添加中央实心碰撞；动物漫步范围另留出 0.95 米。
    }
    static HomeAnimalDefinition Animal(string id,string name,string sourceName,int volume,string output,string favorite,float width,int manure=1)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/resources/NPC/Animal/"+sourceName+"/"+sourceName+"_01.prefab");
        if(!source)throw new InvalidOperationException("缺少家畜模型："+sourceName);
        var root=new GameObject(name);GameObject model=null;
        try
        {
            model=UnityEngine.Object.Instantiate(source,root.transform);model.name="模型";model.transform.localPosition=Vector3.zero;model.transform.localRotation=source.transform.localRotation;
            var animator=model.GetComponentInChildren<Animator>();var clips=animator.runtimeAnimatorController.animationClips;
            foreach(var script in model.GetComponentsInChildren<MonoBehaviour>(true).OrderBy(s=>s is NpcInstance?1:0))if(script)UnityEngine.Object.DestroyImmediate(script);
            foreach(var collider in model.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
            foreach(var old in model.GetComponentsInChildren<Animation>(true))UnityEngine.Object.DestroyImmediate(old);
            var rs=model.GetComponentsInChildren<Renderer>();var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
            float factor=width/Mathf.Max(bounds.size.x,bounds.size.z);model.transform.localScale*=factor;
            model.transform.localPosition=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z)*factor;
            animator.applyRootMotion=false;animator.runtimeAnimatorController=null;
            var d=new HomeAnimalDefinition{ id=id,名=name,幼崽id="item_home_cub_"+id,容积=volume,产物id=output,喜爱食物id=favorite,粪便数量=manure,产出间隔天=id=="muji"?.75f:1,粪便间隔天=.5f,半径=width*.55f };
            d.待机=clips.First(c=>c.name=="Idle");d.行走=clips.First(c=>c.name=="Walk");
            d.模型=PrefabUtility.SaveAsPrefabAsset(root,Home+"/Animals/"+sourceName+".prefab");return d;
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    static Sprite Icon(GameObject source,string id)
    {
        string path=Icons+"/"+id+".png";SectExchangeAssets.RenderIcon(source,path);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=256;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static void SetIcon(string id,GameObject source)
    {
        var item=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Generated/ItemDefinition/"+id+".asset");if(!item)throw new InvalidOperationException("缺少物品定义 "+id);
        item.图标=Icon(source,id);EditorUtility.SetDirty(item);
    }
    static void ProductIcon(string id,int kind,Color color)
    {
        var go=new GameObject(id);
        try
        {
            var mat=Material("Product_"+id,color);
            if(kind==0)Part(go.transform,PrimitiveType.Sphere,"鸡蛋",Vector3.zero,new Vector3(.7f,1,.7f),mat);
            else if(kind==1){Part(go.transform,PrimitiveType.Cylinder,"奶罐",Vector3.zero,new Vector3(.7f,.5f,.7f),mat);Part(go.transform,PrimitiveType.Cylinder,"罐口",new Vector3(0,.65f,0),new Vector3(.4f,.15f,.4f),mat);}
            else if(kind==2)
            {
                Part(go.transform,PrimitiveType.Cylinder,"羽轴",Vector3.zero,new Vector3(.035f,.6f,.035f),mat);
                for(int i=0;i<9;i++){float y=-.4f+i*.1f;float w=.15f+Mathf.Sin(i/9f*Mathf.PI)*.4f;var p=Part(go.transform,PrimitiveType.Sphere,"羽片",new Vector3(0,y,0),new Vector3(w,.16f,.08f),mat);p.transform.localRotation=Quaternion.Euler(0,0,-20);}
            }
            else if(kind==3)
            {
                for(int i=0;i<6;i++)Part(go.transform,PrimitiveType.Sphere,"绒毛",new Vector3(Mathf.Cos(i)*.18f,Mathf.Sin(i)*.18f,0),new Vector3(.55f,.55f,.55f),mat);
            }
            else if(kind==4)
            {
                for(int sign=-1;sign<=1;sign+=2)
                {
                    var p=Part(go.transform,PrimitiveType.Cylinder,"鹿茸主枝",new Vector3(sign*.2f,.35f,0),new Vector3(.15f,.55f,.15f),mat);p.transform.localRotation=Quaternion.Euler(0,0,-sign*18);
                    for(int i=0;i<3;i++){var twig=Part(go.transform,PrimitiveType.Cylinder,"鹿茸支枝",new Vector3(sign*(.3f+i*.08f),.2f+i*.22f,0),new Vector3(.1f,.16f,.1f),mat);twig.transform.localRotation=Quaternion.Euler(0,0,-sign*45);}
                }
            }
            else if(kind==5){Part(go.transform,PrimitiveType.Sphere,"堆肥原料",Vector3.zero,new Vector3(.9f,.45f,.7f),mat);Part(go.transform,PrimitiveType.Sphere,"堆肥原料",new Vector3(.1f,.24f,0),new Vector3(.55f,.3f,.5f),mat);}
            else {Part(go.transform,PrimitiveType.Sphere,"肥料袋",Vector3.zero,new Vector3(.8f,1,.65f),mat);Part(go.transform,PrimitiveType.Cylinder,"袋口",new Vector3(0,.5f,0),new Vector3(.35f,.1f,.35f),mat);}
            SetIcon(id,go);
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
    static void Mist()
    {
        string texturePath=Art+"/WorkMist.png";
        var texture=new Texture2D(32,32,TextureFormat.RGBA32,false);var pixels=new Color[1024];
        for(int y=0;y<32;y++)for(int x=0;x<32;x++){float r=new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).magnitude;pixels[y*32+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-r),2));}
        texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(texturePath,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(texturePath);
        var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();
        var m=AssetDatabase.LoadAssetAtPath<Material>(Home+"/WorkMist.mat");if(!m){m=new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));AssetDatabase.CreateAsset(m,Home+"/WorkMist.mat");}
        m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);EditorUtility.SetDirty(m);
    }
    [MenuItem("Tools/家园/烘焙生产建筑与家畜")]
    public static void Bake()
    {
        foreach(var folder in new[]{Home+"/Animals",Art,Icons})Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        var pen=Building("兽栏","AnimalPen",8);var compost=Building("堆肥箱","CompostBin",2.2f);var furnace=Building("自动炼丹炉","AutoAlchemy",2.4f);
        var catalog=AssetDatabase.LoadAssetAtPath<HomeAnimalCatalog>(Home+"/Animals.asset");if(!catalog){catalog=ScriptableObject.CreateInstance<HomeAnimalCatalog>();AssetDatabase.CreateAsset(catalog,Home+"/Animals.asset");}
        var previous=(catalog.动物??new HomeAnimalDefinition[0]).Where(d=>d!=null).ToDictionary(d=>d.id);
        catalog.动物=new[]{
            Animal("gou","狗","Gou",2,"item_home_dog_milk","item_huichuncao",1.15f),
            Animal("linghe","灵鹤","LingHe",2,"item_home_crane_feather","item_qingxinlian",1.05f),
            Animal("muji","母鸡","MuJi",1,"item_home_egg","item_ningluhua",.65f),
            Animal("tuzi","兔子","TuZi",1,"item_home_rabbit_fur","item_huichuncao",.65f),
            Animal("xiaolu","小鹿","XiaoLu",3,"item_home_deer_antler","item_chiyanzhi",1.1f)};
        // 重建模型和图标时保留在目录中已经调过的玩法数值。
        foreach(var d in catalog.动物)if(previous.TryGetValue(d.id,out var old))
        {
            d.容积=old.容积;d.喜爱食物id=old.喜爱食物id;d.产物id=old.产物id;
            d.产出间隔天=old.产出间隔天;d.粪便间隔天=old.粪便间隔天;d.粪便数量=old.粪便数量;
        }
        EditorUtility.SetDirty(catalog);Mist();
        SetIcon("item_home_animal_pen",pen);SetIcon("item_home_compost_bin",compost);SetIcon("item_home_auto_alchemy",furnace);
        foreach(var d in catalog.动物)SetIcon(d.幼崽id,d.模型);
        ProductIcon("item_home_egg",0,new Color(.84f,.78f,.61f));ProductIcon("item_home_dog_milk",1,new Color(.83f,.87f,.8f));
        ProductIcon("item_home_crane_feather",2,new Color(.64f,.86f,.84f));ProductIcon("item_home_rabbit_fur",3,new Color(.86f,.81f,.73f));
        ProductIcon("item_home_deer_antler",4,new Color(.61f,.39f,.21f));ProductIcon(HomeAnimalCatalog.粪便id,5,new Color(.25f,.17f,.08f));ProductIcon(HomeAnimalCatalog.肥料id,6,new Color(.39f,.49f,.27f));
        AssetDatabase.SaveAssets();Debug.Log("[家园生产] 烘焙三种建筑、五种家畜与十五件物品图标；场景未修改");
    }
}

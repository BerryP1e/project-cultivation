using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cultivation.WorldAuthoring
{
    public sealed class WorldMapRenderer : MonoBehaviour
    {
        const int ChunkCells=16;
        sealed class Chunk
        {
            public GameObject root;
            public Mesh ground,side,water;
            public MeshCollider collider;
            public readonly List<Vector3[]> edges=new List<Vector3[]>();
            public readonly List<Matrix4x4[]> grassBatches=new List<Matrix4x4[]>();
        }
        struct Vertex
        {
            public Vector3 p,n;
            public float value,grass,edge;
            public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex {p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,value=Mathf.Lerp(a.value,b.value,t),grass=Mathf.Lerp(a.grass,b.grass,t),edge=Mathf.Lerp(a.edge,b.edge,t)};
        }
        sealed class Geometry
        {
            public readonly List<Vector3> p=new List<Vector3>(),n=new List<Vector3>();
            public readonly List<Vector2> uv=new List<Vector2>();
            public readonly List<Color> colours=new List<Color>();
            public readonly List<int> indices=new List<int>();
            public void Polygon(List<Vertex> vertices)
            {
                int start=p.Count;
                foreach(var v in vertices){p.Add(v.p);n.Add(v.n);uv.Add(new Vector2(v.p.x,v.p.z)*.15f);colours.Add(new Color(v.grass,v.edge,0,1));}
                for(int j=1;j+1<vertices.Count;j++)
                {
                    if(Vector3.Cross(vertices[j].p-vertices[0].p,vertices[j+1].p-vertices[0].p).sqrMagnitude<1e-10f)continue;
                    indices.Add(start);indices.Add(start+j);indices.Add(start+j+1);
                }
            }
            public Mesh Mesh(string name)
            {
                var mesh=new Mesh {name=name,indexFormat=IndexFormat.UInt32};mesh.MarkDynamic();
                mesh.SetVertices(p);mesh.SetNormals(n);mesh.SetUVs(0,uv);mesh.SetColors(colours);mesh.SetTriangles(indices,0);mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
            }
        }
        readonly Dictionary<Vector2Int,Chunk> chunks=new Dictionary<Vector2Int,Chunk>();
        readonly HashSet<Vector2Int> dirty=new HashSet<Vector2Int>();
        readonly List<GameObject> placed=new List<GameObject>(),rim=new List<GameObject>();
        struct Solid {public MapObjectData item;public WorldPaletteEntry entry;}
        readonly Dictionary<Vector2Int,List<Solid>> solids=new Dictionary<Vector2Int,List<Solid>>();
        readonly List<Vertex> polygon=new List<Vertex>(4);
        WorldMapPalette palette;
        [SerializeField,HideInInspector] Transform generatedRoot;
        public WorldMapData Data {get;private set;}
        Texture2D landMask,paintMask;
        Material terrainMaterial;
        float[] clearance,shoreHeight,grassSoil,contactShade,contactLift;
        bool maskDirty;
        public Texture2D ShoreField=>landMask;
        public Texture2D PaintField=>paintMask;
        public WorldMapPalette Palette=>palette;
        Vector2 fogBaseRange;
        public Vector2 FogHeightRange=>(Data!=null&&Data.bottomFogOnly?new Vector2(Data.fogBaseHeight-4,Data.fogBaseHeight+1.2f):fogBaseRange)+Vector2.one*(Data!=null?Data.fogHeight:0);
        public Vector4 FogPlaneSettings=>Data!=null?new Vector4(Data.bottomFogOnly?1:0,Data.fogBaseHeight,0,0):Vector4.zero;
        Material grassMaterial,waterMaterial;
        float nextMaskUpdate;
        bool useURP;
        public int ChunkCount=>chunks.Count;
        public int BoundaryCount=>rim.Count;
        public int PlacedCount=>placed.Count;
        public int GrassCount {get;private set;}
        public float GroundHeight(float x,float z)=>Data.Sample(Data.height,x,z)+(contactLift!=null?Data.Sample(contactLift,x,z):0);
        public bool IsGroundCollider(Collider candidate)
        {
            foreach(var chunk in chunks.Values)if(chunk.collider==candidate)return true;
            return false;
        }
        public float GrassSoilAt(float x,float z)=>grassSoil!=null?Data.Sample(grassSoil,x,z):0;

        public void Configure(WorldMapPalette library,WorldMapData data)
        {
            Clear();palette=library;Data=data;Data.Validate();useURP=GraphicsSettings.currentRenderPipeline!=null || QualitySettings.renderPipeline!=null;
            generatedRoot=new GameObject("Generated map geometry").transform;generatedRoot.SetParent(transform,false);
            grassMaterial=useURP?palette.grassURP:palette.grassBuiltin;
            waterMaterial=useURP?palette.waterURP:palette.waterBuiltin;
            terrainMaterial=new Material(palette.groundMaterial);
            paintMask=new Texture2D(data.resolution,data.resolution,TextureFormat.RGBA32,false,true){name="Ground paint layers",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Point};
            terrainMaterial.SetTexture("_PaintControl",paintMask);terrainMaterial.SetTexture("_TerrainLayers",palette.groundTextureArray);terrainMaterial.SetVector("_PaintRect",new Vector4(-data.Width*.5f,-data.Width*.5f,1/data.Width,data.resolution));terrainMaterial.SetFloat("_PaintEnabled",palette.groundTextureArray?1:0);
            landMask=new Texture2D(data.resolution,data.resolution,TextureFormat.RGBAHalf,false,true){name="Shore distance and elevation",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            maskDirty=true;
            IndexPlacedObjects();
            UpdateMask();
        }

        public void RebuildAll()
        {
            UpdateMask();int count=(Data.resolution-2)/ChunkCells+1;
            for(int z=0;z<count;z++)for(int x=0;x<count;x++)Rebuild(new Vector2Int(x,z));
            dirty.Clear();RebuildBoundary();RebuildObjects();CountGrass();
        }
        public void Mark(RectInt rect)
        {
            maskDirty=true;
            int margin=Mathf.CeilToInt(1.25f/Data.cellSize)+1;
            for(int z=Mathf.Max(0,rect.yMin-margin)/ChunkCells;z<=Mathf.Min(Data.resolution-2,rect.yMax+margin)/ChunkCells;z++)
                for(int x=Mathf.Max(0,rect.xMin-margin)/ChunkCells;x<=Mathf.Min(Data.resolution-2,rect.xMax+margin)/ChunkCells;x++)dirty.Add(new Vector2Int(x,z));
        }
        public void Flush()
        {
            UpdateMask();foreach(var key in dirty)Rebuild(key);dirty.Clear();RebuildBoundary();RebuildObjects();CountGrass();
        }
        public void RefreshPlacedObjects(Vector3 centre,float radius)
        {
            IndexPlacedObjects();
            int x=Mathf.FloorToInt((centre.x-radius+Data.Width*.5f)/Data.cellSize),z=Mathf.FloorToInt((centre.z-radius+Data.Width*.5f)/Data.cellSize);
            int size=Mathf.CeilToInt(radius*2/Data.cellSize)+2;Mark(new RectInt(x,z,size,size));Flush();
        }
        void Update()
        {
            if(Data==null || !palette)return;
            if(maskDirty && Time.unscaledTime>=nextMaskUpdate){nextMaskUpdate=Time.unscaledTime+.10f;UpdateMask();}
            if(!maskDirty)for(int i=0;i<2 && dirty.Count>0;i++){var iterator=dirty.GetEnumerator();iterator.MoveNext();var key=iterator.Current;iterator.Dispose();dirty.Remove(key);Rebuild(key);}
        }
        void LateUpdate()
        {
            if(!palette || !palette.grassMesh || !grassMaterial || Data==null)return;
            foreach(var chunk in chunks.Values)foreach(var batch in chunk.grassBatches)
                Graphics.DrawMeshInstanced(palette.grassMesh,0,grassMaterial,batch,batch.Length,null,ShadowCastingMode.On,true,0,null,LightProbeUsage.BlendProbes);
        }
        void CountGrass(){GrassCount=0;foreach(var c in chunks.Values)foreach(var batch in c.grassBatches)GrassCount+=batch.Length;}
        void UpdateMask()
        {
            if(!landMask || !maskDirty)return;maskDirty=false;
            int count=Data.land.Length,size=Data.resolution;
            clearance=new float[count];shoreHeight=new float[count];var outside=new float[count];
            float low=80,high=-80;
            for(int i=0;i<count;i++)
            {
                bool land=Data.land[i]>.5f;clearance[i]=land?10000:0;outside[i]=land?0:10000;shoreHeight[i]=land?Data.height[i]:.7f;
                if(land){low=Mathf.Min(low,Data.height[i]);high=Mathf.Max(high,Data.height[i]);}
            }
            for(int pass=0;pass<2;pass++)for(int iz=0;iz<size;iz++)for(int ix=0;ix<size;ix++)
            {
                int x=pass==0?ix:size-1-ix,z=pass==0?iz:size-1-iz,i=Data.Index(x,z),step=pass==0?-1:1;
                for(int k=0;k<4;k++)
                {
                    int nx=x+(k==0?step:k==1?0:k==2?step:-step),nz=z+(k==0?0:step);
                    if(nx<0||nz<0||nx>=size||nz>=size)continue;int j=Data.Index(nx,nz);float d=Data.cellSize*(k<2?1:1.414214f);
                    clearance[i]=Mathf.Min(clearance[i],clearance[j]+d);
                    if(outside[j]+d<outside[i]){outside[i]=outside[j]+d;shoreHeight[i]=shoreHeight[j];}
                }
            }
            if(high<low){low=high=.7f;}
            fogBaseRange=new Vector2(low-4,high+1.2f);
            var pixels=new Color[count];for(int i=0;i<count;i++)pixels[i]=new Color(Data.land[i]>.5f?clearance[i]:-outside[i],shoreHeight[i],0,1);
            landMask.SetPixels(pixels);landMask.Apply(false,false);
            var paintPixels=new Color32[count];var layerMap=new int[Data.surfaceIds.Count+1];
            for(int layer=1;layer<layerMap.Length;layer++)layerMap[layer]=palette.FindGround(Data.surfaceIds[layer-1]);
            for(int i=0;i<count;i++){int layer=Mathf.Clamp(Mathf.RoundToInt(Data.surfaceIndex[i]),0,layerMap.Length-1);int mapped=layer>0?layerMap[layer]:-1;paintPixels[i]=new Color32((byte)Mathf.Max(0,mapped),(byte)(mapped>=0?Mathf.RoundToInt(Data.surfaceBlend[i]*255):0),0,0);}
            paintMask.SetPixels32(paintPixels);paintMask.Apply(false,false);
            BuildSurfaceLayers();
        }

        void BuildSurfaceLayers()
        {
            int count=Data.land.Length,size=Data.resolution,radius=Mathf.Max(1,Mathf.CeilToInt(.95f/Data.cellSize));
            grassSoil=new float[count];contactShade=new float[count];contactLift=new float[count];var horizontal=new float[count];
            for(int z=0;z<size;z++)for(int x=0;x<size;x++)
            {
                float maximum=0;
                for(int dx=-radius;dx<=radius;dx++)
                {
                    int nx=x+dx;if(nx<0||nx>=size)continue;float value=Data.grass[Data.Index(nx,z)];
                    if(value>.025f)maximum=Mathf.Max(maximum,Mathf.Max(.35f,value)-Mathf.Abs(dx)/(float)radius*.17f);
                }
                horizontal[Data.Index(x,z)]=maximum;
            }
            for(int z=0;z<size;z++)for(int x=0;x<size;x++)
            {
                float maximum=0;
                for(int dz=-radius;dz<=radius;dz++){int nz=z+dz;if(nz>=0 && nz<size)maximum=Mathf.Max(maximum,horizontal[Data.Index(x,nz)]-Mathf.Abs(dz)/(float)radius*.17f);}
                int i=Data.Index(x,z);grassSoil[i]=Mathf.Clamp01(maximum)*(1-Mathf.SmoothStep(0,1,Data.water[i]/.16f));
            }
            foreach(var item in Data.objects)
            {
                int index=palette.Find(item.paletteId);if(index<0 || !item.naturalContact)continue;var entry=palette.entries[index];
                if(entry.category=="草花"||entry.category=="天空")continue;
                var half=WorldPlacementRules.Footprint(entry,item.scale)*.5f;float reach=half.magnitude+1;
                int x0=Mathf.Max(0,Mathf.FloorToInt((item.position.x-reach+Data.Width*.5f)/Data.cellSize)),x1=Mathf.Min(size-1,Mathf.CeilToInt((item.position.x+reach+Data.Width*.5f)/Data.cellSize));
                int z0=Mathf.Max(0,Mathf.FloorToInt((item.position.z-reach+Data.Width*.5f)/Data.cellSize)),z1=Mathf.Min(size-1,Mathf.CeilToInt((item.position.z+reach+Data.Width*.5f)/Data.cellSize));
                var inverse=Quaternion.Euler(0,-item.yaw,0);
                for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
                {
                    int i=Data.Index(x,z);var p=Data.Position(x,z);var local=inverse*(p-item.position);
                    float edge=entry.module?entry.module.DistanceToBody(local/item.scale)*item.scale:entry.category!="岩石"&&entry.category!="树木"?Mathf.Max(Mathf.Abs(local.x)-half.x,Mathf.Abs(local.z)-half.y):(new Vector2(local.x/Mathf.Max(.05f,half.x),local.z/Mathf.Max(.05f,half.y)).magnitude-1)*Mathf.Min(half.x,half.y);
                    float influence=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((edge+.1f)/.85f));
                    contactShade[i]=Mathf.Max(contactShade[i],influence);contactLift[i]=Mathf.Max(contactLift[i],influence*.035f);
                    if(entry.category!="岩石")grassSoil[i]*=1-influence*.55f;
                }
            }
        }

        Vertex Node(int x,int z,int mode)
        {
            int i=Data.Index(x,z);var p=Data.Position(x,z);
            float hL=Data.height[Data.Index(Mathf.Max(0,x-1),z)],hR=Data.height[Data.Index(Mathf.Min(Data.resolution-1,x+1),z)];
            float hB=Data.height[Data.Index(x,Mathf.Max(0,z-1))],hF=Data.height[Data.Index(x,Mathf.Min(Data.resolution-1,z+1))];
            var n=new Vector3(hL-hR,Data.cellSize*2,hB-hF).normalized;
            float value=Data.land[i]-.5f;
            if(mode==1){value=Mathf.Min(Data.land[i]-.5f,Mathf.Min(Data.water[i]-.04f,Data.waterHeight[i]-Data.height[i]-.03f));p.y=Data.waterHeight[i];n=Vector3.up;}
            else p.y+=contactLift[i];
            float boundary=Data.rockBoundary?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.20f,2.6f,clearance[i])):0;
            return new Vertex {p=p,n=n,value=value,grass=grassSoil[i],edge=Mathf.Max(boundary,contactShade[i])};
        }
        void Triangle(Vertex a,Vertex b,Vertex c,Geometry geo,Chunk chunk,bool captureBoundary)
        {
            polygon.Clear();var input=new[]{a,b,c};var previous=c;
            var crossings=captureBoundary?new List<Vector3>(2):null;
            foreach(var current in input)
            {
                if((previous.value>=0)!=(current.value>=0))
                {
                    var v=Vertex.Lerp(previous,current,previous.value/(previous.value-current.value));polygon.Add(v);crossings?.Add(v.p);
                }
                if(current.value>=0)polygon.Add(current);previous=current;
            }
            if(polygon.Count>=3)geo.Polygon(polygon);
            if(crossings!=null && crossings.Count==2 && Vector3.Distance(crossings[0],crossings[1])>.005f)chunk.edges.Add(crossings.ToArray());
        }
        void Rebuild(Vector2Int key)
        {
            int x0=key.x*ChunkCells,z0=key.y*ChunkCells,x1=Mathf.Min(Data.resolution-1,x0+ChunkCells),z1=Mathf.Min(Data.resolution-1,z0+ChunkCells);
            bool occupied=false;for(int z=z0;z<=z1&&!occupied;z++)for(int x=x0;x<=x1;x++)if(Data.land[Data.Index(x,z)]>0){occupied=true;break;}
            if(!occupied)
            {
                if(chunks.TryGetValue(key,out var empty)){foreach(var mesh in new[]{empty.ground,empty.side,empty.water})if(mesh)DisposeGenerated(mesh);empty.root.SetActive(false);DisposeGenerated(empty.root);chunks.Remove(key);}return;
            }
            if(!chunks.TryGetValue(key,out var chunk))
            {
                chunk=new Chunk {root=new GameObject("Map chunk "+key.x+","+key.y)};chunk.root.transform.SetParent(generatedRoot,false);chunks.Add(key,chunk);
            }
            foreach(var mesh in new[]{chunk.ground,chunk.side,chunk.water})if(mesh)DisposeGenerated(mesh);
            for(int i=chunk.root.transform.childCount-1;i>=0;i--){var go=chunk.root.transform.GetChild(i).gameObject;go.SetActive(false);DisposeGenerated(go);}
            chunk.edges.Clear();chunk.grassBatches.Clear();
            var ground=new Geometry();var water=new Geometry();var side=new Geometry();
            for(int z=z0;z<z1;z++)for(int x=x0;x<x1;x++)
            {
                for(int mode=0;mode<2;mode++)
                {
                    var a=Node(x,z,mode);var b=Node(x,z+1,mode);var c=Node(x+1,z+1,mode);var d=Node(x+1,z,mode);
                    var geometry=mode==0?ground:water;
                    if(mode==0){GroundTriangle(a,b,c,ground,side,chunk);GroundTriangle(a,c,d,ground,side,chunk);}else{Triangle(a,b,c,geometry,chunk,false);Triangle(a,c,d,geometry,chunk,false);}
                }
            }
            foreach(var edge in chunk.edges)
            {
                var a=edge[0];var b=edge[1];var n=Vector3.Cross(b-a,Vector3.down).normalized;
                var bottomA=a-Vector3.up*.65f;var bottomB=b-Vector3.up*.65f;
                side.Polygon(new List<Vertex>{new Vertex{p=a,n=n},new Vertex{p=b,n=n},new Vertex{p=bottomB,n=n},new Vertex{p=bottomA,n=n}});
            }
            chunk.ground=ground.Mesh("Map ground");chunk.side=side.Mesh("Shallow concealed foundation");chunk.water=water.Mesh("Pond surface");
            chunk.collider=Surface(chunk.root,"Ground",chunk.ground,terrainMaterial,true,false);
            if(chunk.collider)chunk.collider.gameObject.AddComponent<WorldInkTarget>().occluder=true;
            var cliff=Surface(chunk.root,"Rocky height transition",chunk.side,palette.cliffMaterial,true,false);if(cliff)cliff.gameObject.AddComponent<WorldInkTarget>().occluder=true;
            Surface(chunk.root,"Water",chunk.water,waterMaterial,false,false);
            BuildGrass(chunk,x0,z0,x1,z1);
        }
        void GroundTriangle(Vertex a,Vertex b,Vertex c,Geometry ground,Geometry cliff,Chunk chunk)
        {
            var normal=Vector3.Cross(b.p-a.p,c.p-a.p).normalized;bool steep=normal.y<.65f;if(steep){a.n=b.n=c.n=normal;}Triangle(a,b,c,steep?cliff:ground,chunk,true);
        }
        static MeshCollider Surface(GameObject parent,string name,Mesh mesh,Material mat,bool collide,bool outline)
        {
            if(mesh.vertexCount==0 || mesh.GetIndexCount(0)==0)return null;
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent.transform,false);go.layer=collide?28:0;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;
            if(outline)go.AddComponent<WorldInkTarget>();
            if(!collide)return null;var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=mesh;return collider;
        }
        void BuildGrass(Chunk chunk,int x0,int z0,int x1,int z1)
        {
            chunk.grassBatches.Clear();
            var batch=new List<Matrix4x4>(1023);
            for(int z=z0;z<z1;z++)for(int x=x0;x<x1;x++)for(int tuft=0;tuft<3;tuft++)
            {
                int i=Data.Index(x,z);uint hash=Hash(x*3+tuft,z,Data.seed);
                if((Hash(x*3+tuft,z,Data.seed^7919)%1000)/1000f>=Data.grassDensity)continue;
                if(Data.land[i]<.8f || Data.grass[i]<.05f || (hash%1000)/1000f>Data.grass[i] || Data.IsWet(Data.Position(x,z).x,Data.Position(x,z).z))continue;
                var p=Data.Position(x,z);p.x+=((hash>>8)%1000/1000f-.5f)*Data.cellSize;p.z+=((hash>>16)%1000/1000f-.5f)*Data.cellSize;
                // Leave room for the full tuft, not just its pivot, inside the rock rim.
                if(!Data.HasGround(p.x,p.z) || WorldPlacementRules.OnPondBank(Data,p.x,p.z,.35f) || Data.Sample(clearance,p.x,p.z)<2.05f)continue;
                // The source blades extend slightly below their pivot. Sink the tuft enough
                // that the stem base and contact shadow meet the interpolated ground.
                p.y=GroundHeight(p.x,p.z)-.04f;
                float scale=.68f+(hash%120)/400f;
                bool painted=false;
                for(int sample=0;sample<9;sample++){float angle=sample*Mathf.PI*.25f;float reach=sample==8?0:scale*.55f;if(Data.Sample(Data.surfaceBlend,p.x+Mathf.Cos(angle)*reach,p.z+Mathf.Sin(angle)*reach)>.04f){painted=true;break;}}
                if(painted)continue;
                if(OverlapsPlacedRock(p,scale*.45f))continue;
                batch.Add(Matrix4x4.TRS(p,Quaternion.Euler(0,hash%360,0),Vector3.one*scale));
                if(batch.Count==1023){chunk.grassBatches.Add(batch.ToArray());batch.Clear();}
            }
            if(batch.Count>0)chunk.grassBatches.Add(batch.ToArray());
        }
        static uint Hash(int x,int z,int seed){unchecked{uint h=(uint)(x*73856093^z*19349663^seed);h^=h>>16;h*=0x7feb352d;h^=h>>15;return h;}}
        bool OverlapsPlacedRock(Vector3 p,float margin)
        {
            if(solids.TryGetValue(new Vector2Int(Mathf.FloorToInt(p.x/8),Mathf.FloorToInt(p.z/8)),out var nearby))foreach(var solid in nearby)if(WorldPlacementRules.Contains(solid.entry,solid.item,p.x,p.z,margin))return true;
            return false;
        }
        void IndexPlacedObjects()
        {
            solids.Clear();foreach(var item in Data.objects)
            {
                int index=palette.Find(item.paletteId);if(index<0)continue;var entry=palette.entries[index];if(entry.editorOnly||entry.category=="草花"||entry.category=="天空")continue;
                var size=WorldPlacementRules.Footprint(entry,item.scale);if(item.exactTransform)size=Vector2.Scale(size,new Vector2(Mathf.Abs(item.axisScale.x),Mathf.Abs(item.axisScale.z)));
                float reach=size.magnitude*.5f+entry.FootprintOffset.magnitude*Mathf.Max(Mathf.Abs(item.AxisScale.x),Mathf.Abs(item.AxisScale.z))+.7f;
                for(int z=Mathf.FloorToInt((item.position.z-reach)/8);z<=Mathf.FloorToInt((item.position.z+reach)/8);z++)for(int x=Mathf.FloorToInt((item.position.x-reach)/8);x<=Mathf.FloorToInt((item.position.x+reach)/8);x++){var key=new Vector2Int(x,z);if(!solids.TryGetValue(key,out var list))solids[key]=list=new List<Solid>();list.Add(new Solid{item=item,entry=entry});}
            }
        }
        public void RefreshGrass(){foreach(var entry in chunks){int x=entry.Key.x*ChunkCells,z=entry.Key.y*ChunkCells;BuildGrass(entry.Value,x,z,Mathf.Min(Data.resolution-1,x+ChunkCells),Mathf.Min(Data.resolution-1,z+ChunkCells));}CountGrass();}

        public void RebuildBoundary()
        {
            ClearObjects(rim);
            if(!Data.rockBoundary || palette.boundaryRocks.Length==0)return;
            var all=new List<Vector3[]>();foreach(var chunk in chunks.Values)all.AddRange(chunk.edges);
            var lookup=new Dictionary<Vector2Int,List<int>>();
            for(int i=0;i<all.Count;i++)foreach(var p in all[i]){var k=PointKey(p);if(!lookup.TryGetValue(k,out var list))lookup[k]=list=new List<int>();list.Add(i);}
            var used=new bool[all.Count];int ordinal=0;
            for(int start=0;start<all.Count;start++)
            {
                if(used[start])continue;int current=start;var from=all[start][0];var contour=new List<Vector3>{from};
                for(int guard=0;guard<all.Count;guard++)
                {
                    if(used[current])break;used[current]=true;var edge=all[current];var to=PointKey(edge[0])==PointKey(from)?edge[1]:edge[0];
                    contour.Add(to);
                    from=to;if(!lookup.TryGetValue(PointKey(to),out var connected))break;
                    int next=-1;foreach(var id in connected)if(!used[id]){next=id;break;}if(next<0)break;current=next;
                }
                if(contour.Count<3)continue;
                bool closed=Vector3.Distance(contour[0],contour[contour.Count-1])<.02f;
                var arc=new float[contour.Count];for(int i=1;i<arc.Length;i++)arc[i]=arc[i-1]+Vector3.Distance(contour[i-1],contour[i]);
                float length=arc[arc.Length-1];if(length<.1f)continue;
                int count=Mathf.Max(3,Mathf.CeilToInt(length/2.8f));float spacing=length/count;
                int previous=-1,first=-1;
                for(int i=0;i<count;i++)
                {
                    uint h=Hash(ordinal++,start,Data.seed);var choices=new List<int>();
                    foreach(int index in palette.boundaryRocks)if(index!=previous && (!closed||i!=count-1||index!=first))choices.Add(index);
                    if(choices.Count==0)choices.AddRange(palette.boundaryRocks);
                    int selected=choices[(int)(h%(uint)choices.Count)];if(i==0)first=selected;previous=selected;
                    var entry=palette.entries[selected];float distance=(i+.5f)*spacing;
                    var position=Along(contour,arc,distance,closed);
                    var direction=(Along(contour,arc,distance+spacing*.45f,closed)-Along(contour,arc,distance-spacing*.45f,closed)).normalized;
                    float width=spacing*1.86f,scale=width/Mathf.Max(.1f,entry.boundarySpan);
                    var outward=new Vector3(-direction.z,0,direction.x);
                    if(Data.HasGround(position.x+outward.x*.5f,position.z+outward.z*.5f))outward=-outward;
                    float groundHeight=position.y;
                    position+=outward*.20f;position.y-=1.05f;
                    float heightScale=(1.95f+((h>>8)%100)/110f)/Mathf.Max(.1f,entry.bounds.size.y*scale);
                    float angle=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg+entry.boundaryYaw+((int)(h%13)-6);
                    var go=InstantiateEntry(entry,position,angle,scale,false,heightScale,groundHeight);
                    go.name="Boundary rock "+selected;rim.Add(go);
                }
            }
        }
        static Vector3 Along(List<Vector3> points,float[] arc,float distance,bool closed)
        {
            float length=arc[arc.Length-1];distance=closed?Mathf.Repeat(distance,length):Mathf.Clamp(distance,0,length);
            int low=0,high=arc.Length-1;while(high-low>1){int mid=(low+high)/2;if(arc[mid]<distance)low=mid;else high=mid;}
            return Vector3.Lerp(points[low],points[high],Mathf.InverseLerp(arc[low],arc[high],distance));
        }
        Vector2Int PointKey(Vector3 p)=>new Vector2Int(Mathf.RoundToInt(p.x*10000),Mathf.RoundToInt(p.z*10000));
        public void RebuildObjects()
        {
            IndexPlacedObjects();
            ClearObjects(placed);
            foreach(var item in Data.objects)
            {
                int index=palette.Find(item.paletteId);if(index<0){Debug.LogWarning("地图素材暂不可用："+item.paletteId);continue;}
                if(palette.entries[index].editorOnly&&!GetComponent<WorldMapEditorController>())continue;
                var go=InstantiateEntry(palette.entries[index],item.position,item.yaw,item.scale,true,1,item.naturalContact?item.contactHeight:item.position.y);
                if(item.exactTransform){go.transform.rotation=item.Orientation;go.transform.localScale=item.AxisScale;}
                if(!item.naturalContact && palette.entries[index].category=="岩石")foreach(var renderer in go.GetComponentsInChildren<Renderer>()){var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);block.SetFloat("_RockBlend",0);renderer.SetPropertyBlock(block);}
                go.AddComponent<MapPlacedObject>().instanceId=item.instanceId;placed.Add(go);
            }
            foreach(var entry in chunks)
            {
                int x=entry.Key.x*ChunkCells,z=entry.Key.y*ChunkCells;
                BuildGrass(entry.Value,x,z,Mathf.Min(Data.resolution-1,x+ChunkCells),Mathf.Min(Data.resolution-1,z+ChunkCells));
            }
            CountGrass();
        }
        GameObject InstantiateEntry(WorldPaletteEntry entry,Vector3 position,float yaw,float scale,bool collider,float heightScale=1,float rootPlane=float.NaN)
        {
            var holder=new GameObject(entry.label);holder.transform.SetParent(generatedRoot,false);holder.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));holder.transform.localScale=new Vector3(scale,scale*heightScale,scale);
            var model=Instantiate(entry.prefab,holder.transform,false);
            foreach(var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true))DisposeGenerated(behaviour);
            foreach(var body in model.GetComponentsInChildren<Rigidbody>(true))DisposeGenerated(body);
            foreach(var light in model.GetComponentsInChildren<Light>(true))DisposeGenerated(light);
            var renderers=model.GetComponentsInChildren<Renderer>(true);int materialIndex=0;
            foreach(var r in renderers){var original=r.sharedMaterials;bool effect=r is ParticleSystemRenderer||r is LineRenderer||r is TrailRenderer;for(int j=0;j<original.Length;j++){if(materialIndex<entry.materials.Length&&!(effect&&!useURP))original[j]=entry.materials[materialIndex];materialIndex++;}r.sharedMaterials=original;}
            Bounds bounds=new Bounds();bool found=false;foreach(var r in renderers){if(!found){bounds=r.bounds;found=true;}else bounds.Encapsulate(r.bounds);}
            if(found)model.transform.position+=entry.module||entry.preservePivot?Vector3.zero:position-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            if(entry.category=="岩石")
            {
                var block=new MaterialPropertyBlock();block.SetFloat("_RockRootHeight",float.IsNaN(rootPlane)?position.y:rootPlane);block.SetFloat("_RootMoss",Data.Sample(Data.grass,position.x,position.z));
                foreach(var r in renderers)r.SetPropertyBlock(block);
            }
            foreach(var c in model.GetComponentsInChildren<Collider>(true))c.enabled=collider;
            if(collider && model.GetComponentsInChildren<Collider>(true).Length==0 && found)
            {
                if(entry.category=="树木"){var trunk=holder.AddComponent<CapsuleCollider>();trunk.height=entry.bounds.size.y;trunk.center=Vector3.up*trunk.height*.5f;trunk.radius=Mathf.Clamp(Mathf.Max(entry.bounds.size.x,entry.bounds.size.z)*.04f,.12f,.45f);}
                else if(entry.category=="建筑"||entry.category=="桥道"||entry.category=="岩石"){foreach(var mesh in model.GetComponentsInChildren<MeshFilter>(true))if(mesh.sharedMesh)mesh.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh.sharedMesh;}
                else{var box=holder.AddComponent<BoxCollider>();box.center=Vector3.up*entry.bounds.size.y*.5f;box.size=entry.bounds.size;}
            }
            foreach(var t in holder.GetComponentsInChildren<Transform>(true))t.gameObject.layer=29;
            var inkTarget=holder.AddComponent<WorldInkTarget>();inkTarget.树木=entry.category=="树木";
            if(inkTarget.树木){inkTarget.接地高度=float.IsNaN(rootPlane)?position.y:rootPlane;inkTarget.根部淡化高度=Mathf.Clamp(bounds.size.y*.06f,.18f,.65f);}
            return holder;
        }
        void ClearObjects(List<GameObject> list){foreach(var go in list)if(go){go.SetActive(false);DisposeGenerated(go);}list.Clear();}
        void Clear()
        {
            foreach(var chunk in chunks.Values){foreach(var mesh in new[]{chunk.ground,chunk.side,chunk.water})if(mesh)DisposeGenerated(mesh);if(chunk.root){chunk.root.SetActive(false);DisposeGenerated(chunk.root);}}
            chunks.Clear();dirty.Clear();ClearObjects(placed);ClearObjects(rim);
            if(generatedRoot){generatedRoot.gameObject.SetActive(false);DisposeGenerated(generatedRoot.gameObject);generatedRoot=null;}
            if(landMask)DisposeGenerated(landMask);if(paintMask)DisposeGenerated(paintMask);if(terrainMaterial)DisposeGenerated(terrainMaterial);clearance=null;shoreHeight=null;Data=null;
        }
        void OnEnable()
        {
            // Unity hot reload retains map data and scene objects, but resets readonly dictionaries.
            if(Data!=null && palette && chunks.Count==0){var data=Data;var library=palette;Configure(library,data);RebuildAll();}
        }
        static void DisposeGenerated(UnityEngine.Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
        void OnDestroy(){Clear();}
    }
}

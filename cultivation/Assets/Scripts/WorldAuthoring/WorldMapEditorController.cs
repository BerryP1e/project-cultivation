using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cultivation.WorldAuthoring
{
    public sealed class WorldMapEditorController : MonoBehaviour
    {
        public WorldMapPalette palette;
        public Camera editorCamera;
        public WorldMapRenderer world;
        public WorldBrush tool=WorldBrush.Ground;
        public float radius=3,elevation=.7f,pondDepth=1.8f,strength=1,yaw,objectScale=1;
        public int selectedEntry;
        public int selectedGroundLayer;
        public bool gridSnap=true;
        public float gridSize=1,rotationStep=90;
        public string SelectedInstanceId {get;private set;}
        public MapObjectData SelectedObject=>Data?.objects.Find(x=>x.instanceId==SelectedInstanceId);
        public bool MovingSelection {get;private set;}
        public WorldPlayerReference PlayerReference {get;private set;}
        WorldPlacementGrid placementGrid;
        Vector3 referencePosition;
        float referenceYaw;
        GameObject hiddenSelection;
        bool densityEditing;
        public bool naturalPlacement=true;
        public float placementHeight;
        public bool GameView {get;private set;}
        public string PlacementHint {get;private set;}="";
        public WorldPlacementPreview PlacementPreview {get;private set;}
        public string Status {get;private set;}="左键绘制 · 右键旋转 · 中键平移 · 滚轮缩放";
        public bool Dirty {get;private set;}
        public WorldMapData Data=>world?world.Data:null;
        public readonly WorldMapHistory History=new WorldMapHistory();
        public static event Action<WorldMapData,WorldMapPalette> ExportRequested;
        public static event Action<WorldMapData> MapSaved;
        LineRenderer cursor;
        Material cursorMaterial;
        Vector3 cameraFocus=new Vector3(0,0,0),lastStamp;
        float cameraYaw=35,cameraPitch=52,distance=32,pondLevel;
        Vector3 savedFocus;
        Vector4 savedCamera;
        float savedFov;
        bool stroke,stampMoved;
        WorldMapData strokeStart;
        Vector3 pointer;
        public bool HasPointer {get;private set;}

        void Start()
        {
            Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
            if(!world)world=GetComponent<WorldMapRenderer>()??gameObject.AddComponent<WorldMapRenderer>();
            var ink=editorCamera.GetComponent<WorldInkCamera>();if(ink)ink.world=world;
            var contours=editorCamera.GetComponent<WorldMapInk>()??editorCamera.gameObject.AddComponent<WorldMapInk>();contours.world=world;
            var fog=editorCamera.GetComponent<WorldMapCamera>()??editorCamera.gameObject.AddComponent<WorldMapCamera>();fog.world=world;
            WorldMapLook.Apply(palette,editorCamera,true);
            WorldMapData session=null;
            try{session=WorldMapStorage.LastSession();}catch(Exception exception){Debug.LogWarning("上次编辑恢复失败："+exception.Message);}
            var initial=session??WorldMapData.SampleMap();if(initial.resolution<513)initial=initial.Expanded(513);SetData(initial);
            if(session!=null){Dirty=true;Status="已恢复上次编辑；Ctrl+S 保存地图";}
            PlacementPreview=new WorldPlacementPreview();
            PlayerReference=new WorldPlayerReference();placementGrid=new WorldPlacementGrid();
            cursor=new GameObject("Brush preview",typeof(LineRenderer)).GetComponent<LineRenderer>();cursor.transform.SetParent(transform,false);
            cursorMaterial=new Material(Shader.Find("Sprites/Default"));cursor.sharedMaterial=cursorMaterial;cursor.useWorldSpace=true;cursor.loop=true;cursor.widthMultiplier=.055f;cursor.positionCount=64;cursor.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            UpdateCamera();
            var ui=GetComponent<WorldMapEditorUI>()??gameObject.AddComponent<WorldMapEditorUI>();ui.Initialize(this);
        }
        public void SetData(WorldMapData data,bool changed=false)
        {
            data.Validate();bool upgraded=false;
            foreach(var item in data.objects)
            {
                if(item.sizingVersion>=1)continue;int index=palette.Find(item.paletteId);if(index<0)continue;var entry=palette.entries[index];
                float oldScale=item.scale;item.scale=Mathf.Clamp(item.scale*entry.defaultScale/Mathf.Max(.001f,entry.legacyScale),.05f,20);item.sizingVersion=1;upgraded=true;
                if(item.naturalContact&&entry.category=="岩石")item.position.y+=Mathf.Max(.1f,entry.bounds.size.y*oldScale*.22f)-Mathf.Max(.1f,entry.bounds.size.y*item.scale*.22f);
            }
            SelectedInstanceId=null;MovingSelection=false;data.Validate();world.Configure(palette,data);world.RebuildAll();History.Clear();Dirty=upgraded||changed;
            editorCamera.farClipPlane=Mathf.Max(220,data.Width*2);
        }
        void UpdateCamera()
        {
            var rotation=Quaternion.Euler(cameraPitch,cameraYaw,0);editorCamera.transform.position=cameraFocus+rotation*new Vector3(0,0,-distance);editorCamera.transform.rotation=rotation;
        }
        bool Typing()=>EventSystem.current && EventSystem.current.currentSelectedGameObject && EventSystem.current.currentSelectedGameObject.GetComponent<InputField>();
        bool OverUI()=>EventSystem.current && EventSystem.current.IsPointerOverGameObject();
        public void ToggleGameView()
        {
            if(stroke)EndStroke();
            GameView=!GameView;
            if(GameView)
            {
                savedFocus=cameraFocus;savedCamera=new Vector4(cameraYaw,cameraPitch,distance,0);savedFov=editorCamera.fieldOfView;
                CancelMove();var focus=HasPointer?pointer:cameraFocus;
                focus=FindReferencePosition(focus);
                referencePosition=new Vector3(focus.x,world.GroundHeight(focus.x,focus.z),focus.z);referenceYaw=cameraYaw;
                PlayerReference?.Show(palette,referencePosition,referenceYaw);focus=referencePosition+Vector3.up*palette.gameLookHeight;
                cameraFocus=focus;cameraYaw=palette.gameYaw;cameraPitch=palette.gamePitch;distance=palette.gameDistance;editorCamera.fieldOfView=palette.gameFov;
                PlacementPreview?.Hide();if(cursor)cursor.enabled=false;Status="游戏视角 · WASD 平移查看 · 点击编辑视角返回";
            }
            else{PlayerReference?.Hide();cameraFocus=savedFocus;cameraYaw=savedCamera.x;cameraPitch=savedCamera.y;distance=savedCamera.z;editorCamera.fieldOfView=savedFov;Status="已返回编辑视角";}
            UpdateCamera();
        }
        public void FocusSpawn(){cameraFocus=Data.spawn;if(GameView){var point=FindReferencePosition(Data.spawn);referencePosition=new Vector3(point.x,world.GroundHeight(point.x,point.z),point.z);PlayerReference?.Show(palette,referencePosition,referenceYaw);cameraFocus=referencePosition+Vector3.up*palette.gameLookHeight;}UpdateCamera();}
        void Update()
        {
            if(Data==null)return;
            bool typing=Typing(),over=OverUI();
            if(!Input.GetMouseButton(0))densityEditing=false;
            if(!typing && Input.GetKeyDown(KeyCode.G))ToggleGameView();
            if(!typing)
            {
                if(Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl))
                {
                    if(Input.GetKeyDown(KeyCode.Z))Undo();if(Input.GetKeyDown(KeyCode.Y))Redo();if(Input.GetKeyDown(KeyCode.S))Save();
                }
                else
                {
                    var forward=Quaternion.Euler(0,cameraYaw,0)*Vector3.forward;var right=Quaternion.Euler(0,cameraYaw,0)*Vector3.right;
                    float speed=(GameView?4:distance*.55f)*Time.unscaledDeltaTime;var move=Vector3.zero;
                    if(Input.GetKey(KeyCode.W))move+=forward;if(Input.GetKey(KeyCode.S))move-=forward;if(Input.GetKey(KeyCode.A))move-=right;if(Input.GetKey(KeyCode.D))move+=right;
                    if(GameView)MoveReference(Vector3.ClampMagnitude(move,1)*speed);else cameraFocus+=move*speed;
                    if(Input.GetKeyDown(KeyCode.Q))RotateSelection(-1);if(Input.GetKeyDown(KeyCode.E))RotateSelection(1);
                    if(Input.GetKeyDown(KeyCode.Delete))DeleteSelected();if(Input.GetKeyDown(KeyCode.Escape))CancelMove();
                    if(Input.GetKeyDown(KeyCode.LeftBracket))radius=Mathf.Max(.5f,radius-.5f);if(Input.GetKeyDown(KeyCode.RightBracket))radius=Mathf.Min(12,radius+.5f);
                }
            }
            if(!over)
            {
                if(!TreasureSkillHud.占用滚轮)distance=Mathf.Clamp(distance-Input.mouseScrollDelta.y*distance*.08f,5,Mathf.Max(100,Data.Width*1.2f));
                if(!GameView && Input.GetMouseButton(1)){cameraYaw+=Input.GetAxis("Mouse X")*3;cameraPitch=Mathf.Clamp(cameraPitch-Input.GetAxis("Mouse Y")*2,25,78);}
                if(!GameView&&Input.GetMouseButton(2)){var right=editorCamera.transform.right;var up=Vector3.ProjectOnPlane(editorCamera.transform.up,Vector3.up).normalized;cameraFocus-=(right*Input.GetAxis("Mouse X")+up*Input.GetAxis("Mouse Y"))*distance*.018f;}
            }
            UpdateCamera();HasPointer=!over && TryPoint(out pointer);
            cursor.enabled=HasPointer && !GameView && tool!=WorldBrush.Place&&tool!=WorldBrush.SelectObject;
            if(cursor.enabled)for(int i=0;i<64;i++){float angle=i*Mathf.PI*2/64;float x=pointer.x+Mathf.Cos(angle)*radius,z=pointer.z+Mathf.Sin(angle)*radius;float y=Data.HasGround(x,z)?world.GroundHeight(x,z)+.035f:elevation+.035f;cursor.SetPosition(i,new Vector3(x,y,z));}
            if(GameView){placementGrid?.Hide();PlacementPreview?.Hide();PlayerReference?.Tick(Time.unscaledDeltaTime);return;}
            if(gridSnap&&HasPointer&&(tool==WorldBrush.Place||MovingSelection))placementGrid?.Show(world,pointer,gridSize);else placementGrid?.Hide();
            if((tool==WorldBrush.Place||MovingSelection) && HasPointer && palette.entries.Length>0)
            {
                var entry=palette.entries[Mathf.Clamp(selectedEntry,0,palette.entries.Length-1)];
                bool valid=EvaluatePlacement(entry,pointer,out var placedPosition,out _,out var message);
                float scale=PlacementScale(entry);var size=entry.bounds.size*scale;
                PlacementHint=entry.label+" · "+size.x.ToString("0.#")+"×"+size.y.ToString("0.#")+"×"+size.z.ToString("0.#")+"米 · "+message;PlacementPreview.Show(entry,placedPosition,yaw,scale,valid);
            }
            else{PlacementPreview?.Hide();var item=SelectedObject;PlacementHint=tool==WorldBrush.SelectObject&&item!=null?"选中："+palette.entries[palette.Find(item.paletteId)].label+" · 旋转或移动选中":"";}
            if(HasPointer && !typing && Input.GetMouseButtonDown(0))BeginStroke(pointer);
            if(stroke && Input.GetMouseButton(0) && HasPointer)
            {
                bool moved=!stampMoved || Vector3.ProjectOnPlane(pointer-lastStamp,Vector3.up).magnitude>Mathf.Max(.12f,radius*.12f);
                if(tool==WorldBrush.Raise || tool==WorldBrush.Lower || tool==WorldBrush.Smooth || tool==WorldBrush.Grass||tool==WorldBrush.Surface||tool==WorldBrush.EraseSurface)Stamp(pointer,Time.unscaledDeltaTime*strength*3);
                else if(moved && tool!=WorldBrush.Place && tool!=WorldBrush.EraseObject && tool!=WorldBrush.Spawn){Stamp(pointer,1);lastStamp=pointer;stampMoved=true;}
            }
            if(stroke && Input.GetMouseButtonUp(0))EndStroke();
        }
        bool TryPoint(out Vector3 point)
        {
            var ray=editorCamera.ScreenPointToRay(Input.mousePosition);
            if(Physics.Raycast(ray,out var hit,5000,1<<28)){point=hit.point;return true;}
            var plane=new Plane(Vector3.up,new Vector3(0,elevation,0));if(plane.Raycast(ray,out float t)){point=ray.GetPoint(t);return Mathf.Abs(point.x)<Data.Width*.5f && Mathf.Abs(point.z)<Data.Width*.5f;}point=default;return false;
        }
        public void BeginStroke(Vector3 point)
        {
            if(GameView)return;
            if(MovingSelection){CommitMove(point);return;}
            if(tool==WorldBrush.SelectObject){var ray=editorCamera.ScreenPointToRay(Input.mousePosition);if(Physics.Raycast(ray,out var hit,5000,1<<29)){var target=hit.collider.GetComponentInParent<MapPlacedObject>();if(target)SelectObject(target.instanceId);}return;}
            if(tool==WorldBrush.Place){Place(point);return;}
            if(stroke)EndStroke();History.Push(Data);strokeStart=Data.Clone();stroke=true;stampMoved=false;
            pondLevel=Data.IsWet(point.x,point.z)?Data.Sample(Data.waterHeight,point.x,point.z):Data.Sample(Data.height,point.x,point.z)-.35f;
            if(tool==WorldBrush.EraseObject)Erase(point);
            else if(tool==WorldBrush.Spawn){if(Data.HasGround(point.x,point.z)&&!Data.IsWet(point.x,point.z)){Data.spawn=point+Vector3.up*.15f;Dirty=true;}}
        }
        public void Stamp(Vector3 point,float amount)
        {
            bool subtract=Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift);
            if(tool==WorldBrush.Pond && !subtract)
            {
                foreach(var item in Data.objects)
                {
                    int index=palette.Find(item.paletteId);if(index<0||palette.entries[index].IsBridge)continue;var size=WorldPlacementRules.Footprint(palette.entries[index],item.scale);
                    float reach=radius+size.magnitude*.5f+.4f;
                    if(Vector2.Distance(new Vector2(point.x,point.z),new Vector2(item.position.x,item.position.z))<reach){Status="水池不能挖到已有素材下方，请先移开素材";return;}
                }
            }
            string layer=palette.groundLayers.Length>0?palette.groundLayers[Mathf.Clamp(selectedGroundLayer,0,palette.groundLayers.Length-1)].id:null;
            var rect=Data.Paint(tool,point,radius,amount,elevation,pondDepth,pondLevel,subtract,layer);world.Mark(rect);Dirty=true;
        }
        public void EndStroke(){if(!stroke)return;stroke=false;strokeStart=null;world.Flush();Status="笔画已完成，可撤销";}
        public void Place(Vector3 point)
        {
            if(GameView || palette.entries.Length==0)return;
            var entry=palette.entries[Mathf.Clamp(selectedEntry,0,palette.entries.Length-1)];
            if(!EvaluatePlacement(entry,point,out var position,out float contactHeight,out var message)){Status=message;return;}
            History.Push(Data);float scale=PlacementScale(entry);
            Data.objects.Add(new MapObjectData {instanceId=Guid.NewGuid().ToString("N"),paletteId=entry.id,position=position,yaw=yaw,scale=scale,naturalContact=naturalPlacement,contactHeight=contactHeight,sizingVersion=1});
            world.RefreshPlacedObjects(position,WorldPlacementRules.Footprint(entry,scale).magnitude*.5f+2);Dirty=true;Status="已放置："+entry.label;
        }
        bool EvaluatePlacement(WorldPaletteEntry entry,Vector3 point,out Vector3 position,out float contactHeight,out string message)
        {
            float scale=PlacementScale(entry);
            if(gridSnap){point.x=Mathf.Round(point.x/gridSize)*gridSize;point.z=Mathf.Round(point.z/gridSize)*gridSize;}
            Vector3 aligned=point;bool joined=gridSnap&&entry.module&&WorldModuleSnapping.TrySnap(Data,palette,entry,point,yaw,scale,MovingSelection?SelectedInstanceId:null,out aligned);
            float jointHeight=joined?aligned.y:0;if(joined)point=aligned;
            if(naturalPlacement&&!gridSnap)point=WorldPlacementRules.JoinNearbyRock(Data,palette,entry,point,yaw,scale,MovingSelection?SelectedInstanceId:null);
            bool valid=WorldPlacementRules.Evaluate(Data,entry,point,yaw,scale,naturalPlacement,placementHeight,out position,out contactHeight,out message);
            if(joined&&valid){if(Mathf.Abs(position.y-jointHeight)>.35f){message="墙段地面高差过大，请先平整";return false;}position.y=jointHeight;message="墙身端口已对齐 · 点击拼接";}return valid;
        }
        public float PlacementScale(WorldPaletteEntry entry)=>entry.lockScale?(MovingSelection&&SelectedObject!=null?SelectedObject.scale:entry.defaultScale):Mathf.Clamp(objectScale*entry.defaultScale,.05f,20);
        void MoveReference(Vector3 delta)
        {
            if(delta.sqrMagnitude<.000001f)return;var next=referencePosition+delta;
            if(!Data.HasGround(next.x,next.z)||WorldPlacementRules.OnPondBank(Data,next.x,next.z,.18f))return;
            next.y=world.GroundHeight(next.x,next.z);if(Mathf.Abs(next.y-referencePosition.y)>.65f)return;
            if(Physics.CheckCapsule(next+Vector3.up*.35f,next+Vector3.up*Mathf.Max(.4f,palette.playerHeight-.3f),.28f,1<<29,QueryTriggerInteraction.Ignore))return;
            referencePosition=next;referenceYaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
            PlayerReference?.Show(palette,referencePosition,referenceYaw);cameraFocus=referencePosition+Vector3.up*palette.gameLookHeight;
        }
        bool ReferenceFits(Vector3 point)
        {
            if(!Data.HasGround(point.x,point.z)||WorldPlacementRules.OnPondBank(Data,point.x,point.z,.3f))return false;
            point.y=world.GroundHeight(point.x,point.z);
            return !Physics.CheckCapsule(point+Vector3.up*.35f,point+Vector3.up*Mathf.Max(.4f,palette.playerHeight-.3f),.28f,1<<29,QueryTriggerInteraction.Ignore);
        }
        Vector3 FindReferencePosition(Vector3 preferred)
        {
            if(ReferenceFits(preferred))return preferred;Vector3 best=Data.spawn;float distance=float.MaxValue;
            for(int z=0;z<Data.resolution;z++)for(int x=0;x<Data.resolution;x++)
            {
                int i=Data.Index(x,z);if(Data.land[i]<.95f||Data.water[i]>.015f)continue;var point=Data.Position(x,z);float candidate=new Vector2(point.x-preferred.x,point.z-preferred.z).sqrMagnitude;
                if(candidate>=distance||!Data.HasGround(point.x+.6f,point.z)||!Data.HasGround(point.x-.6f,point.z)||!Data.HasGround(point.x,point.z+.6f)||!Data.HasGround(point.x,point.z-.6f)||!ReferenceFits(point))continue;
                distance=candidate;best=point;
            }
            return best;
        }
        public void ChooseAsset(int index)
        {
            CancelMove();selectedEntry=Mathf.Clamp(index,0,palette.entries.Length-1);SelectedInstanceId=null;objectScale=1;tool=WorldBrush.Place;
            var entry=palette.entries[selectedEntry];rotationStep=entry.category=="建筑"||entry.category=="桥道"?90:15;yaw=Mathf.Round(yaw/rotationStep)*rotationStep;
            if(entry.module){gridSize=1;rotationStep=90;yaw=Mathf.Round(yaw/90)*90;}
            Status=entry.label+" · Q/E 或旋转按钮调整朝向 · "+(gridSnap?"网格吸附":"自由摆放");
        }
        public void SelectObject(string id)
        {
            CancelMove();SelectedInstanceId=id;var item=SelectedObject;if(item==null)return;selectedEntry=palette.Find(item.paletteId);if(selectedEntry<0){SelectedInstanceId=null;return;}
            var entry=palette.entries[selectedEntry];tool=WorldBrush.SelectObject;yaw=item.yaw;objectScale=item.scale/Mathf.Max(.001f,entry.defaultScale);naturalPlacement=item.naturalContact;
            float bury=entry.category=="岩石"?Mathf.Max(.1f,entry.bounds.size.y*item.scale*.22f):entry.category=="树木"?.1f:.035f;
            placementHeight=item.position.y-item.contactHeight+(naturalPlacement?bury:0);rotationStep=entry.category=="建筑"||entry.category=="桥道"?90:15;Status="已选中："+entry.label+" · 旋转 / 移动 / 删除";
        }
        public void RotateSelection(int direction){SetYaw(yaw+rotationStep*direction);}
        public void SetYaw(float angle)
        {
            if(GameView)return;if(gridSnap&&palette.entries[Mathf.Clamp(selectedEntry,0,palette.entries.Length-1)].module)angle=Mathf.Round(angle/90)*90;angle=Mathf.Repeat(angle,360);var item=SelectedObject;
            if(tool==WorldBrush.SelectObject&&item!=null&&!MovingSelection)
            {
                var entry=palette.entries[palette.Find(item.paletteId)];
                if(!WorldPlacementRules.Evaluate(Data,entry,item.position,angle,item.scale,item.naturalContact,0,out _,out _,out var reason)){Status=reason;return;}
                History.Push(Data);item.yaw=angle;world.RefreshPlacedObjects(item.position,WorldPlacementRules.Footprint(entry,item.scale).magnitude+2);Dirty=true;
            }
            yaw=angle;Status="朝向 "+yaw.ToString("0.#")+"°";
        }
        public void BeginMoveSelected()
        {
            if(GameView||SelectedObject==null){Status="先用选择物件点击已摆放的素材";return;}
            MovingSelection=true;tool=WorldBrush.SelectObject;
            foreach(var target in FindObjectsOfType<MapPlacedObject>())if(target.instanceId==SelectedInstanceId){hiddenSelection=target.gameObject;foreach(var renderer in hiddenSelection.GetComponentsInChildren<Renderer>())renderer.enabled=false;break;}
            Status="移动预览 · 左键确认 · Esc 取消";
        }
        public void CancelMove()
        {
            if(hiddenSelection)foreach(var renderer in hiddenSelection.GetComponentsInChildren<Renderer>())renderer.enabled=true;hiddenSelection=null;MovingSelection=false;PlacementPreview?.Hide();
        }
        void CommitMove(Vector3 point)
        {
            var item=SelectedObject;if(item==null){CancelMove();return;}var entry=palette.entries[palette.Find(item.paletteId)];
            if(!EvaluatePlacement(entry,point,out var position,out float contact,out var reason)){Status=reason;return;}
            float scale=PlacementScale(entry);History.Push(Data);var previous=item.position;item.position=position;item.yaw=yaw;item.scale=scale;item.naturalContact=naturalPlacement;item.contactHeight=contact;item.sizingVersion=1;
            CancelMove();world.Mark(new RectInt(0,0,Data.resolution,Data.resolution));world.Flush();Dirty=true;Status="物件位置已更新";
        }
        public void DeleteSelected()
        {
            var item=SelectedObject;if(GameView||item==null)return;var entry=palette.entries[palette.Find(item.paletteId)];float reach=WorldPlacementRules.Footprint(entry,item.scale).magnitude+2;History.Push(Data);CancelMove();Data.objects.Remove(item);Data.features.RemoveAll(x=>x.objectId==item.instanceId);SelectedInstanceId=null;world.RefreshPlacedObjects(item.position,reach);Dirty=true;Status="已删除选中物件";
        }
        public void ExpandMap()
        {
            if(stroke)EndStroke();if(Data.resolution>=WorldMapData.MaxResolution){Status="当前精度已达 "+Data.Width.ToString("0")+" 米；可在地图设置中创建更大范围的新地图";return;}
            var expanded=Data.Expanded((Data.resolution-1)*2+1);History.Push(Data);CancelMove();world.Configure(palette,expanded);world.RebuildAll();editorCamera.farClipPlane=Mathf.Max(220,expanded.Width*2);Dirty=true;Status="地图已扩展到 "+expanded.Width.ToString("0")+" × "+expanded.Width.ToString("0")+" 米，原内容坐标保留";
        }
        public void SetFog(float height,float speed,float density){Data.fogHeight=Mathf.Clamp(height,-3,.5f);Data.fogSpeed=Mathf.Clamp(speed,0,3);Data.fogDensity=Mathf.Clamp(density,.2f,2);Dirty=true;}
        public void SetGrassDensity(float density){density=Mathf.Clamp01(density);if(Mathf.Abs(Data.grassDensity-density)<.001f)return;if(!densityEditing){History.Push(Data);densityEditing=true;}Data.grassDensity=density;world.RefreshGrass();Dirty=true;Status="草密度 "+(density*100).ToString("0")+"%";}
        public void ToggleGridSnap(){gridSnap=!gridSnap;if(gridSnap&&palette.entries[Mathf.Clamp(selectedEntry,0,palette.entries.Length-1)].module){yaw=Mathf.Round(yaw/90)*90;rotationStep=90;gridSize=1;}}
        public void AlignWalls()
        {
            if(GameView)return;CancelMove();var repaired=Data.Clone();int changed=0;
            foreach(var item in repaired.objects)
            {
                int index=palette.Find(item.paletteId);if(index<0||!palette.entries[index].module)continue;var entry=palette.entries[index];float angle=Mathf.Round(item.yaw/90)*90;
                if(WorldModuleSnapping.TrySnap(repaired,palette,entry,item.position,angle,item.scale,item.instanceId,out var position)&&Vector3.Distance(position,item.position)>.005f&&Mathf.Abs(position.y-item.position.y)<.35f)
                {
                    if(!WorldPlacementRules.Evaluate(repaired,entry,position,angle,item.scale,item.naturalContact,0,out _,out _,out _))continue;item.position=position;item.yaw=angle;changed++;
                }
            }
            if(changed==0){Status="附近墙段已对齐，或距离过远；可用移动选中接到端口";return;}History.Push(Data);world.Configure(palette,repaired);world.RebuildAll();Dirty=true;Status="已整理 "+changed+" 段墙，可撤销";
        }
        public void Erase(Vector3 point)
        {
            int closest=-1;float best=radius*radius;
            for(int i=0;i<Data.objects.Count;i++){float d=Vector2.SqrMagnitude(new Vector2(point.x-Data.objects[i].position.x,point.z-Data.objects[i].position.z));if(d<best){best=d;closest=i;}}
            if(closest>=0){var item=Data.objects[closest];int index=palette.Find(item.paletteId);float reach=index>=0?WorldPlacementRules.Footprint(palette.entries[index],item.scale).magnitude*.5f+2:8;Data.objects.RemoveAt(closest);Data.features.RemoveAll(x=>x.objectId==item.instanceId);world.RefreshPlacedObjects(item.position,reach);Dirty=true;Status="物件已移除";}
        }
        public void Undo(){if(GameView)return;if(stroke)EndStroke();if(History.UndoCount==0)return;CancelMove();var data=History.Undo(Data);world.Configure(palette,data);world.RebuildAll();if(tool==WorldBrush.SelectObject&&SelectedObject!=null)SelectObject(SelectedInstanceId);Dirty=true;Status="已撤销";}
        public void Redo(){if(GameView)return;if(stroke)EndStroke();if(History.RedoCount==0)return;CancelMove();var data=History.Redo(Data);world.Configure(palette,data);world.RebuildAll();if(tool==WorldBrush.SelectObject&&SelectedObject!=null)SelectObject(SelectedInstanceId);Dirty=true;Status="已重做";}
        public void Save(){try{if(stroke)EndStroke();WorldFeatureRuntime.Synchronize(Data);var path=WorldMapStorage.Save(Data);MapSaved?.Invoke(Data);Status=string.IsNullOrEmpty(Data.sourceScene)?"已保存："+path:"已保存；停止工坊后自动更新游戏地图："+Data.name;Dirty=false;}catch(Exception e){Dirty=true;Status=e.Message;Debug.LogWarning(Status);}}
        public void AssignFeature(string templateId)
        {
            var item=SelectedObject;if(item==null){Status="先选择一个物件，再赋予功能";return;}var template=palette.Feature(templateId);if(!template)return;History.Push(Data);
            Data.features.RemoveAll(x=>x.objectId==item.instanceId);var id=Guid.NewGuid().ToString("N");Data.features.Add(new WorldFeatureData{id=id,templateId=templateId,label=template.label,objectId=item.instanceId,position=item.position,rotation=item.Orientation.eulerAngles,scale=item.AxisScale});item.featureId=id;Dirty=true;Status="已赋予功能："+template.label;
        }
        public void ClearFeature(){var item=SelectedObject;if(item==null)return;History.Push(Data);Data.features.RemoveAll(x=>x.objectId==item.instanceId);item.featureId=null;Dirty=true;Status="已移除功能，保留外观";}
        public void Load(string name){try{if(stroke)EndStroke();SetData(WorldMapStorage.Load(name));Status="已加载："+name;}catch(Exception e){Status=e.Message;Debug.LogWarning(Status);}}
        public void Export()
        {
            Save();if(Dirty)return;
#if UNITY_EDITOR
            ExportRequested?.Invoke(Data.Clone(),palette);Status="已保存导出请求；停止运行后自动生成地图场景与环境预制体";
#else
            Status="地图已保存。可将 JSON 带回 Unity 工程导出场景。";
#endif
        }
        public void New(bool sample,int width=256){if(stroke)EndStroke();SetData(sample?WorldMapData.SampleMap().Expanded(513):WorldMapData.NewWorkspace("新地图",width));Dirty=true;Status="已创建 "+Data.Width.ToString("0")+" 米"+(sample?"样板地图":"空白地图");}
        public void SetEnvironment(bool rocks,bool clouds){History.Push(Data);Data.rockBoundary=rocks;Data.cloudSea=clouds;world.Configure(palette,Data);world.RebuildAll();Dirty=true;}
        void OnDisable(){if(stroke)EndStroke();GetComponent<WorldMapEditorUI>()?.FlushName();CancelMove();placementGrid?.Hide();PlayerReference?.Hide();if(Data!=null)try{WorldMapStorage.SaveSession(Data);}catch(Exception exception){Debug.LogWarning("编辑会话备份失败："+exception.Message);}}
        void OnDestroy(){PlayerReference?.Dispose();placementGrid?.Dispose();PlacementPreview?.Dispose();if(cursorMaterial)Destroy(cursorMaterial);}
    }
}

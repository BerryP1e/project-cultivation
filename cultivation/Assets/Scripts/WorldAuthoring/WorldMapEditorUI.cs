using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cultivation.WorldAuthoring
{
    public sealed class WorldMapEditorUI : MonoBehaviour
    {
        static readonly Color Panel=new Color(.055f,.071f,.083f,.96f),TextColour=new Color(.86f,.88f,.83f),Accent=new Color(.54f,.72f,.62f),Card=new Color(.12f,.15f,.17f);
        WorldMapEditorController controller;
        Canvas canvas;
        Font font;
        Text status,statistics;
        InputField nameField;
        RectTransform paletteContent,leftPanel,rightPanel;
        InputField searchField;
        Text viewHint;
        Image naturalButton,gapButton;
        RectTransform terrainControls,placementControls;
        InputField yawField;
        Button gridButton,gridSizeButton,rotationStepButton,packButton;
        string pack="全部资源";
        string category="全部";
        readonly List<Tuple<WorldBrush,Image>> tools=new List<Tuple<WorldBrush,Image>>();
        readonly List<Tuple<UnityEngine.UI.Slider,Text,string,Func<float>>> sliderBindings=new List<Tuple<UnityEngine.UI.Slider,Text,string,Func<float>>>();
        float nextRefresh;

        public void Initialize(WorldMapEditorController source)
        {
            controller=source;font=Resources.Load<Font>("Fonts/SimHei")??Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvas=new GameObject("WorldMapEditorCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();canvas.transform.SetParent(transform,false);canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=2200;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,800);scaler.matchWidthOrHeight=.5f;
            if(!EventSystem.current)new GameObject("WorldMapEditorEventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            var top=PanelRect("Header",canvas.transform,new Vector2(0,1),new Vector2(1,1),new Vector2(0,-58),Vector2.zero);
            Label("界域工坊",top,20,14,120,30,23,Accent);Label("场景地图制作",top,145,18,120,24,14,TextColour);
            nameField=Field(top,270,14,210,30,controller.Data.name);nameField.onEndEdit.AddListener(text=>controller.Data.name=text);
            Button(top,"撤销",498,14,58,30,controller.Undo);Button(top,"重做",564,14,58,30,controller.Redo);
            Button(top,"保存",642,14,65,30,()=>{controller.Data.name=nameField.text;controller.Save();});
            Button(top,"加载",715,14,65,30,ShowLoad);
            Button(top,"导出地图",788,14,90,30,()=>{controller.Data.name=nameField.text;controller.Export();});
            Button(top,"空白地图",900,14,88,30,()=>ConfirmNew(false));Button(top,"样板地图",996,14,88,30,()=>ConfirmNew(true));
            Button(top,"操作说明",1095,14,90,30,ShowHelp);

            var view=PanelRect("View controls",canvas.transform,new Vector2(0,1),new Vector2(1,1),new Vector2(0,-98),new Vector2(0,-60));
            Button(view,"编辑 / 游戏视角",12,5,138,28,controller.ToggleGameView);Button(view,"定位出生点",158,5,108,28,controller.FocusSpawn);
            Button(view,"地图 / 雾设置",278,5,116,28,ShowMapSettings);
            Button(view,"功能 / 检查",402,5,108,28,ShowFeatures);
            viewHint=Label("编辑视角 · G 切换游戏视角",view,522,5,720,28,13,Accent);

            var left=leftPanel=PanelRect("Brushes",canvas.transform,new Vector2(0,0),new Vector2(0,1),new Vector2(0,36),new Vector2(198,-108));
            var leftViewport=left;leftViewport.gameObject.AddComponent<RectMask2D>();
            left=Rect("Tool content",leftViewport,new Vector2(0,1),new Vector2(1,1),new Vector2(0,-640),Vector2.zero);left.pivot=new Vector2(.5f,1);
            var toolScroll=leftViewport.gameObject.AddComponent<ScrollRect>();toolScroll.content=left;toolScroll.viewport=leftViewport;toolScroll.horizontal=false;toolScroll.movementType=ScrollRect.MovementType.Clamped;
            Label("地形笔刷",left,14,12,170,25,18,Accent);
            var names=new[]{"生成地面","擦除地面","抬高","降低","平滑","恢复地面","种立体草","擦草","挖水池","填水池","地表材质","擦材质","摆放素材","选择物件","删物件","出生点"};
            var brushes=new[]{WorldBrush.Ground,WorldBrush.EraseGround,WorldBrush.Raise,WorldBrush.Lower,WorldBrush.Smooth,WorldBrush.RestoreGround,WorldBrush.Grass,WorldBrush.EraseGrass,WorldBrush.Pond,WorldBrush.FillPond,WorldBrush.Surface,WorldBrush.EraseSurface,WorldBrush.Place,WorldBrush.SelectObject,WorldBrush.EraseObject,WorldBrush.Spawn};
            for(int i=0;i<names.Length;i++){var tool=brushes[i];var button=Button(left,names[i],12+(i%2)*90,44+(i/2)*31,84,26,()=>{controller.CancelMove();controller.tool=tool;if(tool==WorldBrush.Surface){category="地表";RefreshPalette();}});tools.Add(Tuple.Create(tool,button.GetComponent<Image>()));}
            terrainControls=Rect("Terrain settings",left,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            Slider(terrainControls,"笔刷半径",302,.5f,16,controller.radius,value=>controller.radius=value,()=>controller.radius);
            Slider(terrainControls,"笔触强度",352,.1f,2,controller.strength,value=>controller.strength=value);
            Slider(terrainControls,"地面高度",402,-1,30,controller.elevation,value=>controller.elevation=value);
            Slider(terrainControls,"池底深度",452,.3f,8,controller.pondDepth,value=>controller.pondDepth=value);
            Slider(terrainControls,"草密度",502,0,1,controller.Data.grassDensity,controller.SetGrassDensity,()=>controller.Data.grassDensity);
            Label("地表材质：右侧选贴图\n软边叠加；Shift 擦除\n密度可调，0 只保留地表",terrainControls,14,560,174,64,12,TextColour);
            placementControls=Rect("Placement settings",left,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            Button(placementControls,"左转 Q",12,302,84,28,()=>controller.RotateSelection(-1));Button(placementControls,"右转 E",102,302,84,28,()=>controller.RotateSelection(1));
            yawField=Field(placementControls,12,339,82,28,"0");yawField.contentType=InputField.ContentType.DecimalNumber;yawField.onEndEdit.AddListener(value=>{if(float.TryParse(value,out float angle))controller.SetYaw(angle);});
            rotationStepButton=Button(placementControls,"步进 90°",102,339,84,28,()=>{if(controller.gridSnap&&controller.palette.entries[controller.selectedEntry].module){controller.rotationStep=90;return;}controller.rotationStep=controller.rotationStep==15?45:controller.rotationStep==45?90:15;});
            gridButton=Button(placementControls,"网格吸附：开",12,377,174,28,controller.ToggleGridSnap);
            gridSizeButton=Button(placementControls,"网格 1 米",12,413,174,28,()=>{if(controller.palette.entries[controller.selectedEntry].module){controller.AlignWalls();return;}float[] steps={.25f,.5f,1,2,4};int index=Array.IndexOf(steps,controller.gridSize);controller.gridSize=steps[(index+1)%steps.Length];});
            Slider(placementControls,"物件倍率",449,.1f,4,controller.objectScale,value=>controller.objectScale=value,()=>controller.objectScale);
            Slider(placementControls,"摆放升降",499,-2,20,controller.placementHeight,value=>controller.placementHeight=value,()=>controller.placementHeight);
            naturalButton=Button(placementControls,"贴地融合",12,555,84,28,()=>controller.naturalPlacement=true).GetComponent<Image>();
            gapButton=Button(placementControls,"保留间隙",102,555,84,28,()=>controller.naturalPlacement=false).GetComponent<Image>();
            Button(placementControls,"移动选中",12,592,84,28,controller.BeginMoveSelected);Button(placementControls,"删除选中",102,592,84,28,controller.DeleteSelected);

            var right=rightPanel=PanelRect("Palette",canvas.transform,new Vector2(1,0),new Vector2(1,1),new Vector2(-252,36),new Vector2(0,-108));
            Label("素材库",right,14,12,110,25,18,Accent);
            searchField=Field(right,12,44,226,28,"");searchField.onValueChanged.AddListener(_=>RefreshPalette());
            packButton=Button(right,"全部资源",12,79,226,25,()=>{var packs=new List<string>{"全部资源"};foreach(var entry in controller.palette.entries)if(!string.IsNullOrEmpty(entry.sourcePack)&&!packs.Contains(entry.sourcePack))packs.Add(entry.sourcePack);pack=packs[(packs.IndexOf(pack)+1)%packs.Count];RefreshPalette();});
            int tab=0;foreach(var name in new[]{"全部","岩石","草花","桥道","建筑","树木","摆件","地表","天空","功能"}){string selected=name;Button(right,name,12+(tab%3)*77,112+(tab/3)*31,70,26,()=>{category=selected;RefreshPalette();});tab++;}
            var scroll=Rect("Palette scroll",right,new Vector2(0,0),new Vector2(1,1),new Vector2(8,10),new Vector2(-8,-241));scroll.gameObject.AddComponent<Image>().color=new Color(.02f,.03f,.04f,.35f);var mask=scroll.gameObject.AddComponent<Mask>();mask.showMaskGraphic=false;
            var viewport=Rect("Viewport",scroll,new Vector2(0,0),new Vector2(1,1),Vector2.zero,Vector2.zero);
            paletteContent=Rect("Content",viewport,new Vector2(0,1),new Vector2(1,1),Vector2.zero,Vector2.zero);paletteContent.pivot=new Vector2(.5f,1);
            var grid=paletteContent.gameObject.AddComponent<GridLayoutGroup>();grid.cellSize=new Vector2(108,98);grid.spacing=new Vector2(6,8);grid.padding=new RectOffset(4,4,4,4);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=2;
            var fitter=paletteContent.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scrollRect=scroll.gameObject.AddComponent<ScrollRect>();scrollRect.viewport=viewport;scrollRect.content=paletteContent;scrollRect.horizontal=false;scrollRect.movementType=ScrollRect.MovementType.Clamped;
            var bottom=PanelRect("Status",canvas.transform,new Vector2(0,0),new Vector2(1,0),Vector2.zero,new Vector2(0,30));
            status=Label("",bottom,14,4,850,22,13,TextColour);statistics=Label("",bottom,875,4,380,22,13,Accent);
            RefreshPalette();
        }
        public void RefreshPalette()
        {
            for(int i=paletteContent.childCount-1;i>=0;i--){paletteContent.GetChild(i).gameObject.SetActive(false);Destroy(paletteContent.GetChild(i).gameObject);}
            if(category=="地表")
            {
                for(int i=0;i<controller.palette.groundLayers.Length;i++){int layer=i;var entry=controller.palette.groundLayers[i];if(searchField&&!string.IsNullOrWhiteSpace(searchField.text)&&(entry.label+" "+entry.texture.name).IndexOf(searchField.text,StringComparison.OrdinalIgnoreCase)<0)continue;var card=Button(paletteContent,"",0,0,108,98,()=>{controller.CancelMove();controller.selectedGroundLayer=layer;controller.tool=WorldBrush.Surface;});var raw=Rect("Ground texture",card.transform,new Vector2(0,0),new Vector2(1,1),new Vector2(9,28),new Vector2(-9,-6)).gameObject.AddComponent<RawImage>();raw.texture=entry.texture;raw.raycastTarget=false;Label(entry.label,card.transform,4,72,100,22,12,TextColour).alignment=TextAnchor.MiddleCenter;}
                return;
            }
            for(int i=0;i<controller.palette.entries.Length;i++)
            {
                var entry=controller.palette.entries[i];if(entry.libraryHidden)continue;if(category!="全部" && entry.category!=category)continue;
                if(pack!="全部资源"&&entry.sourcePack!=pack)continue;
                if(searchField && !string.IsNullOrWhiteSpace(searchField.text) && (entry.label+" "+entry.prefab.name).IndexOf(searchField.text.Trim(),StringComparison.OrdinalIgnoreCase)<0)continue;
                int selected=i;var card=Button(paletteContent,"",0,0,108,98,()=>controller.ChooseAsset(selected));
                if(entry.thumbnail){var raw=Rect("Thumbnail",card.transform,new Vector2(0,0),new Vector2(1,1),new Vector2(9,28),new Vector2(-9,-6)).gameObject.AddComponent<RawImage>();raw.texture=entry.thumbnail;raw.raycastTarget=false;}
                Label(entry.label,card.transform,4,72,100,22,12,TextColour).alignment=TextAnchor.MiddleCenter;
            }
        }
        void Update()
        {
            if(!controller || !canvas || Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.15f;
            status.text=controller.Status;statistics.text=(controller.Dirty?"未保存 · ":"")+"草簇 "+controller.world.GrassCount+"   物件 "+controller.world.PlacedCount+"   包边 "+controller.world.BoundaryCount;
            leftPanel.gameObject.SetActive(!controller.GameView);rightPanel.gameObject.SetActive(!controller.GameView);
            bool placing=controller.tool==WorldBrush.Place||controller.tool==WorldBrush.SelectObject;placementControls.gameObject.SetActive(placing);terrainControls.gameObject.SetActive(!placing);
            var selectedEntry=controller.palette.entries[Mathf.Clamp(controller.selectedEntry,0,controller.palette.entries.Length-1)];
            foreach(var binding in sliderBindings){float value=binding.Item4();binding.Item1.SetValueWithoutNotify(value);bool locked=binding.Item3=="物件倍率"&&selectedEntry.lockScale;binding.Item1.interactable=!locked;binding.Item2.text=locked?"建筑尺度固定":ValueText(binding.Item3,value);}
            if(!yawField.isFocused)yawField.text=controller.yaw.ToString("0.#");gridButton.GetComponentInChildren<Text>().text=(selectedEntry.module?"墙体端口吸附：":"网格吸附：")+(controller.gridSnap?"开":"关");gridSizeButton.GetComponentInChildren<Text>().text=selectedEntry.module?"整理相邻墙段":"网格 "+controller.gridSize.ToString("0.##")+" 米";rotationStepButton.GetComponentInChildren<Text>().text="步进 "+controller.rotationStep.ToString("0")+"°";packButton.GetComponentInChildren<Text>().text=pack;
            viewHint.text=controller.GameView?"玩家原尺寸参照 · WASD 移动 · G 返回编辑":string.IsNullOrEmpty(controller.PlacementHint)?"编辑视角 · 地图 "+controller.Data.Width.ToString("0")+" 米 · G 查看玩家比例":controller.PlacementHint;
            naturalButton.color=controller.naturalPlacement?new Color(.24f,.38f,.33f):Card;gapButton.color=controller.naturalPlacement?Card:new Color(.24f,.38f,.33f);
            foreach(var item in tools)item.Item2.color=item.Item1==controller.tool?new Color(.24f,.38f,.33f):Card;
        }
        void Slider(Transform parent,string caption,float y,float min,float max,float initial,Action<float> changed,Func<float> getter=null)
        {
            var label=Label(ValueText(caption,initial),parent,14,y,175,22,13,TextColour);
            var rect=Rect("Slider",parent,new Vector2(0,1),new Vector2(0,1),new Vector2(14,-y-42),new Vector2(182,-y-28));
            var background=rect.gameObject.AddComponent<Image>();background.color=Card;
            var handle=Rect("Handle",rect,new Vector2(0,0),new Vector2(0,1),new Vector2(-4,-2),new Vector2(4,2)).gameObject.AddComponent<Image>();handle.color=Accent;
            var slider=rect.gameObject.AddComponent<UnityEngine.UI.Slider>();slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;slider.direction=UnityEngine.UI.Slider.Direction.LeftToRight;slider.minValue=min;slider.maxValue=max;slider.value=initial;
            slider.onValueChanged.AddListener(value=>{label.text=ValueText(caption,value);changed(value);});
            if(getter!=null)sliderBindings.Add(Tuple.Create(slider,label,caption,getter));
        }
        static string ValueText(string caption,float value)=>caption+"  "+(caption=="草密度"?(value*100).ToString("0")+"%":value.ToString("0.0"));
        void ShowLoad()
        {
            var popup=Popup("已保存地图",out var body);var maps=WorldMapStorage.List();
            Button(body,"恢复上次编辑",16,42,212,32,()=>{try{var data=WorldMapStorage.LastSession();if(data!=null){controller.SetData(data);nameField.text=data.name;Destroy(popup);}}catch(Exception exception){Debug.LogWarning(exception.Message);}});
            Button(body,"恢复最近删除",236,42,220,32,()=>{try{WorldMapStorage.RestoreLastDeleted();Destroy(popup);ShowLoad();}catch(Exception exception){Label(exception.Message,body,16,390,440,30,12,TextColour);}});
            var viewport=Rect("Map list",body,new Vector2(0,1),new Vector2(1,1),new Vector2(16,-356),new Vector2(-24,-82));viewport.gameObject.AddComponent<Image>().color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();var content=Rect("Maps",viewport,new Vector2(0,1),new Vector2(1,1),new Vector2(0,-Mathf.Max(274,maps.Length*40)),Vector2.zero);content.pivot=new Vector2(.5f,1);scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.scrollSensitivity=28;scroll.movementType=ScrollRect.MovementType.Clamped;
            int y=0;foreach(var name in maps){string choice=name;Button(content,name,0,y,352,32,()=>{controller.Load(choice);nameField.text=controller.Data.name;Destroy(popup);});Button(content,"删除",360,y,70,32,()=>ConfirmDeleteMap(choice,popup));y+=40;}
            if(maps.Length==0)Label("尚未保存地图",body,18,126,430,30,16,TextColour);
            Label("文件目录："+WorldMapStorage.DirectoryPath,body,16,366,440,50,12,TextColour);
        }
        void ConfirmDeleteMap(string name,GameObject loadPopup)
        {
            var popup=Popup("删除已保存地图？",out var body);
            Label("地图："+name+"\n删除后可用“恢复最近删除”找回。\n仅移除已保存的地图文件，正式场景不受影响。",body,16,64,440,104,15,TextColour);
            Button(body,"删除并保留备份",16,190,216,36,()=>{try{WorldMapStorage.Trash(name);Destroy(popup);Destroy(loadPopup);ShowLoad();}catch(Exception exception){Label(exception.Message,body,16,244,440,50,13,TextColour);}});
            Button(body,"取消",240,190,216,36,()=>Destroy(popup));
        }
        void ConfirmNew(bool sample,int width=256)
        {
            if(!controller.Dirty){controller.New(sample,width);nameField.text=controller.Data.name;return;}
            var popup=Popup("保存当前地图后再切换？",out var body);
            Button(body,"保存并切换",18,90,135,36,()=>{controller.Save();if(!controller.Dirty){controller.New(sample,width);nameField.text=controller.Data.name;Destroy(popup);}});
            Button(body,"放弃本次修改",165,90,145,36,()=>{controller.New(sample,width);nameField.text=controller.Data.name;Destroy(popup);});
        }
        void ShowMapSettings()
        {
            var popup=Popup("地图范围与流动雾",out var body);
            var size=Label("",body,16,50,440,44,15,TextColour);
            Action sizeText=()=>size.text="范围 "+controller.Data.Width.ToString("0")+" × "+controller.Data.Width.ToString("0")+" 米 · 地形精度 "+controller.Data.cellSize.ToString("0.##")+" 米\n扩展保留原内容与坐标，可撤销。";sizeText();
            Button(body,"当前地图范围扩大一倍",16,104,270,32,()=>{controller.ExpandMap();sizeText();});
            Button(body,"新建 256米",16,148,140,32,()=>{Destroy(popup);ConfirmNew(false,256);});Button(body,"新建 512米",164,148,140,32,()=>{Destroy(popup);ConfirmNew(false,512);});Button(body,"新建 1024米",312,148,150,32,()=>{Destroy(popup);ConfirmNew(false,1024);});
            bool recorded=false;Action<float,float,float> change=(height,speed,density)=>{if(!recorded){controller.History.Push(controller.Data);recorded=true;}controller.SetFog(height,speed,density);};
            Slider(body,"雾面偏移（米）",206,-3,.5f,controller.Data.fogHeight,value=>change(value,controller.Data.fogSpeed,controller.Data.fogDensity));
            Slider(body,"流动速度",258,0,3,controller.Data.fogSpeed,value=>change(controller.Data.fogHeight,value,controller.Data.fogDensity));
            Slider(body,"雾浓度",310,.2f,2,controller.Data.fogDensity,value=>change(controller.Data.fogHeight,controller.Data.fogSpeed,value));
            Label("默认雾面低于地面，双层噪声持续流动。\n0 速度可暂停；以上设置随地图保存。",body,224,214,238,100,13,Accent);
            Label("大地图按已绘制区域生成网格；范围不等于满铺地面。",body,16,378,440,40,13,TextColour);
        }
        void ShowHelp()
        {
            Popup("操作说明",out var body);
            Label("左键：绘制 / 摆放；Q / E 或按钮：旋转\n选择物件：点击已摆物，可旋转、移动、删除\n网格吸附开关：0.25 / 0.5 / 1 / 2 / 4 米\n右键旋转视角；中键 / WASD 平移；滚轮缩放\nG：带原尺寸玩家模型的游戏视角\n地表材质：右侧选贴图，软边混合；Shift 擦除\n恢复地面：清草、填池、清除材质笔触\n地图 / 雾设置：扩展尺寸、降低雾面、调整流动\n桥身可跨水，两端贴岸；保留间隙可手动架设\nCtrl+Z / Y：撤销 / 重做；Ctrl+S：保存\n正式地图：保存后停止工坊，自动更新游戏地图\n停止仅备份编辑会话；要更新游戏须先点保存。",body,18,50,440,320,15,TextColour);
            Button(body,"切换岩石包边",18,350,180,34,()=>controller.SetEnvironment(!controller.Data.rockBoundary,controller.Data.cloudSea));
            Button(body,"切换空地云海",220,350,180,34,()=>controller.SetEnvironment(controller.Data.rockBoundary,!controller.Data.cloudSea));
        }
        void ShowFeatures()
        {
            var popup=Popup("功能模块与地图检查",out var body);var selected=controller.SelectedObject;
            Label(selected==null?"先选择物件，可将炼丹、修炼、传送等功能赋予它。":"选中物件："+controller.palette.entries[controller.palette.Find(selected.paletteId)].label,body,16,52,440,44,14,TextColour);
            var dropdownRoot=Rect("功能模板",body,new Vector2(0,1),new Vector2(0,1),new Vector2(16,-134),new Vector2(456,-98));dropdownRoot.gameObject.AddComponent<Image>().color=Card;var dropdown=dropdownRoot.gameObject.AddComponent<Dropdown>();dropdown.captionText=Label("选择功能模板",dropdownRoot,8,0,420,36,14,TextColour);
            var templates=controller.palette.featureTemplates;foreach(var template in templates)if(template)dropdown.options.Add(new Dropdown.OptionData(template.sourceScene+" · "+template.label));
            // Native dropdown requires an explicit template, built from the same UI primitives.
            var panel=PanelRect("Template",dropdownRoot,new Vector2(0,0),new Vector2(1,0),new Vector2(0,-210),Vector2.zero);panel.pivot=new Vector2(.5f,1);var scroll=panel.gameObject.AddComponent<ScrollRect>();var viewport=Rect("Viewport",panel,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);viewport.gameObject.AddComponent<RectMask2D>();var content=Rect("Content",viewport,new Vector2(0,1),new Vector2(1,1),new Vector2(0,-32),Vector2.zero);var item=Rect("Item",content,new Vector2(0,1),new Vector2(1,1),new Vector2(0,-32),Vector2.zero);item.gameObject.AddComponent<Image>().color=Card;var toggle=item.gameObject.AddComponent<Toggle>();toggle.targetGraphic=item.GetComponent<Image>();toggle.graphic=toggle.targetGraphic;dropdown.itemText=Label("",item,8,0,420,32,14,TextColour);scroll.content=content;scroll.viewport=viewport;scroll.horizontal=false;dropdown.template=panel;panel.gameObject.SetActive(false);dropdown.RefreshShownValue();
            Button(body,"赋予选中物件",16,150,208,34,()=>{if(templates.Length>0)controller.AssignFeature(templates[Mathf.Clamp(dropdown.value,0,templates.Length-1)].id);Destroy(popup);});Button(body,"移除功能保留外观",236,150,220,34,()=>{controller.ClearFeature();Destroy(popup);});
            var issues=WorldMapConsistency.Check(controller.Data,controller.palette);Label("外观 "+controller.Data.objects.Count+" · 功能 "+controller.Data.features.Count+"\n"+(issues.Count==0?"结构检查通过。":string.Join("\n",issues.GetRange(0,Mathf.Min(6,issues.Count)))),body,16,204,440,180,13,issues.Count==0?Accent:TextColour);
            Label("在工坊中只编辑功能数据；进入游戏才启用功能。",body,16,390,440,26,12,Accent);
        }
        GameObject Popup(string title,out RectTransform body)
        {
            var overlay=PanelRect("Popup",canvas.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);overlay.GetComponent<Image>().color=new Color(0,0,0,.60f);
            body=PanelRect("Dialog",overlay,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-240,-220),new Vector2(240,220));
            Label(title,body,16,12,395,30,20,Accent);var result=overlay.gameObject;Button(body,"×",428,12,32,30,()=>Destroy(result));return result;
        }
        RectTransform PanelRect(string name,Transform parent,Vector2 min,Vector2 max,Vector2 a,Vector2 b){var rt=Rect(name,parent,min,max,a,b);rt.gameObject.AddComponent<Image>().color=Panel;return rt;}
        static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max,Vector2 a,Vector2 b)
        {
            var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rt.SetParent(parent,false);rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=a;rt.offsetMax=b;return rt;
        }
        Text Label(string text,Transform parent,float x,float y,float width,float height,int size,Color colour)
        {
            var rt=Rect("Label",parent,new Vector2(0,1),new Vector2(0,1),new Vector2(x,-y-height),new Vector2(x+width,-y));var label=rt.gameObject.AddComponent<Text>();label.font=font;label.fontSize=size;label.color=colour;label.text=text;label.alignment=TextAnchor.MiddleLeft;label.raycastTarget=false;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Overflow;return label;
        }
        Button Button(Transform parent,string title,float x,float y,float width,float height,Action action)
        {
            var rt=Rect(title,parent,new Vector2(0,1),new Vector2(0,1),new Vector2(x,-y-height),new Vector2(x+width,-y));var image=rt.gameObject.AddComponent<Image>();image.color=Card;
            var button=rt.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>action());Label(title,rt,3,0,width-6,height,14,TextColour).alignment=TextAnchor.MiddleCenter;return button;
        }
        InputField Field(Transform parent,float x,float y,float width,float height,string value)
        {
            var rt=Rect("Map name",parent,new Vector2(0,1),new Vector2(0,1),new Vector2(x,-y-height),new Vector2(x+width,-y));rt.gameObject.AddComponent<Image>().color=Card;var field=rt.gameObject.AddComponent<InputField>();field.textComponent=Label("",rt,8,0,width-16,height,15,TextColour);field.text=value;field.characterLimit=64;return field;
        }
        void OnDestroy(){if(canvas)Destroy(canvas.gameObject);}
        public void FlushName(){if(controller&&controller.Data!=null&&nameField)controller.Data.name=nameField.text;}
        public void SetMapName(string value){if(nameField)nameField.SetTextWithoutNotify(value);}
    }
}

using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>鼠标常驻定向；左键一次普攻，神通按住引导、松键施放。费用与冷却仍由各施放器负责。</summary>
[DefaultExecutionOrder(-120)]
[DisallowMultipleComponent]
public sealed class PlayerManualAim : MonoBehaviour
{
    public static bool AltHeld=>Input.GetKey(KeyCode.LeftAlt)||Input.GetKey(KeyCode.RightAlt);
    public bool 正在瞄准 {get;private set;}
    public bool 本帧取消引导 {get;private set;}
    public Vector3 落点 {get;private set;}
    public int 选择槽位 {get;private set;}=-1; // -1 cultivation, -2 treasure, -3 weapon
    public bool 鼠标落点有效 {get;private set;}
    public Vector3 鼠标方向 {get {var d=落点-transform.position;d.y=0;return d.normalized;}}
    NpcTargeting targeting;
    ActiveSkillCaster caster;
    LineRenderer ring, direction, senseRing, senseWash;
    Material material;
    VoxelMapPilot map;
    bool guiding;
    KeyCode 引导按键=KeyCode.None;
    Object 引导内容;
    PlayerAimWheel wheel;
    public static float SenseRange(GameObject player)
    {var targeting=player.GetComponent<NpcTargeting>();return targeting?Mathf.Min(40,targeting.神识范围):PlayerCombatStats.算神识范围(player.GetComponent<PlayerCombatStats>(),4,.8f,40);}
    public bool 可接收输入=>Time.timeScale>0 && !UiEscRegistry.SceneInputBlocked && !(GetComponent<PlayerVitals>()?.IsDead ?? true)
        && !(GetComponent<演出锁>()?.正在锁 ?? false) && !灵田摆放器.正在摆放;
    public bool 有锁定=>targeting && targeting.LockedNpc && !targeting.LockedNpc.IsDead;
    void Awake(){targeting=GetComponent<NpcTargeting>();caster=GetComponent<ActiveSkillCaster>();wheel=GetComponent<PlayerAimWheel>()??gameObject.AddComponent<PlayerAimWheel>();}
    public bool 开始引导(int slot,KeyCode 按住键=KeyCode.None)
    {
        if(!可接收输入)return false;
        if(!caster)caster=GetComponent<ActiveSkillCaster>();
        if(slot>=0 && (!caster || !(caster.槽位内容(slot) is ActiveDivineAbility)))return false;
        选择槽位=slot;引导按键=按住键;引导内容=slot>=0?caster.槽位内容(slot):null;guiding=true;return true;
    }
    public void 取消引导(){guiding=false;引导按键=KeyCode.None;引导内容=null;选择槽位=-1;Hide();}
    void Update()
    {
        本帧取消引导=false;
        鼠标落点有效=false;
        if(!可接收输入||TreasureSkillHud.正在切换){取消引导();wheel.显示(false);return;}
        if(!map)map=FindObjectOfType<VoxelMapPilot>();if(map && map.测试挖掘){取消引导();wheel.显示(false);return;}
        if(guiding&&(Input.GetKeyDown(KeyCode.Escape)||Input.GetMouseButtonDown(1))){本帧取消引导=true;取消引导();UiEscRegistry.NotifyClosed();wheel.显示(false);return;}
        // 同一帧松开旧键并按下新技能时，以新按下的技能为准。
        bool skillShortcut=caster&&caster.处理快捷键();
        if(guiding&&选择槽位>=0&&(!caster||引导内容!=caster.槽位内容(选择槽位)))取消引导();
        // 不仅判断GetKeyUp，也处理一帧内快速点按或丢失的松键边沿。
        bool release=guiding&&引导按键!=KeyCode.None&&!Input.GetKey(引导按键);
        var camera=Camera.main;if(!camera){if(release)取消引导();else Hide();return;}
        var ray=camera.ScreenPointToRay(Input.mousePosition);
        Vector3 normal=Vector3.up;
        if(VoxelCombatDamage.Ray(ray.origin,ray.GetPoint(200),out var hit,false,transform)){落点=hit.point;normal=hit.normal;鼠标落点有效=true;}
        else if(new Plane(Vector3.up,transform.position).Raycast(ray,out float distance)){落点=ray.GetPoint(distance);鼠标落点有效=true;}
        wheel.显示(鼠标落点有效);
        if(!鼠标落点有效){if(release)取消引导();else Hide();return;}
        if(EventSystem.current && EventSystem.current.IsPointerOverGameObject()){if(release)取消引导();else Hide();return;}
        if(guiding){
            正在瞄准=true;var point=引导目标点();
            Draw(point,Radius(),Vector3.Distance(point,transform.position)<=Range()+.05f,自身技能()||跟随锁定(选择槽位)?Vector3.up:normal);DrawSenseRange();
        }else Hide();
        if(release){
            int slot=选择槽位;var point=引导目标点();
            取消引导();确认施放(slot,point);return;
        }
        // 按住神通期间左键不再提前确认；法宝的既有鼠标确认入口保留。
        if(guiding&&引导按键!=KeyCode.None)return;
        if(!guiding && Input.GetKeyDown(KeyCode.E) && !skillShortcut && !Input.GetKeyDown(KeyCode.Q)){
            确认施放(-3,落点);return;
        }
        bool shortcut=Input.GetKeyDown(KeyCode.E)||Input.GetKeyDown(KeyCode.Q)||skillShortcut;
        if(Input.GetMouseButtonDown(0)&&!shortcut&&!本帧取消引导){
            if(确认施放(guiding?选择槽位:-1,自身技能()&&guiding?transform.position:落点))取消引导();
        }
    }
    bool 自身技能(){var a=caster?caster.槽位内容(选择槽位) as ActiveDivineAbility:null;return a&&(a.结算方式==ActiveSkillKind.雷狱领域||a.结算方式==ActiveSkillKind.四御护罩);}
    bool 跟随锁定(int slot){var a=caster?caster.槽位内容(slot) as ActiveDivineAbility:null;return 有锁定&&a&&a.结算方式!=ActiveSkillKind.闪烁位移&&a.结算方式!=ActiveSkillKind.雷狱领域&&a.结算方式!=ActiveSkillKind.四御护罩;}
    Vector3 引导目标点()=>自身技能()?transform.position:跟随锁定(选择槽位)?targeting.LockedNpc.transform.position:落点;
    float Range()
    {
        if(选择槽位==-2)return SpiritArrayTalisman.Equipped(FindObjectOfType<UIPanelData>())?SenseRange(gameObject):GetComponent<QingshanSwordTreasure>()?.SenseRange ?? SenseRange(gameObject);
        var skill=caster?caster.槽位内容(选择槽位) as ActiveDivineAbility:null;
        if(skill && (skill.结算方式==ActiveSkillKind.追踪弹 || skill.结算方式==ActiveSkillKind.定向水炮 || skill.结算方式==ActiveSkillKind.伴龙随行) && skill.范围>0)return Mathf.Min(SenseRange(gameObject),skill.范围);
        return SenseRange(gameObject);
    }
    float Radius()
    {
        if(选择槽位==-2)return SpiritArrayTalisman.Equipped(FindObjectOfType<UIPanelData>())?SpiritArrayRules.阵半径:GetComponent<QingshanSwordTreasure>()?.UltimateVoxelRadius ?? 4;
        var skill=caster?caster.槽位内容(选择槽位) as ActiveDivineAbility:null;
        if(skill&&skill.结算方式==ActiveSkillKind.雷狱领域)return SenseRange(gameObject);
        if(!skill){var dragon=GetComponent<BasicTaichuDragon01>();if(dragon&&dragon.enabled)return dragon.引导范围(落点);var frost=GetComponent<BasicFrostSpike01>();if(frost&&frost.enabled)return frost.下次范围;var thunder=GetComponent<BasicThunder01>();return thunder&&thunder.enabled?thunder.下次范围:.6f;}
        if(skill.结算方式==ActiveSkillKind.伴龙随行)return GetComponent<CheDianYeTingCompanion>()?.Definition?.BreathRadius ?? .85f;
        if(skill.结算方式==ActiveSkillKind.追踪弹)return 2.5f;
        if(skill.结算方式==ActiveSkillKind.天降打击||skill.结算方式==ActiveSkillKind.螺旋剑雨)return skill.范围;
        return VoxelCombatDamage.AreaRadius(skill);
    }
    public bool 确认施放(int slot,Vector3 point)
    {
        if(!可接收输入)return false;
        bool cast=false;
        if(slot>=0)cast=caster && (跟随锁定(slot)?caster.锁定施放(slot):caster.手动施放(slot,point,targeting?targeting.RaycastNpcUnderMouse():null));
        else if(slot==-2){
            var data=FindObjectOfType<UIPanelData>();
            if(data&&data.当前法宝&&data.当前法宝.法宝id==UIPanelData.镇妖葫id)cast=GetComponent<TreasureCaster>()?.手动收服(targeting?targeting.RaycastNpcUnderMouse():null)??false;
            else cast=SpiritArrayTalisman.Equipped(data)?SpiritArrayTalisman.Cast(gameObject,data,point):GetComponent<QingshanSwordTreasure>()?.TryCastUltimateAt(point) ?? false;
        }
        else
        {
            bool locked=有锁定;if(locked)point=targeting.LockedNpc.transform.position;
            if(slot==-3){
                foreach(var melee in GetComponents<BasicJiuba01>())if(melee.enabled){cast=locked?melee.出手():melee.手动出手(point);break;}
            }
            else if(slot==-1){
            foreach(var ranged in GetComponents<BasicRemoteAttack01>())if(ranged.enabled){cast=locked?ranged.出手():ranged.手动出手(point);break;}
            if(!cast)foreach(var thunder in GetComponents<BasicThunder01>())if(thunder.enabled){cast=locked?thunder.出手():thunder.手动出手(point);break;}
            if(!cast)foreach(var frost in GetComponents<BasicFrostSpike01>())if(frost.enabled){cast=locked?frost.出手():frost.手动出手(point);break;}
            // 邪眼使用原始落点与自己的神识/冷却检查，不受近战射程和普攻冷却阻止。
            bool eye=GetComponent<DevilEyeAbility>()?.跟随普攻瞄准(point) ?? false;
            cast|=eye;
            }
        }
        if(!cast)FindObjectOfType<UIPanelData>()?.ShowHint("当前招式尚未就绪，或落点超出施放距离");
        return cast;
    }
    LineRenderer CreateLine(string name,int count)
    {
        var go=new GameObject(name);go.layer=2;go.transform.SetParent(transform,false);
        var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;line.positionCount=count;
        line.widthMultiplier=.035f;line.numCapVertices=3;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;return line;
    }
    void Draw(Vector3 point,float radius,bool inRange,Vector3 normal)
    {
        if(!material){material=new Material(Shader.Find("Sprites/Default"));ring=CreateLine("技能落点",65);direction=CreateLine("施放指向",2);}
        ring.enabled=direction.enabled=true;Color color=inRange?new Color(.7f,.85f,.8f,.85f):new Color(.8f,.55f,.4f,.8f);
        ring.startColor=ring.endColor=direction.startColor=direction.endColor=color;
        var frost=选择槽位==-1?GetComponent<BasicFrostSpike01>():null;
        if(frost&&frost.enabled&&frost.下次式==2)
        {
            var forward=point-transform.position;forward.y=0;forward=forward.sqrMagnitude>.0001f?forward.normalized:transform.forward;
            var start=transform.position+Vector3.up*.1f;var end=start+forward*frost.冰波长度;var side=Vector3.Cross(Vector3.up,forward)*radius;
            ring.positionCount=5;ring.SetPositions(new[]{start-side,start+side,end+side,end-side,start-side});direction.SetPositions(new[]{start,end});return;
        }
        ring.positionCount=65;
        var aimPoint=point;
        if(选择槽位==-2&&SpiritArrayTalisman.Equipped(FindObjectOfType<UIPanelData>())){
            var origin=transform.position+Vector3.up*1.1f;normal=point-origin;
            normal=normal.sqrMagnitude>.001f?normal.normalized:transform.forward;point=origin+normal*1.1f;
        }
        var dragon=选择槽位==-1?GetComponent<BasicTaichuDragon01>():null;
        if(dragon&&dragon.enabled&&dragon.引导式(point)<2){point=transform.position;normal=Vector3.up;}
        if(frost&&frost.enabled&&frost.下次式==3){point=transform.position;normal=Vector3.up;}
        var rotation=Quaternion.FromToRotation(Vector3.up,normal);
        for(int i=0;i<65;i++){float angle=i*Mathf.PI*2/64;ring.SetPosition(i,point+rotation*new Vector3(Mathf.Cos(angle)*radius,.08f,Mathf.Sin(angle)*radius));}
        direction.SetPosition(0,transform.position+Vector3.up*.12f);direction.SetPosition(1,(dragon&&dragon.enabled?aimPoint:point)+Vector3.up*.12f);
    }
    void DrawSenseRange()
    {
        if(!senseRing){senseRing=CreateLine("神识墨线",129);senseWash=CreateLine("神识墨晕",129);senseWash.widthMultiplier=.13f;}
        senseRing.enabled=senseWash.enabled=true;
        senseRing.startColor=senseRing.endColor=new Color(.35f,.46f,.41f,.55f);
        senseWash.startColor=senseWash.endColor=new Color(.22f,.32f,.28f,.14f);
        var center=transform.position+Vector3.up*.09f;float range=SenseRange(gameObject),time=Time.time;
        var terrain=Terrain.activeTerrain;
        for(int i=0;i<129;i++){
            float a=i*Mathf.PI*2/128;
            float ripple=Mathf.Sin(a*9+time*.9f)*.035f+Mathf.Sin(a*17-time*.65f)*.018f;
            var p=center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(range+ripple);
            if(terrain){var local=p-terrain.transform.position;var size=terrain.terrainData.size;
                if(local.x>=0&&local.z>=0&&local.x<=size.x&&local.z<=size.z)p.y=terrain.SampleHeight(p)+terrain.transform.position.y+.09f;}
            senseRing.SetPosition(i,p);senseWash.SetPosition(i,p);
        }
    }
    void Hide(){正在瞄准=false;if(ring)ring.enabled=false;if(direction)direction.enabled=false;if(senseRing)senseRing.enabled=false;if(senseWash)senseWash.enabled=false;}
    void OnDisable(){取消引导();鼠标落点有效=false;if(wheel)wheel.显示(false);}
    void OnApplicationFocus(bool focused){if(!focused)取消引导();}
    void OnDestroy(){if(material)Destroy(material);}
}

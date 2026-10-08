using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Alt selects an unlocked world cast; confirmation uses the normal animation, costs and cooldowns.</summary>
[DefaultExecutionOrder(-120)]
[DisallowMultipleComponent]
public sealed class PlayerManualAim : MonoBehaviour
{
    public static bool AltHeld=>Input.GetKey(KeyCode.LeftAlt)||Input.GetKey(KeyCode.RightAlt);
    public bool 正在瞄准 {get;private set;}
    public Vector3 落点 {get;private set;}
    public int 选择槽位 {get;private set;}=-1; // -1 basic, -2 treasure
    NpcTargeting targeting;
    ActiveSkillCaster caster;
    LineRenderer ring, direction, senseRing, senseWash;
    Material material;
    VoxelMapPilot map;
    bool held,cancelled;
    public static float SenseRange(GameObject player)
    {var targeting=player.GetComponent<NpcTargeting>();return targeting?Mathf.Min(40,targeting.神识范围):PlayerCombatStats.算神识范围(player.GetComponent<PlayerCombatStats>(),4,.8f,40);}
    public bool 可接收输入=>Time.timeScale>0 && !UiEscRegistry.SceneInputBlocked && !(GetComponent<PlayerVitals>()?.IsDead ?? true)
        && !(GetComponent<演出锁>()?.正在锁 ?? false) && !灵田摆放器.正在摆放;
    public bool 有锁定=>targeting && targeting.LockedNpc && !targeting.LockedNpc.IsDead;
    void Awake(){targeting=GetComponent<NpcTargeting>();caster=GetComponent<ActiveSkillCaster>();}
    void Update()
    {
        if(!AltHeld){held=false;cancelled=false;Hide();return;}
        if(!held){held=true;选择槽位=-1;}
        if(!可接收输入 || 有锁定){Hide();return;}
        if(!map)map=FindObjectOfType<VoxelMapPilot>();if(map && map.测试挖掘){Hide();return;}
        if(Input.GetKeyDown(KeyCode.Escape)||Input.GetMouseButtonDown(1)){cancelled=true;Hide();return;}
        if(Input.GetKeyDown(KeyCode.F)){选择槽位=-1;cancelled=false;}
        if(Input.GetKeyDown(KeyCode.E)){选择槽位=-2;cancelled=false;}
        for(int i=0;i<6;i++)if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i))||Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad1+i))){选择槽位=i;cancelled=false;}
        if(cancelled || EventSystem.current && EventSystem.current.IsPointerOverGameObject()){Hide();return;}
        var camera=Camera.main;if(!camera){Hide();return;}
        var ray=camera.ScreenPointToRay(Input.mousePosition);
        if(!VoxelCombatDamage.Ray(ray.origin,ray.GetPoint(200),out var hit,false,transform)){Hide();return;}
        float range=Range();Vector3 offset=hit.point-transform.position;
        落点=hit.point;正在瞄准=true;
        Draw(落点,Radius(),offset.magnitude<=range+.05f,hit.normal);
        DrawSenseRange();
        if(Input.GetMouseButtonDown(0))确认施放(选择槽位,落点);
    }
    float Range()
    {
        if(选择槽位==-2)return GetComponent<QingshanSwordTreasure>()?.SenseRange ?? SenseRange(gameObject);
        var skill=caster?caster.槽位内容(选择槽位) as ActiveDivineAbility:null;
        if(skill && (skill.结算方式==ActiveSkillKind.追踪弹 || skill.结算方式==ActiveSkillKind.定向水炮) && skill.范围>0)return Mathf.Min(SenseRange(gameObject),skill.范围);
        return SenseRange(gameObject);
    }
    float Radius()
    {
        if(选择槽位==-2)return GetComponent<QingshanSwordTreasure>()?.UltimateVoxelRadius ?? 4;
        var skill=caster?caster.槽位内容(选择槽位) as ActiveDivineAbility:null;
        if(!skill)return .6f;
        if(skill.结算方式==ActiveSkillKind.追踪弹)return 2.5f;
        if(skill.结算方式==ActiveSkillKind.向前冰柱)return 1.3f;
        return VoxelCombatDamage.AreaRadius(skill);
    }
    public bool 确认施放(int slot,Vector3 point)
    {
        if(!可接收输入 || 有锁定)return false;
        bool cast=false;
        if(slot>=0)cast=caster && caster.手动施放(slot,point);
        else if(slot==-2)cast=GetComponent<QingshanSwordTreasure>()?.TryCastUltimateAt(point) ?? false;
        else
        {
            foreach(var melee in GetComponents<BasicJiuba01>())if(melee.enabled){cast=melee.手动出手(point);break;}
            if(!cast)foreach(var ranged in GetComponents<BasicRemoteAttack01>())if(ranged.enabled){cast=ranged.手动出手(point);break;}
            // 邪眼使用原始落点与自己的神识/冷却检查，不受近战射程和普攻冷却阻止。
            bool eye=GetComponent<DevilEyeAbility>()?.跟随普攻瞄准(point) ?? false;
            cast|=eye;
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
        var skill=caster?caster.槽位内容(选择槽位) as ActiveDivineAbility:null;
        if(skill && skill.结算方式==ActiveSkillKind.向前冰柱)
        {
            var forward=point-transform.position;forward.y=0;forward=forward.sqrMagnitude>.0001f?forward.normalized:transform.forward;
            var start=transform.position+Vector3.up*.1f;var end=start+forward*skill.范围;var side=Vector3.Cross(Vector3.up,forward)*radius;
            ring.positionCount=5;ring.SetPositions(new[]{start-side,start+side,end+side,end-side,start-side});direction.SetPositions(new[]{start,end});return;
        }
        ring.positionCount=65;
        var rotation=Quaternion.FromToRotation(Vector3.up,normal);
        for(int i=0;i<65;i++){float angle=i*Mathf.PI*2/64;ring.SetPosition(i,point+rotation*new Vector3(Mathf.Cos(angle)*radius,.08f,Mathf.Sin(angle)*radius));}
        direction.SetPosition(0,transform.position+Vector3.up*.12f);direction.SetPosition(1,point+Vector3.up*.12f);
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
    void OnDisable(){Hide();held=false;}
    void OnDestroy(){if(material)Destroy(material);}
}

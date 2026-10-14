using UnityEngine;
using UnityEngine.UI;

/// <summary>龙身侧的临时三维追击星位；独立于暂停的仪轨提示，不接管技能输入。</summary>
[DefaultExecutionOrder(350)]
public sealed class CheDianComboCue : MonoBehaviour
{
    CheDianYeTingCompanion owner;
    Transform glyph, orbitA, orbitB;
    SpriteRenderer icon;
    GameObject shade;
    Material iconMaterial, ringMaterial, shadeMaterial;
    Mesh mesh;
    float visibility, phase;
    Image shadeImage;
    Text key;
    public bool Visible => visibility > .05f;
    public Transform Glyph => glyph;
    public float OrbitSpeed => 210;
    public void Confirm(){if(shade)shade.SetActive(false);Destroy(gameObject);}

    public void Initialize(CheDianYeTingCompanion dragon)
    {
        owner=dragon; gameObject.layer=2;
        glyph=new GameObject("临时追击星位").transform;glyph.SetParent(transform,false);
        var shader=Resources.Load<Shader>("UI/InkUI/Dynamic/InkStarVolume");
        ringMaterial=new Material(shader);ringMaterial.SetFloat("_ComboGlow",1.6f);
        mesh=UIInkSkillVolume.CreateRing();
        for(int i=0;i<2;i++)
        {
            var ring=new GameObject("高速仪轨",typeof(MeshFilter),typeof(MeshRenderer));ring.layer=2;ring.transform.SetParent(glyph,false);
            ring.GetComponent<MeshFilter>().sharedMesh=mesh;ring.GetComponent<MeshRenderer>().sharedMaterial=ringMaterial;
            ring.transform.localScale=Vector3.one*(i==0?.019f:.023f);
            if(i==0)orbitA=ring.transform;else orbitB=ring.transform;
        }
        var node=new GameObject("吐息图标",typeof(SpriteRenderer));node.layer=2;node.transform.SetParent(glyph,false);icon=node.GetComponent<SpriteRenderer>();
        var caster=owner.GetComponent<ActiveSkillCaster>();IPanelEntry entry=null;int slot=0;
        for(int i=0;i<6;i++)if(caster && caster.SlotContent(i) is ActiveDivineAbility a && a.神通id==CheDianYeTingDefinition.AbilityId){entry=a;slot=i;break;}
        icon.sprite=UIInkAbilityArt.Icon(entry);if(icon.sprite)node.transform.localScale=Vector3.one*(2.65f/Mathf.Max(.001f,Mathf.Max(icon.sprite.bounds.size.x,icon.sprite.bounds.size.y)));
        iconMaterial=new Material(Resources.Load<Shader>("UI/InkUI/Dynamic/InkCooldown"));iconMaterial.SetFloat("_ZWrite",1);iconMaterial.SetFloat("_ZTest",4);iconMaterial.SetFloat("_ComboGlow",.8f);icon.sharedMaterial=iconMaterial;
        var label=new GameObject("追击按键",typeof(RectTransform),typeof(Canvas));label.layer=2;label.transform.SetParent(glyph,false);label.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
        var rect=label.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(180,40);rect.localScale=Vector3.one*.012f;rect.localPosition=Vector3.down*1.25f;
        var textRect=UIBuildUtils.CreateRect("按键",rect);UIBuildUtils.Stretch(textRect);key=textRect.gameObject.AddComponent<Text>();
        foreach(var font in Resources.FindObjectsOfTypeAll<Font>())if(font && font.name.ToLowerInvariant().Contains("simhei")){key.font=font;break;}
        if(!key.font)key.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");key.fontSize=24;key.alignment=TextAnchor.MiddleCenter;key.raycastTarget=false;
        key.text=(caster && slot<caster.快捷键.Length?caster.快捷键[slot].ToString().Replace("Alpha",""):(slot+1).ToString())+" · 续吐";
        shade=new GameObject("龙身追击聚光",typeof(RectTransform),typeof(Canvas));shade.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;shade.GetComponent<Canvas>().sortingOrder=1490;
        shadeImage=UIBuildUtils.CreateImage("周身暗场",shade.transform,Color.white);UIBuildUtils.Stretch(shadeImage.rectTransform);shadeImage.raycastTarget=false;
        shadeMaterial=new Material(Resources.Load<Shader>("Abilities/CheDianYeTing/CheDianFocusShade"));shadeImage.material=shadeMaterial;
    }
    void LateUpdate()
    {
        if(!owner || !owner.Companion){Destroy(gameObject);return;}
        var camera=Camera.main;if(!camera)return;
        float dt=Time.timeScale>0?Time.unscaledDeltaTime:0;phase+=dt;
        float desired=owner.Breath!=null&&owner.Breath.ComboWindow?1:0;
        visibility=Mathf.MoveTowards(visibility,desired,dt*(desired>0?6:4));
        transform.position=owner.Companion.transform.position+Vector3.up*2+camera.transform.right*2.4f;
        glyph.rotation=camera.transform.rotation;glyph.localScale=Vector3.one*visibility*(1+.11f*Mathf.Sin(phase*15));
        orbitA.localRotation=Quaternion.Euler(60,25,phase*210);orbitB.localRotation=Quaternion.Euler(110,75,-phase*165);
        icon.color=new Color(1,1,1,visibility);key.color=new Color(.9f,1,.83f,visibility);
        float influence=CheDianComboFocus.CameraWeight;
        shadeImage.enabled=influence>.001f;
        var head=owner.Breath?.Head;var center=Vector3.Lerp(owner.Companion.transform.position+Vector3.up*2,head?head.position:owner.Companion.transform.position,.45f);
        var uv=camera.WorldToViewportPoint(center);var hint=camera.WorldToViewportPoint(transform.position);
        shadeMaterial.SetVector("_DragonEllipse",new Vector4(uv.x,uv.y,.23f,.31f));
        shadeMaterial.SetVector("_CueEllipse",new Vector4(hint.x,hint.y,.08f,.12f));
        shadeMaterial.SetFloat("_Strength",influence*.72f);
        if(!CheDianComboFocus.For(owner)&&visibility<=0&&influence<=.001f)Destroy(gameObject);
    }
    void OnDestroy()
    {
        if(shade)Destroy(shade);if(mesh)Destroy(mesh);if(iconMaterial)Destroy(iconMaterial);if(ringMaterial)Destroy(ringMaterial);if(shadeMaterial)Destroy(shadeMaterial);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>循环法宝列：持久条目沿连续坐标运动，只有屏外池项允许更换内容。</summary>
public sealed class UITreasureCarousel : MonoBehaviour
{
    sealed class Card {public Image Icon;public Text Name;public Outline Outline;}
    readonly List<TreasureDefinition> items=new List<TreasureDefinition>();readonly Card[] cards=new Card[5];
    float position,velocity;int target;bool ready;
    public float Position=>position;public int Target=>target;public bool IsMoving=>Mathf.Abs(target-position)>.001f||Mathf.Abs(velocity)>.01f;
    static int Wrap(int value,int count)=>(value%count+count)%count;
    public void Initialize(Font font)
    {
        gameObject.AddComponent<CanvasGroup>().blocksRaycasts=false;
        for(int i=0;i<cards.Length;i++){
            var icon=UIBuildUtils.CreateImage("MovingTreasure_"+i,transform,Color.clear);icon.preserveAspect=true;icon.raycastTarget=false;
            icon.rectTransform.anchorMin=icon.rectTransform.anchorMax=new Vector2(.5f,.5f);
            var outline=icon.gameObject.AddComponent<Outline>();outline.effectDistance=new Vector2(2,-2);
            var name=UIBuildUtils.CreateText("TreasureName",icon.transform,font,"",13,TextAnchor.MiddleCenter,Color.clear);name.raycastTarget=false;
            name.rectTransform.anchorMin=new Vector2(.5f,0);name.rectTransform.anchorMax=new Vector2(.5f,0);name.rectTransform.pivot=new Vector2(.5f,1);name.rectTransform.anchoredPosition=new Vector2(0,-5);name.rectTransform.sizeDelta=new Vector2(170,24);
            cards[i]=new Card{Icon=icon,Name=name,Outline=outline};
        }
    }
    public void SetItems(IReadOnlyList<TreasureDefinition> source,TreasureDefinition selected)
    {
        bool changed=source.Count!=items.Count;
        if(!changed)for(int i=0;i<items.Count;i++)if(items[i]!=source[i]){changed=true;break;}
        if(!changed&&ready)return;
        items.Clear();for(int i=0;i<source.Count;i++)items.Add(source[i]);Align(selected);
    }
    public void Align(TreasureDefinition selected){target=Mathf.Max(0,items.IndexOf(selected));position=target;velocity=0;ready=true;}
    public TreasureDefinition Scroll(int delta){if(items.Count==0)return null;if(items.Count>1)target+=delta;return items[Wrap(target,items.Count)];}
    public void Step(float delta,float reveal,bool reduced)
    {
        if(!ready||items.Count==0){foreach(var c in cards)c.Icon.gameObject.SetActive(false);return;}
        if(reduced){position=target;velocity=0;}else if(delta>0&&IsMoving)position=Mathf.SmoothDamp(position,target,ref velocity,.105f,Mathf.Infinity,Mathf.Min(.05f,delta));
        if(Mathf.Abs(target-position)<.001f&&Mathf.Abs(velocity)<.01f){position=target;velocity=0;}
        int anchor=Mathf.RoundToInt(position);
        for(int offset=-2;offset<=2;offset++){
            int index=anchor+offset;var c=cards[Wrap(index,cards.Length)];float distance=Mathf.Abs(index-position);
            float fade=distance<=1?Mathf.Lerp(1,.33f,Mathf.SmoothStep(0,1,distance)):.33f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1,1.65f,distance)));
            float alpha=fade*reveal;if(items.Count==1&&index!=target)alpha=0;c.Icon.gameObject.SetActive(alpha>.001f);if(alpha<=.001f)continue;
            var item=items[Wrap(index,items.Count)];float focus=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(distance));
            c.Icon.sprite=item.图标;c.Icon.enabled=item.图标;c.Icon.color=new Color(1,1,1,alpha);
            c.Icon.rectTransform.anchoredPosition=new Vector2(0,(position-index)*156);c.Icon.rectTransform.sizeDelta=Vector2.one*Mathf.Lerp(82,197.5f,focus);
            c.Name.text=item.DisplayName;c.Name.fontSize=Mathf.RoundToInt(Mathf.Lerp(13,15,focus));c.Name.color=new Color(.94f,.92f,.83f,alpha*.9f);
            c.Outline.enabled=focus>.01f;c.Outline.effectColor=new Color(1,.93f,.68f,alpha*focus*.95f);
        }
    }
    public void ResetMotion(){position=target;velocity=0;}
}

using UnityEngine;

/// <summary>原神通/法则入口共用宣纸装备页；保留旧页对象和存档索引。</summary>
public static class UIInkEquipmentPage
{
    public static void 应用(Transform root)
    {
        if(!UIInkNavigation.启用)return;var panel=root.GetComponent<CharacterPanelUI>();if(!panel||panel.tabs.Count<5)return;
        var page=panel.tabs[(int)CharacterTab.神通].page;if(!page)return;
        var old=page.GetComponent<UIInkSkillsPage>();if(old)old.enabled=false;
        var view=page.GetComponent<UIInkBagPage>();if(!view)view=page.AddComponent<UIInkBagPage>();view.BuildEquipment(panel,root.GetComponent<UIPanelData>());
    }
}

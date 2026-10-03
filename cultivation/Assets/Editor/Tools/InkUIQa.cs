using System;
using System.Reflection;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>只影响编辑器 GameView，按真实画布尺寸验收 Overlay 截图。</summary>
public static class InkUIQa
{
    static bool guard;
    static InkUIQa()
    {
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredEditMode) guard = false; };
        EditorApplication.update += () =>
        {
            if (!guard || !EditorApplication.isPlaying) return;
            foreach (var loader in UnityEngine.Object.FindObjectsOfType<PlayerAbilityLoader>()) loader.enabled = false;
            foreach (var storm in UnityEngine.Object.FindObjectsOfType<LeiYun>()) storm.enabled = false;
            foreach (var storm in UnityEngine.Object.FindObjectsOfType<QianJieLeiYu>()) storm.enabled = false;
        };
    }
    public static string Panel(string command)
    {
        if (command == "before" || command == "after")
        {
            EditorPrefs.SetBool("InkUI.QA.Before", command == "before");
            return "OK mode=" + command + "; restart Play to apply";
        }
        if (!EditorApplication.isPlaying) return "FAIL inkqa: requires Play";
        var role = UnityEngine.Object.FindObjectOfType<CharacterPanelUI>(true);
        var cultivation = UnityEngine.Object.FindObjectOfType<CultivationUI>(true);
        var alchemy = UnityEngine.Object.FindObjectOfType<炼丹界面>(true);
        var field = UnityEngine.Object.FindObjectOfType<灵田界面>(true);
        var contribution = UnityEngine.Object.FindObjectOfType<功德堂兑换>(true);
        var pause = UnityEngine.Object.FindObjectOfType<PauseMenuUI>(true);
        var death = UnityEngine.Object.FindObjectOfType<DeathScreenUI>(true);
        if (command == "audit")
        {
            var log = new StringBuilder();
            foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
            {
                log.AppendLine(canvas.name + " order=" + canvas.sortingOrder);
                foreach (var image in canvas.GetComponentsInChildren<Image>())
                    if (image.rectTransform.rect.width > 1000) log.AppendLine("  " + image.name + " size=" + image.rectTransform.rect.size + " sprite=" + (image.sprite != null ? image.sprite.name : "none"));
                foreach (var scroll in canvas.GetComponentsInChildren<ScrollRect>())
                    log.AppendLine("  scroll=" + scroll.name + " viewport=" + scroll.viewport.rect.size + " content=" + scroll.content.rect.size + " bar=" + (scroll.verticalScrollbar != null));
                foreach (var text in canvas.GetComponentsInChildren<Text>())
                    if (text.name.StartsWith("InkSlotNumber_"))
                        log.AppendLine("  " + text.name + " text=" + text.text + " rect=" + text.rectTransform.rect + " color=" + text.color + " font=" + (text.font != null ? text.font.name : "none"));
            }
            return log.ToString();
        }
        if (command == "seed")
        {
            guard = true;
            // 截图只检查 UI 状态；停止任务演出和能力装载，避免临时被动攻击场景 NPC。
            foreach (var intro in UnityEngine.Object.FindObjectsOfType<主线开场>(true)) { intro.StopAllCoroutines(); intro.enabled = false; }
            foreach (var loader in UnityEngine.Object.FindObjectsOfType<PlayerAbilityLoader>(true)) loader.enabled = false;
            foreach (var storm in UnityEngine.Object.FindObjectsOfType<LeiYun>(true)) storm.enabled = false;
            foreach (var storm in UnityEngine.Object.FindObjectsOfType<QianJieLeiYu>(true)) storm.enabled = false;
            if (起名界面.实例 != null) { 起名界面.实例.StopAllCoroutines(); 起名界面.实例.收起(); }
            if (黑幕字幕.实例 != null) { 黑幕字幕.实例.StopAllCoroutines(); 黑幕字幕.实例.收起(0); }
            while (黑幕字幕.演出中) 黑幕字幕.结束演出();
            var data = role.GetComponent<UIPanelData>();
            foreach (var ability in data.神通)
            {
                if (ability is ActiveDivineAbility active) data.获得主动(active);
                if (ability is PassiveDivineAbility passive) data.获得被动(passive);
            }
            foreach (var id in AssetDatabase.FindAssets("t:GongFaDefinition"))
                data.学会(AssetDatabase.LoadAssetAtPath<GongFaDefinition>(AssetDatabase.GUIDToAssetPath(id)));
            foreach (var recipe in 炼丹炉.取().全部丹方()) 对话标记.添加(炼丹炉.丹方标记(recipe.id));
            for (int i = 0; i < Mathf.Min(5, data.已获得主动神通.Count); i++) data.EquipToSlot(i, data.已获得主动神通[i]);
            return "OK temporary actual catalog: active=" + data.已获得主动神通.Count + "; passive=" + data.已获得被动神通.Count;
        }
        if (command == "validate") return Validate(role);
        if (role != null) role.SetOpen(false);
        if (cultivation != null) cultivation.Hide();
        if (alchemy != null) alchemy.Close();
        if (field != null) field.关闭();
        if (contribution != null) contribution.Close();
        if (pause != null && pause.IsPaused) pause.继续();
        if (death != null) death.Hide();
        DialogueUI.关闭();
        if (起名界面.实例 != null) { 起名界面.实例.StopAllCoroutines(); 起名界面.实例.收起(); }
        foreach (var portal in UnityEngine.Object.FindObjectsOfType<Teleporter>(true)) portal.关面板();
        foreach (var tower in UnityEngine.Object.FindObjectsOfType<TowerUI>(true)) tower.HideDeathChoice();
        foreach (var plotUI in UnityEngine.Object.FindObjectsOfType<灵田地块界面>(true)) UnityEngine.Object.Destroy(plotUI.gameObject);
        if (command.StartsWith("role:")) { role.SetOpen(true); role.ShowTabByIndex(int.Parse(command.Substring(5))); }
        else if (command.StartsWith("cultivation:"))
        {
            if (cultivation == null) cultivation = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CultivationUI.prefab")).GetComponent<CultivationUI>();
            cultivation.Show(); cultivation.SwitchTab(int.Parse(command.Substring(12)));
        }
        else if (command == "alchemy" || command == "materials")
        {
            if (alchemy == null) alchemy = new GameObject("InkQA_Alchemy").AddComponent<炼丹界面>();
            alchemy.Open();
            if (command == "materials")
                foreach (var button in UnityEngine.Object.FindObjectsOfType<Button>())
                    if (button.name == "主材格") { button.onClick.Invoke(); break; }
        }
        else if (command == "field")
        {
            if (field == null) field = new GameObject("InkQA_Field").AddComponent<灵田界面>();
            field.打开();
        }
        else if (command == "plot" || command.StartsWith("plot:"))
        {
            int index = command == "plot" ? 0 : int.Parse(command.Substring(5));
            var farm = 灵田.取();
            if (farm.总块数 == 0)
            {
                var crop = 灵植库.全部[0];
                // 临时加载三个真实类型的 UI 验收状态，成熟时间直接取作物定义；不保存、不用于合法性判定。
                farm.导入(new List<灵田地块状态> {
                    new 灵田地块状态(),
                    new 灵田地块状态 { 作物id = crop.id, 已生长天数 = crop.成熟天数 * .5f },
                    new 灵田地块状态 { 作物id = crop.id, 已生长天数 = crop.成熟天数 }
                });
            }
            var plot = UnityEngine.Object.FindObjectOfType<灵田地块>();
            foreach (var candidate in UnityEngine.Object.FindObjectsOfType<灵田地块>()) if (candidate.编号 == index) plot = candidate;
            if (plot == null || plot.编号 != index) { plot = new GameObject("InkQA_PlotContext").AddComponent<灵田地块>(); plot.编号 = index; }
            灵田地块界面.造(plot);
        }
        else if (command == "contribution")
        {
            if (contribution == null) contribution = new GameObject("InkQA_Contribution").AddComponent<功德堂兑换>();
            contribution.Open();
        }
        else if (command == "pause") pause.暂停();
        else if (command == "death") death.Show();
        else if (command == "dialogue")
        {
            var npc = UnityEngine.Object.FindObjectOfType<NpcDialogue>();
            if (npc == null) return "FAIL no dialogue source in scene";
            DialogueUI.打开(npc);
        }
        else if (command == "tower") UnityEngine.Object.FindObjectOfType<TowerUI>(true).ShowDeathChoice(1, 10);
        else if (command == "teleport") UnityEngine.Object.FindObjectOfType<Teleporter>(true).开面板();
        else if (command == "close") { }
        else if (command == "hud") { }
        else if (command == "toast") ToastUI.提示("材料不足", 20);
        else if (command == "name") 起名界面.确保().StartCoroutine(起名界面.取名("那个少年的名字叫做：", null));
        else if (command == "menu") UnityEngine.Object.FindObjectOfType<MainMenuUI>().CloseSlots();
        else if (command == "saves") UnityEngine.Object.FindObjectOfType<MainMenuUI>().OpenSlots(false);
        else return "FAIL unknown panel=" + command;
        Canvas.ForceUpdateCanvases();
        return "OK " + command + "; Screen=" + Screen.width + "x" + Screen.height + "; timeScale=" + Time.timeScale;
    }
    static string Validate(CharacterPanelUI role)
    {
        var data = role.GetComponent<UIPanelData>();
        var log = new StringBuilder(); int failed = 0;
        Action<string, bool> check = (label, ok) => { log.AppendLine((ok ? "PASS " : "FAIL ") + label); if (!ok) failed++; };
        var saved = new List<UnityEngine.Object>(data.主动技能);
        var pending = data.待装备神通;
        var a = data.已获得主动神通[0]; var b = data.已获得主动神通[1]; var p = data.已获得被动神通[0];
        try
        {
            data.ClearSlot(0); data.ClearSlot(1); data.EquipToSlot(0, a); data.EquipToSlot(0, b);
            check("occupied slot drop replaces content", data.主动技能[0] == b);
            data.EquipToSlot(0, p); check("passive rejected by data entry point", data.主动技能[0] == b);
            data.BeginPendingEquip(a); check("pending click rejects occupied slot", !data.HandleSlotClicked(0) && data.待装备神通 == a);
            check("pending click equips empty slot", data.HandleSlotClicked(1) && data.主动技能[1] == a && data.待装备神通 == null);
            bool enabled = data.IsPassiveEnabled(p);
            data.TogglePassive(p); check("passive enable/disable uses original data", data.IsPassiveEnabled(p) != enabled); data.TogglePassive(p);
            role.SetOpen(true); role.ShowTabByIndex(2); Canvas.ForceUpdateCanvases();
            var bar = role.GetComponentInChildren<UIActiveSkillBar>();
            check("six fixed numbered slots", bar.slots.Count == 6);
            UIDragContext.Begin(a, role.transform, role.GetComponentInChildren<Text>().font);
            check("drag ghost outside list masks and ignores raycasts", UIDragContext.Ghost != null && UIDragContext.Ghost.GetComponentInParent<Mask>() == null && !UIDragContext.Ghost.GetComponent<Image>().raycastTarget);
            bar.slots[0].OnDrop(new PointerEventData(EventSystem.current));
            check("slot OnDrop equips and ends drag", data.主动技能[0] == a && !UIDragContext.Dragging);
            role.ShowTabByIndex(3);
            var treasureBar = role.GetComponentInChildren<UIActiveSkillBar>();
            check("treasure page shares same equipment", treasureBar.slots[0].Content == a);
            role.ShowTabByIndex(4);
            var arrayBar = role.GetComponentInChildren<UIActiveSkillBar>();
            check("array page shares same equipment", arrayBar.slots[0].Content == a);
            role.ShowTabByIndex(2);
            for (int i = 0; i < bar.slots.Count; i++)
            {
                var slot = bar.slots[i]; var pos = ((RectTransform)slot.transform).anchoredPosition;
                var expected = new Vector2(Mathf.Sin(i * Mathf.PI / 3), Mathf.Cos(i * Mathf.PI / 3)) * 143;
                var number = bar.transform.Find("InkNumbers/InkSlotNumber_" + i).GetComponent<Text>();
                check("clockwise slot " + (i + 1), slot.index == i && Vector2.Distance(pos, expected) < 1 && number.text == (i + 1).ToString() && number.font != null && number.isActiveAndEnabled);
            }
            foreach (var scroll in role.GetComponentsInChildren<ScrollRect>())
                check("permanent scroll " + scroll.name, scroll.verticalScrollbar != null && scroll.verticalScrollbarVisibility == ScrollRect.ScrollbarVisibility.Permanent);
            var list = role.GetComponentInChildren<UIEntryList>();
            var ids = new HashSet<int>(); foreach (var row in list.container.GetComponentsInChildren<UIEntryRow>()) ids.Add(row.GetInstanceID());
            list.RebuildFromSource(); bool reuse = true;
            foreach (var row in list.container.GetComponentsInChildren<UIEntryRow>()) reuse &= ids.Contains(row.GetInstanceID());
            check("rows reused on data refresh", reuse && ids.Count > 0);
            foreach (var row in list.container.GetComponentsInChildren<UIEntryRow>())
                if (row.Entry is PassiveDivineAbility) { row.OnBeginDrag(new PointerEventData(EventSystem.current)); check("passive cannot start equipment drag", !UIDragContext.Dragging); break; }
            var slotHit = bar.slots[0].GetComponent<InkUIHitShape>(); var rt = (RectTransform)slotHit.transform;
            Vector2 center = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
            Vector2 corner = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.max - Vector2.one));
            check("irregular slot center accepts hit", slotHit.IsRaycastLocationValid(center, null));
            check("irregular slot transparent corner rejects hit", !slotHit.IsRaycastLocationValid(corner, null));
            role.ShowTabByIndex(1);
            foreach (var image in role.GetComponentsInChildren<Image>())
                if (image.name == "Fill") check("realm fill has sprite and stays Filled", image.sprite != null && image.type == Image.Type.Filled);
            role.ShowTabByIndex(5); Canvas.ForceUpdateCanvases();
            foreach (var spirits in role.GetComponentsInChildren<UIEntryList>())
                if (spirits.source == ListSource.战阵成员)
                {
                    int count = spirits.container.GetComponentsInChildren<UIEntryRow>().Length;
                    check("spirit catalog complete (" + count + "/" + data.GetSpirits().Count + ")", count == data.GetSpirits().Count);
                    ids.Clear(); foreach (var row in spirits.container.GetComponentsInChildren<UIEntryRow>()) ids.Add(row.GetInstanceID());
                    spirits.RebuildFromSource(); reuse = true;
                    foreach (var row in spirits.container.GetComponentsInChildren<UIEntryRow>()) reuse &= ids.Contains(row.GetInstanceID());
                    check("spirit pool reuses existing rows", reuse);
                }
            role.SetOpen(false); check("closing role restores timeScale", Time.timeScale > 0);
        }
        finally { data.主动技能 = saved; data.待装备神通 = pending; UIDragContext.End(); data.RaiseChanged(); }
        return "Failures=" + failed + "\n" + log;
    }
    public static string SetSize(int width, int height)
    {
        var assembly = typeof(Editor).Assembly;
        var type = assembly.GetType("UnityEditor.GameViewSizes");
        var singleton = typeof(ScriptableSingleton<>).MakeGenericType(type);
        var instance = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
        var group = type.GetMethod("GetGroup").Invoke(instance, new[] { Enum.Parse(groupType, "Standalone") });
        var gt = group.GetType();
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var modeType = assembly.GetType("UnityEditor.GameViewSizeType");
        int builtin = (int)gt.GetMethod("GetBuiltinCount").Invoke(group, null);
        int count = builtin + (int)gt.GetMethod("GetCustomCount").Invoke(group, null);
        int index = -1;
        for (int i = 0; i < count; i++)
        {
            var candidate = gt.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
            if ((int)sizeType.GetProperty("width").GetValue(candidate) == width && (int)sizeType.GetProperty("height").GetValue(candidate) == height)
            { index = i; break; }
        }
        if (index < 0)
        {
            var size = Activator.CreateInstance(sizeType, new object[] { Enum.Parse(modeType, "FixedResolution"), width, height, "InkUI QA " + width + "x" + height });
            gt.GetMethod("AddCustomSize").Invoke(group, new[] { size });
            index = count;
        }
        var gameViewType = assembly.GetType("UnityEditor.GameView");
        var view = EditorWindow.GetWindow(gameViewType);
        gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, index);
        view.Repaint();
        var current = gameViewType.GetProperty("currentGameViewSize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(view);
        return "GameView requested " + width + "x" + height + "; Screen currently " + Screen.width + "x" + Screen.height
            + "; selected=" + index + "; current=" + current + "; batch=" + Application.isBatchMode + "; windows=" + Resources.FindObjectsOfTypeAll(gameViewType).Length;
    }
}

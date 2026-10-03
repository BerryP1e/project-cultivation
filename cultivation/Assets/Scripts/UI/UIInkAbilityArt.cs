using UnityEngine;
using UnityEngine.UI;

/// <summary>神通展示素材按真实 id 查找；不写 ScriptableObject、表或装备状态。</summary>
public static class UIInkAbilityArt
{
    static bool Enabled
    {
        get
        {
#if UNITY_EDITOR
            return InkUITheme.Enabled && !UnityEditor.EditorPrefs.GetBool("InkUI.QA.AbilityArtBefore", false);
#else
            return InkUITheme.Enabled;
#endif
        }
    }
    static string Key(IPanelEntry entry)
    {
        var ability = entry as DivineAbilityDefinition;
        if (ability == null || string.IsNullOrEmpty(ability.神通id)) return null;
        return ability.神通id.StartsWith("ability_") ? ability.神通id.Substring(8) : ability.神通id;
    }

    public static Sprite Icon(IPanelEntry entry)
    {
        if (entry == null) return null;
        string key = Key(entry);
        if (Enabled && key != null)
        {
            var sprite = InkUITheme.Load("AbilityArt/icon-ability-" + key);
            if (sprite != null) return sprite;
        }
        return entry.DisplayIcon;
    }

    public static Sprite Artwork(IPanelEntry entry)
    {
        string key = Key(entry);
        return Enabled && key != null ? InkUITheme.Load("AbilityArt/art-ability-" + key) : null;
    }

    public static void RefreshDetail(UIEntryInfo info)
    {
        if (info == null || !Enabled) return;
        bool skillsPage = false;
        for (var parent = info.transform.parent; parent != null; parent = parent.parent)
            if (parent.name == "Page_神通") { skillsPage = true; break; }
        if (!skillsPage) return;
        var image = info.transform.Find("InkAbilityArtwork")?.GetComponent<Image>();
        var sprite = Artwork(info.Current);
        if (sprite == null)
        {
            if (image != null) image.gameObject.SetActive(false);
            return;
        }
        if (image == null) image = UIBuildUtils.CreateImage("InkAbilityArtwork", info.transform, Color.white);
        image.gameObject.SetActive(true);
        image.sprite = sprite; image.color = Color.white;
        image.type = Image.Type.Simple; image.preserveAspect = true; image.raycastTarget = false;
        UIBuildUtils.Place(image.rectTransform, new Vector2(0, 1), Vector2.one,
            new Vector2(42, -472), new Vector2(-42, -210));
        if (info.iconImage != null) info.iconImage.gameObject.SetActive(false);
        if (info.descriptionText != null)
            UIBuildUtils.Place(info.descriptionText.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(72, 142), new Vector2(-72, -490));
    }
}

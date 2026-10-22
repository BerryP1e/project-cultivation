using System;
using System.Collections.Generic;

public static class WeaponCatalog
{
    public static IReadOnlyList<WeaponDefinition> 全部=>PanelDatabase.取()?.武器??(IReadOnlyList<WeaponDefinition>)Array.Empty<WeaponDefinition>();
    public static WeaponDefinition 找(string id)
    {
        if(string.IsNullOrEmpty(id))return null;
        foreach(var weapon in 全部)if(weapon&&weapon.武器id==id)return weapon;
        return null;
    }
}

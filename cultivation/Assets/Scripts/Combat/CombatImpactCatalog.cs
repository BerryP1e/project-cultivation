using System;
using System.Collections.Generic;
using UnityEngine;

public enum CombatEnvironmentShape { None, Sphere, Ellipsoid }

/// <summary>Contact policies. Biological radius is owned by attack geometry; visual size does not change damage.</summary>
[CreateAssetMenu(menuName = "修仙/战斗接触目录")]
public sealed class CombatImpactCatalog : ScriptableObject
{
    [Serializable] public sealed class Entry
    {
        public string id;
        public string 生物命中特效id;
        public string 地表特效id;
        public Vector3 地表特效偏移;
        public CombatEnvironmentShape 环境形状;
        [Min(0)] public float 默认半径 = .6f;
        [Min(0)] public float 半径倍率 = 1f;
        [Min(0)] public float 深度倍率 = 1f;
        public bool 固定深度;
        [Min(0)] public float 深度 = .325f;
        [Min(0)] public float 落点引导半径倍率 = 1f;
        public float 半径(float basis) => Mathf.Max(0f, basis) * Mathf.Max(0f, 半径倍率);
        public float 破坏深度(float basis) => 固定深度 ? Mathf.Max(0f, 深度) : Mathf.Max(0f, basis) * Mathf.Max(0f, 深度倍率);
    }
    public Entry[] 规则 = Array.Empty<Entry>();
    Dictionary<string, Entry> lookup;
    public Entry 查找(string id)
    {
        if (lookup == null) { lookup = new Dictionary<string, Entry>(); foreach (var entry in 规则) if (entry != null && !string.IsNullOrEmpty(entry.id)) lookup[entry.id] = entry; }
        return id != null && lookup.TryGetValue(id, out var found) ? found : null;
    }
    void OnValidate() => lookup = null;
}

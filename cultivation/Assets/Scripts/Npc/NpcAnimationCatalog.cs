using System;
using UnityEngine;

/// <summary>Player-readable NPC state/index mappings baked from the actual controllers.</summary>
public sealed class NpcAnimationCatalog : ScriptableObject
{
    [Serializable] public class Action
    {
        public string name;
        public int index;
        public AnimationClip clip;
        public string statePath;
    }
    [Serializable] public class Controller
    {
        public RuntimeAnimatorController controller;
        public bool usesActionParameter;
        public Action[] actions;
    }
    public Controller[] controllers;
    static NpcAnimationCatalog loaded;

    public static Controller Find(RuntimeAnimatorController controller)
    {
        while (controller is AnimatorOverrideController overrides)
            controller = overrides.runtimeAnimatorController;
        if (loaded == null) loaded = Resources.Load<NpcAnimationCatalog>("NPC数据/NPC动作映射");
        if (loaded == null || loaded.controllers == null) return null;
        foreach (var item in loaded.controllers)
            if (item.controller == controller) return item;
        return null;
    }
}

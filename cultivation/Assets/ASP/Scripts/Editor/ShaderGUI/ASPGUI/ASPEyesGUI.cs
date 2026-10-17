
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using LWGUI;

namespace LWGUI.ASP
{
	public static class ASPEyesEditorGUI
	{
		 private static void SetKeyword(Material material, string keyword, bool state)
        {
            //UnityEngine.Debug.Log(keyword + " = "+state);
            if (state)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

        private static void SetPropertyFloat(Material material, string propName, bool state)
        {
            //UnityEngine.Debug.Log(propName + " = "+state);
            if (state)
            {
                material.SetFloat(propName, 1f);
            }
            else
            {
                material.SetFloat(propName, 0f);
            }
        }

        public static void DoCustomFooter(LWGUI lwgui)
        {
            SetKeyword(lwgui.material, "_NORMALMAP", lwgui.material.GetTexture("_BumpMap"));
            SetPropertyFloat(lwgui.material,"_HasMatCapNormal", lwgui.material.GetTexture("_BumpMap"));
            
            SetKeyword(lwgui.material, "_MATCAP_HIGHLIGHT_MAP", lwgui.material.GetTexture("_MatCapReflectionMap"));
            SetPropertyFloat(lwgui.material,"_IsUsingMatcapReflectMap", lwgui.material.GetTexture("_MatCapReflectionMap"));
            SetKeyword(lwgui.material, "_EYE_HIGHLIGHT_MAP", lwgui.material.GetTexture("_EyeHighlightMap1"));
            
            SetKeyword(lwgui.material, "_RECEIVE_SHADOWS_OFF", lwgui.material.GetFloat("_ReceiveShadows") <= 0);
            SetKeyword(lwgui.material, "_SURFACE_TYPE_TRANSPARENT", lwgui.material.GetFloat("_SurfaceType") >= 1.0f);
            if (lwgui.material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"))
            {
                lwgui.material.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                lwgui.material.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                lwgui.material.SetOverrideTag("RenderType", "Transparent");
                lwgui.material.SetOverrideTag("Queue", "Transparent");
                lwgui.material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                lwgui.material.SetInt("_ZWrite", 0);
            }
            else
            {
                
                lwgui.material.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                lwgui.material.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                lwgui.material.SetOverrideTag("RenderType", "Opaque");
                lwgui.material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                lwgui.material.SetInt("_ZWrite", 1);
            }
            
            if (lwgui.material.GetFloat("_OverrideZTest") <= 0)
            {
                lwgui.material.SetFloat("_ZTest", 4.0f);
            }
            
            // Render settings
            if (lwgui.material.GetFloat("_OverrideZTest") <= 0)
            {
                EditorGUILayout.LabelField("RenderQueue adjustment required enable override ZTest Option inside Advance Setup.");
                GUI.enabled = false;
            }
#if UNITY_2019_4_OR_NEWER
            if (SupportedRenderingFeatures.active.editableMaterialRenderQueue)
#endif
            {
                lwgui.materialEditor.RenderQueueField();
            }
        }
        
		
        [InitializeOnLoadMethod]
        private static void RegisterEvent()
        {
            ASPEyesGUI.onDrawCustomFooter += DoCustomFooter;
        }
	}

	public class ASPEyesGUI : LWGUI
	{
		/// <summary>
		/// Called when switch to a new Material Window, each window has a LWGUI instance
		/// </summary>
		public ASPEyesGUI() { }

        public static LWGUICustomGUIEvent onDrawCustomFooter;
        public static LWGUICustomGUIEvent onDrawCustomHeader;
        
        protected override void OnDrawCustomHeader()
        {
            base.OnDrawCustomHeader();
            onDrawCustomHeader?.Invoke(this);
        }
        
        protected override void OnDrawCustomFooter()
        {
            base.OnDrawCustomFooter();
            onDrawCustomFooter?.Invoke(this);
            
        }
    }
} 
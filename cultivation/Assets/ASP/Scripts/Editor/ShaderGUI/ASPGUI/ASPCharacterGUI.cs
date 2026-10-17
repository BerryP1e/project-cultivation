
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using LWGUI;

namespace LWGUI.ASP
{
	public static class ASPCharacterEditorGUI
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
            var isStylizePBRMode = lwgui.material.IsKeywordEnabled("_STYLE_STYLIZEPBR");
            var isCelShadingMode = lwgui.material.IsKeywordEnabled("_STYLE_CELSHADING");
            SetKeyword(lwgui.material, "_NORMALMAP", lwgui.material.GetTexture("_BumpMap"));
            SetKeyword(lwgui.material, "_METALLICSPECGLOSSMAP", lwgui.material.GetTexture("_OSMTexture") && isStylizePBRMode);
            SetKeyword(lwgui.material, "_RAMP_OCCLUSION_MAP", lwgui.material.GetTexture("_RampOcclusionMap") && !isStylizePBRMode);
            
            SetKeyword(lwgui.material, "_FACESHADOW", lwgui.material.GetTexture("_FaceShadowMap") && !isStylizePBRMode);
            SetKeyword(lwgui.material, "_HAIRMAP", lwgui.material.GetTexture("_HairHighlightMaskMap") && isCelShadingMode);
            SetKeyword(lwgui.material, "_SSSMAP", !isStylizePBRMode && lwgui.material.GetTexture("_SSSRampMap") && lwgui.material.GetFloat("_EnableSSSRamp") > 0);
            
            //uncomment below line to enable clip map feature
            //if(lwgui.material.HasFloat("_IsUsingClipMap"))
            //SetKeyword(lwgui.material, "_CLIP_MAP", lwgui.material.GetFloat("_IsUsingClipMap") > 0 && lwgui.material.GetTexture("_ClipMap") );
            
            SetKeyword(lwgui.material, "_MATCAP_HIGHLIGHT_MAP", lwgui.material.GetTexture("_MatCapReflectionMap"));
            if (lwgui.material.GetTexture("_StandardBrdfLut") == null)
            {
                //lwgui.material.SetTexture("_StandardBrdfLut", Resources.Load<Texture>("brdf_lut"));
            }
            
            SetPropertyFloat(lwgui.material,"_IsUsingOSMTexture", lwgui.material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"));
            SetPropertyFloat(lwgui.material,"_IsUsingRampOcclusionTexture", lwgui.material.IsKeywordEnabled("_RAMP_OCCLUSION_MAP") && !isStylizePBRMode);
            SetPropertyFloat(lwgui.material,"_IsUsingMatcapReflectMap", lwgui.material.GetTexture("_MatCapReflectionMap"));
            

            SetKeyword(lwgui.material, "_RECEIVE_SHADOWS_OFF", lwgui.material.GetFloat("_ReceiveShadows") <= 0);
            SetKeyword(lwgui.material, "_SURFACE_TYPE_TRANSPARENT", lwgui.material.GetFloat("_SurfaceType") >= 1.0f);
            if (lwgui.material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"))
            {
                lwgui.material.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                lwgui.material.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                lwgui.material.SetOverrideTag("RenderType", "Transparent");
                lwgui.material.SetOverrideTag("Queue", "Transparent");
                if (lwgui.material.GetFloat("_OverrideZTest") <= 0)
                {
                    lwgui.material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
                lwgui.material.SetInt("_ZWrite", 0);
            }
            else
            {
                
                lwgui.material.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                lwgui.material.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                lwgui.material.SetOverrideTag("RenderType", "Opaque");
                if (lwgui.material.GetFloat("_OverrideZTest") <= 0)
                {
                    lwgui.material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                }

                lwgui.material.SetInt("_ZWrite", 1);
            }

            if (lwgui.material.GetFloat("_OverrideZTest") <= 0)
            {
                lwgui.material.SetFloat("_ZTest", 4.0f);
            }

            if (!lwgui.material.GetTexture("_RampMap"))
            {
                var defaultRampTexture = Resources.Load<Texture>("Textures/DefaultRampMap");
                if (defaultRampTexture != null)
                {
                    lwgui.material.SetTexture("_RampMap", defaultRampTexture);
                }
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

            GUI.enabled = true;
        }
		
        [InitializeOnLoadMethod]
        private static void RegisterEvent()
        {
            ASPCharacterGUI.onDrawCustomFooter += DoCustomFooter;
        }
	}

	public class ASPCharacterGUI : LWGUI
	{
		/// <summary>
		/// Called when switch to a new Material Window, each window has a LWGUI instance
		/// </summary>
		public ASPCharacterGUI() { }
        
        public static LWGUICustomGUIEvent onDrawCustomHeader;
        public static LWGUICustomGUIEvent onDrawCustomFooter;
        
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
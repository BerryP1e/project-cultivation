//Stylized Water Shader by Jonathan3D http://u3d.as/A2R

using UnityEngine;
using System;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
[RequireComponent(typeof(MeshRenderer))]
public class StylizedWater : MonoBehaviour
{

    #region Shader variables
    [Header("Color")]
    public Color waterColor;
    public Color fresnelColor;
    public Color rimColor;

    [Header("Surface")]
    public float transparency;
    public Texture reflectionCubemap;
    public float glossiness;

    public float fresnel;
    public float surfaceHighlight;
    public float surfaceHighlightTiling;
    public float surfaceHighlightSize;

    public float depth;
    public float depthDarkness;

    public float rimSize;
    public float rimFalloff;

    public bool worldSpaceTiling;
    public float tiling;
    public float rimTiling;

    public float refractionAmount;

    public float waveStrength;

    public Texture customIntersection;
    public Texture customNormal;
    public Texture normals;
    public Texture shadermap;

    public float tesselation;
    #endregion

    #region  Substance variables
    [Header("Substance")]

    [HideInInspector] public UnityEngine.Object substance; // Legacy reference only; use baked textures in Unity 2022.
    public float seed;
    public float normalStrength;

    public string[] intersectionStyleList = new string[] { "None", "Windwaker", "Foamy", "Foamy2", "Triangular", "Cells" };
    public int intersectionStyle;

    public string[] waveStyleList = new string[] { "Perlin", "Plasma", "Circular", "Stream", "Sharp" };
    public int waveStyle;
    public float waveSpeed;

    public bool useCustomIntersection;
    public bool useCustomNormals;
    #endregion

    //Script vars
    public Material material;
    public bool isMobileAdvanced = false;
    public bool isMobileBasic = false;
    public string shaderName = null;

    //Toggle bools
    [Header("Toggles")]
    public bool showColors = true;
    public bool showSurface = true;
    public bool showIntersection;
    public bool showHighlights;
    public bool showDepth;
    public bool showWaves;

    public bool hasSubstanceParams;

    public void getProperties()
    {
        //Debug.Log("StylizedWater.cs: getProperties()");

        material = GetComponent<MeshRenderer>().sharedMaterial; //Requires typeof
        if (!material || !material.shader) return;
        shaderName = material.shader.name;

        if (!substance)
        {
        #if UNITY_EDITOR
            getSubstance(material);
        #endif
        }

        getSubstanceProperties();
        getShaderProperties();
    }

    private void getSubstanceProperties()
    {
        // Built-in Substance support was removed in Unity 2018.1.
        // Read the existing baked outputs instead of invoking the removed generator.
        hasSubstanceParams = false;
        if (!material) return;
        if (material.HasProperty("_Shadermap")) shadermap = material.GetTexture("_Shadermap");
        if (material.HasProperty("_Normals")) normals = material.GetTexture("_Normals");
    }

    private void getShaderProperties()
    {
        if (!material) return;

        isMobileBasic = (shaderName.Contains("Mobile Basic")) ? true : false;
        isMobileAdvanced = (shaderName.Contains("Mobile Advanced")) ? true : false;

        //Not basic, get these parameters
        if (!shaderName.Contains("Basic"))
        {
            transparency = material.GetFloat("_Transparency");

            fresnelColor = material.GetColor("_FresnelColor");
            fresnel = material.GetFloat("_Fresnelexponent");

            surfaceHighlight = material.GetFloat("_SurfaceHighlight");
            surfaceHighlightTiling = material.GetFloat("_SurfaceHightlighttiling");
            surfaceHighlightSize = material.GetFloat("_Surfacehightlightsize");

            depth = material.GetFloat("_Depth");
            depthDarkness = material.GetFloat("_Depthdarkness");
        }

        //Excluded from both mobile versions
        if (!isMobileAdvanced && !isMobileBasic)
        {
            refractionAmount = material.GetFloat("_RefractionAmount");
            waveStrength = material.GetFloat("_Wavesstrength");
            reflectionCubemap = material.GetTexture("_Reflection");
        }

        //Shared by all
        worldSpaceTiling = (material.GetFloat("_Worldspacetiling") == 1) ? true : false;

        waterColor = material.GetColor("_WaterColor");
        rimColor = material.GetColor("_RimColor");

        glossiness = material.GetFloat("_Glossiness");

        rimSize = material.GetFloat("_RimSize");
        rimFalloff = material.GetFloat("_Rimfalloff");

        tiling = material.GetFloat("_Tiling");
        rimTiling = material.GetFloat("_Rimtiling");

        waveSpeed = material.GetFloat("_Wavesspeed");

        //Tesselation shader only
        if (shaderName == "StylizedWater/Desktop (DX11 Tesselation)")
        {
            tesselation = material.GetFloat("_Tesselation");
        }
    }

#if UNITY_EDITOR
    private void getSubstance(Material mat)
    {
        hasSubstanceParams = false;
    }
#endif
}//class end

//Easter egg, good job :)

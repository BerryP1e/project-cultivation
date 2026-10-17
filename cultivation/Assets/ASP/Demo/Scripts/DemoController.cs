using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace  ASP.Demo
{
    public class DemoController : MonoBehaviour
    {
        [SerializeField]
        private Light m_mainLight;
        [SerializeField]
        private GameObject[] m_additionalLights;
        [SerializeField]
        private Transform[] m_cameraPositions;
        [SerializeField] private GameObject m_descUI;
        public delegate void ValueChangedDelegate(int e);
        public event ValueChangedDelegate OnCameraPositionIndexChanged;
        public GameObject[] DemoCharacters;
        private int m_currentCharacterIndex = 0;
        private int m_currentSkyboxIndex = 0;
        private int m_destinationIndex = 0;
        public GameObject CurrentCharacter
        {
            get =>  DemoCharacters[m_currentCharacterIndex];
        }

        [SerializeField] private TextMeshProUGUI m_rimLightStatus;
        [SerializeField] private TextMeshProUGUI m_depthLightStatus;
        [SerializeField] private TextMeshProUGUI m_specLightStatus;
        [SerializeField] private TextMeshProUGUI m_matcapReflectionStatus;
        [SerializeField] private TextMeshProUGUI m_faceShadowStatus;
        [SerializeField] private TextMeshProUGUI m_sssMapStatus;
        [SerializeField] private TextMeshProUGUI m_fovStatus;
        [SerializeField] private TextMeshProUGUI m_ditheringStatus;
        [SerializeField] private TextMeshProUGUI m_eyeMatcapReflectStatus;
        [SerializeField] private TextMeshProUGUI m_eyeHighLightStatus;
        [SerializeField] private TextMeshProUGUI m_flattenGIStatus;
        [SerializeField] private TextMeshProUGUI m_flattenAdditionalLightStatus;
        [SerializeField] private Slider m_fovSlider;
        [SerializeField] private ReflectionProbe m_reflectionProbe;
        [SerializeField] private Material[] m_skyboxMaterials;
        [SerializeField] private TabsController m_tabController;
        [SerializeField] private Volume m_volume;
        [SerializeField] private Button m_debugOutlineButton;
        private Dictionary<Material, bool> m_rimLightPair = new Dictionary<Material, bool>();
        private Dictionary<Material, bool> m_depthRimLightPair = new Dictionary<Material, bool>();
        private Dictionary<Material, bool> m_specLightPair = new Dictionary<Material, bool>();
        private Dictionary<Material, bool> m_matcapReflectPair = new Dictionary<Material, bool>();
        private Dictionary<Material, bool> m_shadingStylePair = new Dictionary<Material, bool>();
        private Dictionary<Material, bool> m_faceShadowMapPair = new Dictionary<Material, bool>();
        private Dictionary<Material, bool> m_sssMapPair = new Dictionary<Material, bool>();
        private Dictionary<Material, bool> m_eyeMatcapMapPair = new Dictionary<Material, bool>();
        private Dictionary<Material, bool> m_eyeHighlightMapPair = new Dictionary<Material, bool>();
        private bool m_isDemoMultipleLights = false;
        private bool m_isAnimatingDithering = false;
        
        public int CameraPositionIndex {
            get
            {
                return m_destinationIndex;
            }
        }

        private Vector3 m_targetPosition;
        // Start is called before the first frame update
        private Bloom m_bloom;
        private ASPToneMap m_toneMap;
        private ASPSreenSpaceOutline m_screenSpaceOutline;
        void Start()
        {
            Bloom bloom;
            if( m_volume.profile.TryGet<Bloom>( out bloom ) )
            {
                m_bloom = bloom;
            }

            ASPToneMap toneMap;
            if( m_volume.profile.TryGet<ASPToneMap>( out toneMap ) )
            {
                m_toneMap = toneMap;
            }

            ASPSreenSpaceOutline screenSpaceOutline;
            if( m_volume.profile.TryGet<ASPSreenSpaceOutline>( out screenSpaceOutline ) )
            {
                m_screenSpaceOutline = screenSpaceOutline;
            }

            m_descUI.SetActive(false);
            m_targetPosition = m_cameraPositions[m_destinationIndex].position;
            ToNextCharacter();
            m_tabController.OnTabClick += OnTabClicked;
        }

        private Dictionary<Renderer, List<Material>> m_cachedMaterialPairs = new Dictionary<Renderer, List<Material>>();
        private void OnTabClicked()
        {
            if (m_tabController.CurrentTabIndex != 2)
            {
                foreach (var renderer in CurrentCharacter.GetComponentsInChildren<Renderer>())
                {
                    foreach (var mat in renderer.materials)
                    {
                        if (mat == null || mat.shader == null)
                            continue;
                        if (!mat.shader.name.Equals("ASP/Character"))
                        {
                            continue;
                        }
                        mat.SetFloat("_DebugGI", 0);
                    }
                }
            }
        }

        private void UpdateStatusTextByKeyword(TextMeshProUGUI guiText, string keyword, string shaderName)
        {
            var keywordState = AnyKeywordExist(CurrentCharacter, keyword, shaderName);
            guiText.text = keywordState ? "On" :  "Off";
            guiText.color = keywordState ? Color.green : Color.red;
        }
        
        private void UpdateStatusTextByFloat(TextMeshProUGUI guiText, string propertyName, string shaderName)
        {
            var floatState = AnyFloatValid(CurrentCharacter, propertyName, shaderName);
            guiText.text = floatState ? "On" :  "Off";
            guiText.color = floatState ? Color.green : Color.red;
        }

        private const string CharacterShaderName = "ASP/Character";
        private const string EyesShaderName = "ASP/Eye";
        private void UpdateMaterialStatusOnUI()
        {
            UpdateStatusTextByKeyword(m_rimLightStatus, "_RIMLIGHTING_ON", CharacterShaderName);
            UpdateStatusTextByKeyword(m_depthLightStatus, "_DEPTH_RIMLIGHTING_ON", CharacterShaderName);
            UpdateStatusTextByKeyword(m_specLightStatus, "_SPECULAR_LIGHTING_ON", CharacterShaderName);
            UpdateStatusTextByKeyword(m_matcapReflectionStatus, "_MATCAP_HIGHLIGHT_MAP", CharacterShaderName);
            UpdateStatusTextByKeyword(m_faceShadowStatus, "_FACESHADOW", CharacterShaderName);
            UpdateStatusTextByKeyword(m_sssMapStatus, "_SSSMAP", CharacterShaderName);
            UpdateStatusTextByKeyword(m_eyeMatcapReflectStatus, "_MATCAP_HIGHLIGHT_MAP", EyesShaderName);
            UpdateStatusTextByKeyword(m_eyeHighLightStatus, "_EYE_HIGHLIGHT_MAP", EyesShaderName);
            UpdateStatusTextByFloat(m_flattenGIStatus, "_BakeGISource", CharacterShaderName);
            UpdateStatusTextByFloat(m_flattenAdditionalLightStatus, "_FlattenAdditionalLighting", CharacterShaderName);
            m_fovStatus.text = GetFirstFloatValid(CurrentCharacter, "_FOVShiftX", CharacterShaderName).ToString("F1");
            m_fovSlider.SetValueWithoutNotify(GetFirstFloatValid(CurrentCharacter, "_FOVShiftX", CharacterShaderName));
            m_ditheringStatus.text = GetFirstFloatValid(CurrentCharacter, "_Dithering", CharacterShaderName).ToString("F1");
        }

        private void RevertMaterialsFromCached()
        {
            foreach (var characterRenderer in CurrentCharacter.GetComponentsInChildren<Renderer>())
            {
                characterRenderer.shadowCastingMode = ShadowCastingMode.Off;
                if (m_cachedMaterialPairs.ContainsKey(characterRenderer))
                {
                    characterRenderer.materials = m_cachedMaterialPairs[characterRenderer].ToArray();
                }
            }
            m_cachedMaterialPairs.Clear();
        }
        
        private void UpdateMaterialsToCached()
        {
            foreach (var characterRenderer in CurrentCharacter.GetComponentsInChildren<Renderer>())
            {
                if (!m_cachedMaterialPairs.ContainsKey(characterRenderer))
                {
                    m_cachedMaterialPairs.Add(characterRenderer, characterRenderer.sharedMaterials.ToList());
                }
            }
        }

        public void ToNextCharacter()
        {
            if(m_isAnimatingDithering)
                return;
            if(DemoCharacters.Length == 0)
                return;
            foreach (var obj in DemoCharacters)
            {
                obj.SetActive(false);
            }
            //RevertMaterialsFromCached();
            
            m_currentCharacterIndex++;
            if (m_currentCharacterIndex >= DemoCharacters.Length)
            {
                m_currentCharacterIndex = 0;
            }
            DemoCharacters[m_currentCharacterIndex].SetActive(true);
            UpdateMaterialStatusOnUI();
           // UpdateMaterialsToCached();
            IsPBR = false;
        }

        public void ToNextCameraPosition()
        {
            m_destinationIndex++;
            if (m_destinationIndex >= m_cameraPositions.Length)
            {
                m_destinationIndex = 0;
            }
            OnCameraPositionIndexChanged?.Invoke(m_destinationIndex);
        }

        // Update is called once per frame
        private Vector3 m_lastMousePositin;
        void Update()
        {
            
            if (Input.GetMouseButton(0) && m_destinationIndex != 4)
            {
                if (m_lastMousePositin != Vector3.zero)
                {
                    var movement = m_lastMousePositin - Input.mousePosition;
                    var rotationDir = new Vector3(0, 1, 0);
                    rotationDir *= Time.deltaTime * -movement.x * 15;
                    var rotation = CurrentCharacter.transform.rotation * rotationDir;
                    CurrentCharacter.transform.Rotate(rotation);
                }
                m_lastMousePositin = Input.mousePosition;
            }
            
            if (Input.GetMouseButtonUp(0))
            {

                m_lastMousePositin = Vector3.zero;
            }

            transform.position = Vector3.Lerp(transform.position, m_cameraPositions[m_destinationIndex].position, 1 - Mathf.Exp(-5f * Time.deltaTime));
        }

        private bool IsPBR = false;
        public void ToggleStandardPBR()
        {
            if (IsPBR)
            {
                foreach (var characterRenderer in CurrentCharacter.GetComponentsInChildren<Renderer>())
                {
                    characterRenderer.shadowCastingMode = ShadowCastingMode.Off;
                    if (m_cachedMaterialPairs.ContainsKey(characterRenderer))
                    {
                        characterRenderer.materials = m_cachedMaterialPairs[characterRenderer].ToArray();
                    }
                }
            }
            else
            {
                foreach (var characterRenderer in CurrentCharacter.GetComponentsInChildren<Renderer>())
                {
                    characterRenderer.shadowCastingMode = ShadowCastingMode.On;
                    var matList = new Material[characterRenderer.sharedMaterials.Length];
                    var index = 0;
                    foreach (var mat in characterRenderer.sharedMaterials)
                    {
                        var pbrMat = new Material(mat);
                        pbrMat.shader = Shader.Find("Universal Render Pipeline/Lit");
                        matList[index++] = pbrMat;
                    }

                    characterRenderer.materials = matList;
                }    
            }
            IsPBR = !IsPBR;
        }

        private void ToggleShaderKeyword(GameObject characterObject, Dictionary<Material, bool>map, string keyword)
        {
            foreach (var renderer in characterObject.GetComponentsInChildren<Renderer>())
            {
                foreach (var material in renderer.materials)
                {
                    if (map.ContainsKey(material) && map[material])
                    {
                        var currentValue = material.IsKeywordEnabled(keyword);
                        if (currentValue)
                        {
                            material.DisableKeyword(keyword);
                        }
                        else
                        {
                            material.EnableKeyword(keyword);
                        }
                    }
                }
            }
        }
        
        private void FetchCharacterShaderKeyword(GameObject characterObject, Dictionary<Material, bool> map, string keyword)
        {
            foreach (var renderer in characterObject.GetComponentsInChildren<Renderer>())
            {
                foreach (var material in renderer.materials)
                {
                    if (material.shader.name.Contains("ASP/Character"))
                    {
                        if (!map.ContainsKey(material))
                        {
                            map.Add(material, material.IsKeywordEnabled(keyword));
                        }
                    }
                }
            }
        }

        private void FetchEyeShaderKeyword(GameObject characterObject, Dictionary<Material, bool> map, string keyword)
        {
            foreach (var renderer in characterObject.GetComponentsInChildren<Renderer>())
            {
                foreach (var material in renderer.materials)
                {
                    if (material.shader.name.Contains("ASP/Eye"))
                    {
                        if (!map.ContainsKey(material))
                        {
                            map.Add(material, material.IsKeywordEnabled(keyword));
                        }
                    }
                }
            }
        }
        
        private bool AnyKeywordExist(GameObject characterObject, string keyword, string shaderName)
        {
            foreach (var renderer in characterObject.GetComponentsInChildren<Renderer>())
            {
                foreach (var material in renderer.materials)
                {
                    if (material.shader.name.Contains(shaderName))
                    {
                        if (material.IsKeywordEnabled(keyword))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        
        private bool AnyFloatValid(GameObject characterObject, string propertyName, string shaderName)
        {
            foreach (var renderer in characterObject.GetComponentsInChildren<Renderer>())
            {
                foreach (var material in renderer.materials)
                {
                    if (material.shader.name.Contains(shaderName))
                    {
                        if (material.HasFloat(propertyName) && material.GetFloat(propertyName) >= 1.0f)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        
        private float GetFirstFloatValid(GameObject characterObject, string propertyName, string shaderName)
        {
            foreach (var renderer in characterObject.GetComponentsInChildren<Renderer>())
            {
                foreach (var material in renderer.materials)
                {
                    if (material.shader.name.Contains(shaderName))
                    {
                        if (material.HasFloat(propertyName))
                        {
                            return material.GetFloat(propertyName);
                        }
                    }
                }
            }

            return 0;
        }
        
        public void ToggleRimLight()
        {
            FetchCharacterShaderKeyword(CurrentCharacter, m_rimLightPair, "_RIMLIGHTING_ON");
            ToggleShaderKeyword(CurrentCharacter, m_rimLightPair, "_RIMLIGHTING_ON");
            UpdateMaterialStatusOnUI();
        }
        
        public void ToggleDepthRimLight()
        {
            FetchCharacterShaderKeyword(CurrentCharacter, m_depthRimLightPair, "_DEPTH_RIMLIGHTING_ON");
            ToggleShaderKeyword(CurrentCharacter, m_depthRimLightPair, "_DEPTH_RIMLIGHTING_ON");
            UpdateMaterialStatusOnUI();
        }
        
        public void ToggleSpecularHighlights()
        {
            FetchCharacterShaderKeyword(CurrentCharacter, m_specLightPair, "_SPECULAR_LIGHTING_ON");
            ToggleShaderKeyword(CurrentCharacter, m_specLightPair, "_SPECULAR_LIGHTING_ON");
            UpdateMaterialStatusOnUI();
        }
        
        public void ToggleMatCapHighlights()
        {
            FetchCharacterShaderKeyword(CurrentCharacter, m_matcapReflectPair, "_MATCAP_HIGHLIGHT_MAP");
            ToggleShaderKeyword(CurrentCharacter, m_matcapReflectPair, "_MATCAP_HIGHLIGHT_MAP");
            UpdateMaterialStatusOnUI();
        }
        
        public void ToggleEyeMatCapHighlights()
        {
            FetchEyeShaderKeyword(CurrentCharacter, m_eyeMatcapMapPair, "_MATCAP_HIGHLIGHT_MAP");
            ToggleShaderKeyword(CurrentCharacter, m_eyeMatcapMapPair, "_MATCAP_HIGHLIGHT_MAP");
            UpdateMaterialStatusOnUI();
        }
        
        public void ToggleFaceShadowMap()
        {
            FetchCharacterShaderKeyword(CurrentCharacter, m_faceShadowMapPair, "_FACESHADOW");
            ToggleShaderKeyword(CurrentCharacter, m_faceShadowMapPair, "_FACESHADOW");

            foreach (var renderer in CurrentCharacter.GetComponentsInChildren<Renderer>())
            {
                foreach (var material in renderer.materials)
                {
                    if (material.shader.name.Contains("ASP/Character") && m_faceShadowMapPair[material])
                    {
                        if (material.IsKeywordEnabled("_FACESHADOW"))
                        {
                            material.SetFloat("_style", 2);
                            material.EnableKeyword("_STYLE_FACE");
                            material.DisableKeyword("_STYLE_CELSHADING");
                        }
                        else
                        {
                            material.SetFloat("_style", 1);
                            material.DisableKeyword("_STYLE_FACE");
                            material.EnableKeyword("_STYLE_CELSHADING");
                        }
                    }
                }
            }
            UpdateMaterialStatusOnUI();
        }
        
        public void ToggleSubsurfaceScattering()
        {
            FetchCharacterShaderKeyword(CurrentCharacter, m_sssMapPair, "_SSSMAP");
            ToggleShaderKeyword(CurrentCharacter, m_sssMapPair, "_SSSMAP");
            UpdateMaterialStatusOnUI();
        }
        
        public void ToggleEyeHighlights()
        {
            FetchEyeShaderKeyword(CurrentCharacter, m_eyeHighlightMapPair, "_EYE_HIGHLIGHT_MAP");
            ToggleShaderKeyword(CurrentCharacter, m_eyeHighlightMapPair, "_EYE_HIGHLIGHT_MAP");
            UpdateMaterialStatusOnUI();
        }
        
        private void ToggleCharacterFloat(string propertyName)
        {
            foreach (var renderer in CurrentCharacter.GetComponentsInChildren<Renderer>())
            {
                foreach (var material in renderer.materials)
                {
                    if (material.shader.name.Contains("ASP/Character"))
                    {
                        if (material.HasFloat(propertyName) && material.GetFloat(propertyName) < 1.0f)
                        {
                            material.SetFloat(propertyName, 1.0f);
                        }
                        else
                        {
                            material.SetFloat(propertyName, 0);
                        }
                    }
                }
            }
        }

        public void ToggleSampleSHMode()
        {
            ToggleCharacterFloat("_BakeGISource");
            UpdateMaterialStatusOnUI();
        }
        
        public void ToggleFlattenAdditionalLightsMode()
        {
            ToggleCharacterFloat("_FlattenAdditionalLighting");
            UpdateMaterialStatusOnUI();
        }
        
        public void ToggleDemoMultipleLights()
        {
            m_isDemoMultipleLights = !m_isDemoMultipleLights;
            if (m_isDemoMultipleLights)
            {
                m_mainLight.intensity = 0.5f;
            }
            else
            {
                m_mainLight.intensity = 1.0f;
            }

            foreach (var lightObj in m_additionalLights)
            {
                lightObj.SetActive(m_isDemoMultipleLights);
            }
            UpdateMaterialStatusOnUI();
        }

        private IEnumerator DemoDithering()
        {
            m_isAnimatingDithering = true;
            var elapsedTime = 0f;
            CurrentCharacter.GetComponent<ASPCharacterPanel>()
                .SetDitheringSizeValueToAllMaterials(4f);
            while (elapsedTime <= 0.75f)
            {
                CurrentCharacter.GetComponent<ASPCharacterPanel>().SetDitheringValueToAllMaterials(elapsedTime / 0.75f);
                m_ditheringStatus.text = GetFirstFloatValid(CurrentCharacter, "_Dithering", CharacterShaderName).ToString("F1");
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            CurrentCharacter.GetComponent<ASPCharacterPanel>().SetDitheringValueToAllMaterials(1.0f);
            elapsedTime = 0.0f;
            while (elapsedTime <= 0.75f)
            {
                CurrentCharacter.GetComponent<ASPCharacterPanel>().SetDitheringValueToAllMaterials(1.0f - (elapsedTime / 0.75f));
                m_ditheringStatus.text = GetFirstFloatValid(CurrentCharacter, "_Dithering", CharacterShaderName).ToString("F1");
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            CurrentCharacter.GetComponent<ASPCharacterPanel>().SetDitheringValueToAllMaterials(0f);
            m_ditheringStatus.text = GetFirstFloatValid(CurrentCharacter, "_Dithering", CharacterShaderName).ToString("F1");
            m_isAnimatingDithering = false;
        }

        public void ToggleDemoDithering()
        {
             if(m_isAnimatingDithering) 
                 return;
             StartCoroutine(DemoDithering());
        }

        public void UpdateFOV(Slider slider)
        {
            CurrentCharacter.GetComponent<ASPCharacterPanel>().SetFOVAdjustValueToAllMaterials(slider.value);
            m_fovStatus.text = GetFirstFloatValid(CurrentCharacter, "_FOVShiftX", CharacterShaderName).ToString("F1");
        }

        public void ToNextSkybox()
        {
            m_currentSkyboxIndex++;
            if (m_currentSkyboxIndex >= m_skyboxMaterials.Length)
            {
                m_currentSkyboxIndex = 0;
            }
            RenderSettings.skybox = m_skyboxMaterials[m_currentSkyboxIndex];
            UpdateAmbientProbe();
            m_reflectionProbe.RenderProbe();
        }

        private void UpdateAmbientProbe()
        {
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            StartCoroutine(UpdateEnvironment());
        }

        IEnumerator UpdateEnvironment() {
            DynamicGI.UpdateEnvironment();
            m_reflectionProbe.RenderProbe();
            yield return new WaitForEndOfFrame();
#if UNITY_2022_1_OR_NEWER
            RenderSettings.customReflectionTexture = m_reflectionProbe.texture;
#else
            RenderSettings.customReflection = m_reflectionProbe.texture;
            #endif
        }

        public void ToggleDebugBakeGIColor()
        {
            foreach (var renderer in CurrentCharacter.GetComponentsInChildren<Renderer>())
            {
                foreach (var mat in renderer.materials)
                {
                    if (mat == null || mat.shader == null)
                        continue;
                    if (!mat.shader.name.Equals("ASP/Character"))
                    {
                        continue;
                    }
                    var newValue = mat.GetFloat("_DebugGI") < 1.0f ? 1.0f : 0.0f;
                    mat.SetFloat("_DebugGI", newValue);
                }
            }
        }

        public void ToggleBloom()
        {
            m_bloom.active = !m_bloom.active;
        }
        
        public void ToggleScreenSpaceOutline()
        {
            m_screenSpaceOutline.EnableOutline.value = !m_screenSpaceOutline.EnableOutline.value;
            m_debugOutlineButton.interactable = m_screenSpaceOutline.EnableOutline.value;
        }
        
        public void ToggleToneMapping()
        {
            m_toneMap.active = !m_toneMap.active;
        }
        
        public void ToggleDebugScreenSpaceOutline()
        {
            m_screenSpaceOutline.EnableDebugMode.value = !m_screenSpaceOutline.EnableDebugMode.value;
            m_debugOutlineButton.image.color = m_screenSpaceOutline.EnableDebugMode.value ? Color.green : Color.white;
        }

        private int m_cachedDepthOffsetShadowRenderingLayerMask;
        public void ToggleDepthOffsetShadow()
        {
            var renderer = (GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset).GetRenderer(0);
            var property =
                typeof(ScriptableRenderer).GetProperty("rendererFeatures",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            var features = property.GetValue(renderer) as List<ScriptableRendererFeature>;
            var isExist = features.Any(e => e.GetType() == typeof(ASPDepthOffsetShadowFeature));
            if (isExist)
            {
                var feature = features.Find(e => e.GetType() == typeof(ASPDepthOffsetShadowFeature));
                if ((feature as ASPDepthOffsetShadowFeature).GetLayer() != 0)
                {
                    m_cachedDepthOffsetShadowRenderingLayerMask = (feature as ASPDepthOffsetShadowFeature).GetLayer();
                    (feature as ASPDepthOffsetShadowFeature).SetLayer(0);
                    feature.Dispose();
                    feature.Create();
                }
                else
                {
                    (feature as ASPDepthOffsetShadowFeature).SetLayer(m_cachedDepthOffsetShadowRenderingLayerMask);
                    feature.Dispose();
                    feature.Create();
                }
            }
        }
        
        public void ToggleMeshBasedOutline()
        {
            var renderer = (GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset).GetRenderer(0);
            var property =
                typeof(ScriptableRenderer).GetProperty("rendererFeatures",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            var features = property.GetValue(renderer) as List<ScriptableRendererFeature>;
            var isExist = features.Any(e => e.GetType() == typeof(ASPMeshOutlineRendererFeature));
            if (isExist)
            {
                var feature = features.Find(e => e.GetType() == typeof(ASPMeshOutlineRendererFeature));
                feature.SetActive(!feature.isActive);
                feature.Dispose();
                feature.Create();
            }
        }


    }
    
}

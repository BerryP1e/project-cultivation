using System;
using UnityEngine;
using UnityEngine.UI;

namespace ASP
{
    [ExecuteInEditMode]
    public class RTPreviewer : MonoBehaviour
    {
        public string RTName;
        public RawImage Image;
        
        private void Update()
        {
            if(RTName != String.Empty && Image != null)
            {
                Image.texture = Shader.GetGlobalTexture(RTName);
            }
        }
    }
 }
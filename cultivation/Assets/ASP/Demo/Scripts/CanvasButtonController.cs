using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace ASP.Demo
{
    public class CanvasButtonController : MonoBehaviour
    {
        public DemoController demoController;
        public GameObject[] Panels;
        public Button[] DeactiveButtons;

        public Button[] CameraMidWayButtons;
        public Button[] CameraCloseUpButtons;
        public Button[] CameraCloseDownButtons;
        public Button[] CameraSideButtons;

        // Start is called before the first frame update
        void Start()
        {
            if (demoController == null)
                return;
        }

        private void HandleCameraPositionIndexChanged(int index)
        {
            HandleCameraButtons(index);
        }

        private void HandleCameraButtons(int index)
        {
            if (demoController == null)
                return;
            foreach (var button in CameraMidWayButtons)
            {
                if(button != null)
                    button.interactable = false;
            }
            
            foreach (var button in CameraCloseDownButtons)
            {
                if(button != null)
                    button.interactable = false;
            }
            
            foreach (var button in CameraCloseUpButtons)
            {
                if(button != null)
                    button.interactable = false;
            }
            
            foreach (var button in CameraSideButtons)
            {
                if (button != null)
                {
                    button.interactable = false;
                    if  (button != null && button.GetComponentInChildren<Slider>() != null)
                    {
                        button.GetComponentInChildren<Slider>().interactable = index == 4;
                    }   
                }
            }
            
            foreach (var button in CameraMidWayButtons)
            {
                button.interactable |= index == 1;
            }
            
            foreach (var button in CameraCloseDownButtons)
            {
                button.interactable |= index == 2;
            }
            
            foreach (var button in CameraCloseUpButtons)
            {
                button.interactable |= index == 3;
            }
            
            foreach (var button in CameraSideButtons)
            {
                button.interactable |= index == 4;
                if  (button != null && button.GetComponentInChildren<Slider>() != null)
                {
                    button.GetComponentInChildren<Slider>().interactable = index == 4;
                }
            }
        }

        void OnEnable()
        {
            if (demoController == null)
                return;
            demoController.OnCameraPositionIndexChanged += HandleCameraPositionIndexChanged;
            foreach (var panel in Panels)
            {
                foreach (var button in panel.GetComponentsInChildren<Button>())
                {
                    button.interactable = true;
                }
            }

            foreach (var button in DeactiveButtons)
            {
                button.interactable = false;
            }

            HandleCameraButtons(demoController.CameraPositionIndex);
        }

        void OnDisable()
        {
            if (demoController == null)
                return;
            demoController.OnCameraPositionIndexChanged -= HandleCameraPositionIndexChanged;
            foreach (var panel in Panels)
            {
                if(panel == null)
                    continue;
                foreach (var button in panel.GetComponentsInChildren<Button>())
                {
                    if(button != null)
                        button.interactable = true;
                }
            }

            foreach (var button in DeactiveButtons)
            {
                if(button != null)
                    button.interactable = false;
            }

            HandleCameraButtons(demoController.CameraPositionIndex);
        }
    }
}
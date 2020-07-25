using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.UI;

public class Menu : MonoBehaviour
{
    public Canvas MenuCanvas;
    public Text NodeIdText;
    public Toggle MicrophoneToggle;
    
    // To control rendering of line beam from controllers
    // https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@0.9/api/UnityEngine.XR.Interaction.Toolkit.XRInteractorLineVisual.html
    public UnityEngine.XR.Interaction.Toolkit.XRInteractorLineVisual LeftLine, RightLine;

    const KeyCode DesktopKey = KeyCode.Space;

    bool isOpen = false;

    InputDevice? controller;

    bool lastButtonValue = false;   // for detecting button down/up of the XR controller

    public void Update()
    {
        // For VR
        if (!controller.HasValue)
        {
            // FIXME: currently, cannot find controller during Start().
            // Search right controller
            //  See https://docs.unity3d.com/ja/2019.4/Manual/xr_input.html
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller,
                devices
            );
            if (devices.Count != 0)
                controller = devices[0];
        }
        else
        {
            // Check button down
            if (controller.Value.TryGetFeatureValue(CommonUsages.primaryButton, out bool buttonValue))
            {
                if (!lastButtonValue && buttonValue) Toggle();
                lastButtonValue = buttonValue;
            }
        }

        // For desktop (non-VR)
        if (Input.GetKeyDown(DesktopKey))
            Toggle();
        
        // Show microphone state
        var micCap = GetComponent<MicrophoneCapture>();
        MicrophoneToggle.isOn = micCap.MicrophoneEnabled;
    }

    public void OnMicrophoneToggleChanged()
    {
        // Set microphone state
        var micCap = GetComponent<MicrophoneCapture>();
        micCap.MicrophoneEnabled = MicrophoneToggle.isOn;
    }

    void OnSyncReady()
    {
        var objectSync = GetComponent<ObjectSync>();

        // Display NodeId
        NodeIdText.text = $"Node ID: {objectSync.Node.NodeId}";
    }

    // Toggle menu opened/closed
    void Toggle()
    {
        isOpen = !isOpen;
        MenuCanvas.gameObject.SetActive(isOpen);

        // Display controller line only if menu is open
        LeftLine.enabled = isOpen;
        RightLine.enabled = isOpen;
    }
}
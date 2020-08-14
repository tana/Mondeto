using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

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

    public void OnResetPosRotButtonClicked()
    {
        // Reset avatar position and rotation
        // CharacterController overwrites transform.position.
        // Therefore we have to disable before resetting position.
        //  https://forum.unity.com/threads/does-transform-position-work-on-a-charactercontroller.36149/
        GetComponent<CharacterController>().enabled = false;
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        GetComponent<CharacterController>().enabled = true;
    }

    public async void OnRecenterButtonClicked()
    {
        /*
        // Recenter using Unity's new XR API
        //  See https://docs.unity3d.com/2019.4/Documentation/Manual/xr_input.html (the "XRInputSubsystem and InputDevice association" section)
        var subsystems = new List<XRInputSubsystem>();
        SubsystemManager.GetInstances<XRInputSubsystem>(subsystems);
        foreach (var subsystem in subsystems)
        {
            subsystem.TryRecenter();
        }
        */
        InputTracking.Recenter();   // Legacy XR needs legacy recenter method

        // Wait until recentered
        await UniTask.WaitForEndOfFrame();
        await UniTask.WaitForEndOfFrame();

        MoveMenu();
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

        MoveMenu();

        // Display controller line only if menu is open
        LeftLine.enabled = isOpen;
        RightLine.enabled = isOpen;
    }

    void MoveMenu()
    {
        // Make the menu appear in front of the camera
        MenuCanvas.transform.position = Camera.main.transform.TransformPoint(new Vector3(0, 0, 1));
        MenuCanvas.transform.rotation = Camera.main.transform.rotation;
    }
}
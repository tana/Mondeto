using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

public class Menu : MonoBehaviour
{
    public Canvas MenuCanvas;
    public GameObject MenuPanel;
    public GameObject DialogPanel;
    public Text NodeIdText;
    public Toggle MicrophoneToggle;

    public InputField ObjectCreationInput;
    
    // To control rendering of line beam from controllers
    // https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@0.9/api/UnityEngine.XR.Interaction.Toolkit.XRInteractorLineVisual.html
    public UnityEngine.XR.Interaction.Toolkit.XRInteractorLineVisual LeftLine, RightLine;

    const KeyCode DesktopKey = KeyCode.Space;

    bool isOpen = false;
    bool canToggle = true;

    InputDevice? controller;

    bool lastButtonValue = false;   // for detecting button down/up of the XR controller

    Camera lastCamera;

    public void Update()
    {
        // Set camera of canvas when camera is changed (for FirstPerson-ThirdPerson change)
        Camera camera = Camera.main;
        if (lastCamera != camera)
        {
            lastCamera = camera;
            MenuCanvas.worldCamera = camera;
            MoveMenu();
        }

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
                if ((!lastButtonValue && buttonValue) && canToggle) Toggle();
                lastButtonValue = buttonValue;
            }
        }

        // For desktop (non-VR)
        // If object specification YAML is being typed in, space key does not close the menu.
        if (Input.GetKeyDown(DesktopKey) && canToggle && !ObjectCreationInput.isFocused)
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
        // Recenter using Unity's new XR API
        //  See https://docs.unity3d.com/2019.4/Documentation/Manual/xr_input.html (the "XRInputSubsystem and InputDevice association" section)
        var subsystems = new List<XRInputSubsystem>();
        SubsystemManager.GetInstances<XRInputSubsystem>(subsystems);
        foreach (var subsystem in subsystems)
        {
            subsystem.TryRecenter();
        }

        // Wait until recentered
        await UniTask.WaitForEndOfFrame();
        await UniTask.WaitForEndOfFrame();

        MoveMenu();
    }

    public async void OnCreateObjectButtonClicked()
    {
        var sceneLoader = new SceneLoader(GetComponent<ObjectSync>().Node);
        await sceneLoader.LoadObject(ObjectCreationInput.text);
    }

    public void OnShowCreditsButtonClicked()
    {
        // FIXME: Probably it does not work in Quest standalone
        Application.OpenURL("file:///" + System.IO.Path.GetFullPath("./credits/Credits.html"));
    }

    public async Task ShowDialog(string title, string message)
    {
        canToggle = false;
        MenuPanel.SetActive(false);
        DialogPanel.SetActive(true);
        Toggle();

        var titleText = DialogPanel.transform.Find("Title")?.GetComponent<Text>();
        if (titleText != null) titleText.text = title;
        var messageText = DialogPanel.transform.Find("Message")?.GetComponent<Text>();
        if (messageText != null) messageText.text = message;

        var okButton = DialogPanel.transform.Find("OKButton")?.GetComponent<Button>();
        await okButton.OnClickAsync();  // Wait until the OK button is clicked

        Toggle();
        MenuPanel.SetActive(true);
        DialogPanel.SetActive(false);
        canToggle = true;
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
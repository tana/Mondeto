using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class StartupController : MonoBehaviour
{
    public Toggle ServerToggle;
    public InputField ServerUrlInput;
    public Toggle ClientToggle;
    public InputField ClientUrlInput;

    const string DefaultServerUrl = "wss://devel.luftmensch.info:15902/server";
    const string DefaultClientUrl = "wss://devel.luftmensch.info:15902/client";

    public void Start()
    {
        // If batch mode, log is only written into stdout (Console.WriteLine).
        // If graphics is available, log is also written into Unity debug console.
        if (!Application.isBatchMode)
        {
            Logger.OnLog += (type, component, msg) => {
                Debug.Log($"[{Logger.LogTypeToString(type)}] {component}: {msg}");
            };
        }

        if (Application.isBatchMode)
        {
            Logger.Log("StartupController", "Running as batch mode. Starting dedicated server scene");
            Settings.Instance.SignalingServerUrl = DefaultServerUrl;
            SceneManager.LoadScene("WalkServer");
        }

        ServerUrlInput.text = DefaultServerUrl;
        ClientUrlInput.text = DefaultClientUrl;

        OnToggleChanged();
    }

    public void OnToggleChanged()
    {
        ServerUrlInput.interactable = ServerToggle.isOn;
        ClientUrlInput.interactable = ClientToggle.isOn;
    }

    public void OnStartClicked()
    {
        if (ServerToggle.isOn)
        {
            Settings.Instance.SignalingServerUrl = ServerUrlInput.text;
            SceneManager.LoadScene("WalkServerHybrid");
        }
        else if (ClientToggle.isOn)
        {
            Settings.Instance.SignalingServerUrl = ClientUrlInput.text;
            SceneManager.LoadScene("WalkClient");
        }
    }
}

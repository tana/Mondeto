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
    public InputField AvatarInput;

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
            ChangeSettings();
            Logger.Log("StartupController", "Running as batch mode. Starting dedicated server scene");
            SceneManager.LoadScene("WalkServer");
        }

        LoadSettings();
        OnToggleChanged();
    }

    void LoadSettings()
    {
        ServerUrlInput.text = Settings.Instance.SignalerUrlForServer;
        ClientUrlInput.text = Settings.Instance.SignalerUrlForClient;
        AvatarInput.text = Settings.Instance.AvatarPath;
    }

    void ChangeSettings()
    {
        Settings.Instance.SignalerUrlForServer = ServerUrlInput.text;
        Settings.Instance.SignalerUrlForClient = ClientUrlInput.text;
        Settings.Instance.AvatarPath = AvatarInput.text;
    }

    public void OnToggleChanged()
    {
        ServerUrlInput.interactable = ServerToggle.isOn;
        ClientUrlInput.interactable = ClientToggle.isOn;
    }

    public void OnStartClicked()
    {
        ChangeSettings();

        if (ServerToggle.isOn)
        {
            SceneManager.LoadScene("WalkServerHybrid");
        }
        else if (ClientToggle.isOn)
        {
            SceneManager.LoadScene("WalkClient");
        }
    }
}

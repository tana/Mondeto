using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class StartupController : MonoBehaviour
{
    public Toggle ServerToggle;
    public Toggle ClientToggle;

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
            SceneManager.LoadScene("WalkServer");
        }
    }

    public void OnStartClicked()
    {
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

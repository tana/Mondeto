using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnityLogOutput : MonoBehaviour
{
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
    }
}

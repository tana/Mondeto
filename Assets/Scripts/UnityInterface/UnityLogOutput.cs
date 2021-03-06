﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class UnityLogOutput : MonoBehaviour
{
    public void Start()
    {
        // Log output is done in main thread
        //  https://qiita.com/toRisouP/items/a2c1bb1b0c4f73366bc6
        SynchronizationContext context = SynchronizationContext.Current;
        
        // If batch mode, log is only written into stdout (Console.WriteLine).
        // If graphics is available, log is written into Unity debug console.
        if (!Application.isBatchMode)
        {
            Logger.OnLog += async (type, component, msg) => {
                context.Post(_ => Debug.Log($"[{Logger.LogTypeToString(type)}] {component}: {msg}"), null);
            };
        }
        else
        {
            Logger.OnLog += async (type, component, msg) => {
                context.Post(_ => Console.WriteLine($"[{Logger.LogTypeToString(type)}] {component}: {msg}"), null);
            };
        }
    }
}

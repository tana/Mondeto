using System;
using System.Collections.Generic;
using UnityEngine;

public class Logger
{
    public enum LogType
    {
        Debug, Log, Error
    }

    static Dictionary<LogType, string> TypeString = new Dictionary<LogType, String> {
        { LogType.Debug, "DBG" },
        { LogType.Log, "LOG" },
        { LogType.Error, "ERR" }
    };

    public static void Write(LogType type, string component, string msg)
    {
        string line = $"[{TypeString[type]}] {component}: {msg}";
        UnityEngine.Debug.Log(line);
        Console.WriteLine(line);
    }
    
    public static void Debug(string component, string msg) => Write(LogType.Debug, component, msg);
    public static void Log(string component, string msg) => Write(LogType.Log, component, msg);
    public static void Error(string component, string msg) => Write(LogType.Error, component, msg);
}
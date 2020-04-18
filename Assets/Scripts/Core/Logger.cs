using System;
using System.Collections.Generic;

public class Logger
{
    public enum LogType
    {
        Debug, Log, Error
    }

    public delegate void LogHandler(LogType type, string component, string msg);
    public static event LogHandler OnLog;

    static Dictionary<LogType, string> TypeString = new Dictionary<LogType, String> {
        { LogType.Debug, "DBG" },
        { LogType.Log, "LOG" },
        { LogType.Error, "ERR" }
    };

    public static string LogTypeToString(LogType type) => TypeString[type];

    public static void Write(LogType type, string component, string msg)
    {
        string line = $"[{LogTypeToString(type)}] {component}: {msg}";
        Console.WriteLine(line);

        OnLog?.Invoke(type, component, msg);
    }
    
    public static void Debug(string component, string msg) => Write(LogType.Debug, component, msg);
    public static void Log(string component, string msg) => Write(LogType.Log, component, msg);
    public static void Error(string component, string msg) => Write(LogType.Error, component, msg);
}
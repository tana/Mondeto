using System;
using UnityEngine;

public class Logger
{
    public static void Write(string msg)
    {
        Debug.Log(msg);
        Console.WriteLine(msg);
    }
}
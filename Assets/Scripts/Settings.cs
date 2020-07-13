using System;

public class Settings
{
    public static readonly Settings Instance = new Settings();

    public string SignalerUrlForServer = "wss://devel.luftmensch.info:15902/server";
    public string SignalerUrlForClient = "wss://devel.luftmensch.info:15902/client";

    private Settings()
    {
    }

    // TODO: command line args or setting file loading
}
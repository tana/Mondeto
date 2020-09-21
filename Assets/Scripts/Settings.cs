using System;
using System.IO;

public class Settings
{
    public static readonly Settings Instance = new Settings();

    public string SignalerUrlForServer = "wss://devel.luftmensch.info:15902/server";
    public string SignalerUrlForClient = "wss://devel.luftmensch.info:15902/client";

    public string AvatarPath = "avatar.vrm";

    public string TempDirectory = "." + Path.DirectorySeparatorChar + "temp_data";

    public int ObjectLogSize = 1000;

    private Settings()
    {
    }

    // TODO: command line args or setting file loading
}
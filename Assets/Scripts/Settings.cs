using System;

public class Settings
{
    public static readonly Settings Instance = new Settings();

    public string SignalingServerUrl;

    private Settings()
    {
    }

    // TODO: command line args or setting file loading
}
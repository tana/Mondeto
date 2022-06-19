using System;
using System.IO;
using UnityEngine;

public class SettingsManager
{
    string settingsPath;
    bool isSettingsValid;

    // Singleton
    static SettingsManager instance = new SettingsManager();
    public static SettingsManager Instance
    {
        get => instance;
        private set => instance = value;
    }

    private SettingsManager()
    {
    }

    public void InitializeSettings()
    {
        settingsPath = Application.persistentDataPath + Path.DirectorySeparatorChar + "settings.yml";

        if (File.Exists(settingsPath))
        {
            isSettingsValid = LoadSettingsFromFile();
        }
        else
        {
            // If the settings file is not exist (first launch), set default settings

            Settings.Instance.AvatarPath = Application.streamingAssetsPath + "/default_avatar.vrm";
            Settings.Instance.MimeTypesPath = Application.streamingAssetsPath + "/config/mime.types";
            Settings.Instance.SceneFile = Application.streamingAssetsPath + "/scene.yml";

            Settings.Instance.TempDirectory = Application.temporaryCachePath;

            isSettingsValid = true;
        }
    }

    public void DumpSettings()
    {
        if (isSettingsValid)
        {
            DumpSettingsToFile();
        }
        else
        {
            // If the settings file is broken, avoid overwriting to prevent unintended loss of settings
            Logger.Error("SettingsManager", "Settings file is broken. New settings were NOT saved.");
        }
    }

    // Returns true if succeeded
    bool LoadSettingsFromFile()
    {
        using (var reader = new StreamReader(settingsPath))
        {
            try
            {
                Settings.Load(reader);
                return true;
            }
            catch (Exception e) // Cannot load settings
            {
                Logger.Error("SettingsManager", "Cannot load settings. " + e.ToString());
                return false;
            }
        }
    }

    void DumpSettingsToFile()
    {
        using (var writer = new StreamWriter(settingsPath))
        {
            Settings.Instance.Dump(writer);
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using VRM;
using Cysharp.Threading.Tasks;

public class StartupController : MonoBehaviour
{
    public Toggle ServerToggle;
    public InputField ServerUrlInput;
    public Toggle ClientToggle;
    public InputField ClientUrlInput;
    public InputField AvatarInput;

    public Text AvatarInfo;

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

        LoadSettings();
        OnToggleChanged();
        ShowAvatarInfo();
    }

    void LoadSettings()
    {
        ServerUrlInput.text = Settings.Instance.SignalerUrlForServer;
        ClientUrlInput.text = Settings.Instance.SignalerUrlForClient;
        AvatarInput.text = Settings.Instance.AvatarPath;
    }

    void ChangeSettings()
    {
        Settings.Instance.SignalerUrlForServer = ServerUrlInput.text;
        Settings.Instance.SignalerUrlForClient = ClientUrlInput.text;
        Settings.Instance.AvatarPath = AvatarInput.text;
    }

    async void ShowAvatarInfo()
    {
        VRMMetaObject meta;
        try
        {
            // Load and analyze VRM
            var ctx = new VRMImporterContext();
            ctx.ParseGlb(File.ReadAllBytes(AvatarInput.text));
            meta = ctx.ReadMeta();
            ctx.Dispose();

            // Create information text
            using (var writer = new StringWriter())
            {
                writer.NewLine = "\n";
                writer.WriteLine($"<b>{meta.Title}</b> by <b>{meta.Author} ({meta.ContactInformation})</b>");

                // Use Unity's text styling to emphasize some information
                // (coloring is done in ColorizeVrmAllowedUser and ColorizeVrmUsageLicense methods)
                //  https://docs.unity3d.com/ja/2019.4/Manual/StyledText.html
                writer.WriteLine($"Allowed user: <b>{ColorizeVrmAllowedUser(meta.AllowedUser)}</b>");
                writer.WriteLine($"Violent usage: <b>{ColorizeVrmUsageLicense(meta.ViolentUssage)}</b>");
                writer.WriteLine($"Sexual usage: <b>{ColorizeVrmUsageLicense(meta.SexualUssage)}</b>");
                writer.WriteLine($"Commercial usage: <b>{ColorizeVrmUsageLicense(meta.CommercialUssage)}</b>");
                writer.WriteLine($"Other permission: <b>{meta.OtherPermissionUrl}</b>");
                writer.WriteLine($"License: <b>{meta.LicenseType}</b>");
                writer.WriteLine($"Other license: <b>{meta.OtherLicenseUrl}</b>");

                AvatarInfo.text = writer.ToString();
            }
        }
        catch (Exception e)
        {
            AvatarInfo.text = "<color=red>Avatar load error</color>\n" + e.ToString();
        }

        // Rebuild Vertical Layout Group using new size of AvatarInfo (changed according to its content by Content Size Filter)
        //  https://docs.unity3d.com/ja/2019.4/Manual/UIAutoLayout.html
        //  https://docs.unity3d.com/ja/2019.4/Manual/HOWTO-UIFitContentSize.html
        await UniTask.WaitForEndOfFrame();  // Somehow, frame wait is needed before MarkLayoutForRebuild
        LayoutRebuilder.MarkLayoutForRebuild(AvatarInfo.transform.parent.GetComponent<RectTransform>());
    }

    string ColorizeVrmAllowedUser(AllowedUser allowedUser)
    {
        if (allowedUser == AllowedUser.Everyone) return "Everyone";
        else if (allowedUser == AllowedUser.ExplicitlyLicensedPerson) return "<color=orange>Explicitly licensed person</color>";
        else return "<color=red>Only author</color>";
    }

    string ColorizeVrmUsageLicense(UssageLicense license)
    {
        if (license == UssageLicense.Allow) return "Allow";
        else return "<color=red>Disallow</color>";
    }

    public void OnToggleChanged()
    {
        ServerUrlInput.interactable = ServerToggle.isOn;
        ClientUrlInput.interactable = ClientToggle.isOn;
    }

    public void OnStartClicked()
    {
        ChangeSettings();

        if (ServerToggle.isOn)
        {
            SceneManager.LoadScene("WalkServerHybrid");
        }
        else if (ClientToggle.isOn)
        {
            SceneManager.LoadScene("WalkClient");
        }
    }

    public void OnAvatarSetClicked()
    {
        ShowAvatarInfo();
    }
}

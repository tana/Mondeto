using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using VRM;

public class DesktopAvatar : MonoBehaviour
{
    public string VrmPath = "avatar.vrm";

    private VRMImporterContext ctx;

    void Start()
    {
    }

    void Update()
    {
        SyncObject obj = GetComponent<ObjectSync>().SyncObject;
        if (obj == null) return;
        bool isOriginal = GetComponent<ObjectSync>().IsOriginal;

        var textDisplay = GetComponentInChildren<TextMesh>();
        textDisplay.text = $"NodeId={obj.OriginalNodeId}";
        // Text is always facing the screen
        textDisplay.transform.rotation = Quaternion.LookRotation(
            -(Camera.main.transform.position - textDisplay.transform.position),
            Camera.main.transform.up
        );
    }

    // Called by ObjectSync when become ready
    async void OnSyncReady()
    {
        Debug.Log("OnSyncReady");

        SyncObject obj = GetComponent<ObjectSync>().SyncObject;
        SyncNode node = GetComponent<ObjectSync>().Node;

        Blob vrmBlob;
        if (GetComponent<ObjectSync>().IsOriginal)
        {
            BlobHandle vrmHandle = node.GenerateBlobHandle();
            byte[] vrmData = File.ReadAllBytes(VrmPath);
            // Use MIME type for GLTF binary https://www.iana.org/assignments/media-types/model/gltf-binary
            vrmBlob = new Blob { MimeType = "model/gltf-binary", Data = vrmData };
            node.WriteBlob(vrmHandle, vrmBlob);
            obj.Fields["vrm"] = vrmHandle;
        }
        else
        {
            BlobHandle texHandle = (BlobHandle)obj.Fields["vrm"];
            vrmBlob = await node.ReadBlob(texHandle);
        }

        // Load VRM from byte array
        // https://github.com/vrm-c/UniVRM/wiki/Runtime-import
        // https://qiita.com/sh_akira/items/8155e4b69107c2a7ede6
        ctx = new VRMImporterContext();
        ctx.ParseGlb(vrmBlob.Data);

        var meta = ctx.ReadMeta();
        Logger.Write($"Loading VRM {meta.Title} created by {meta.Author} ({meta.ContactInformation})");
        Logger.Write($"AllowedUser={meta.AllowedUser}, ViolentUsage={meta.ViolentUssage}");
        Logger.Write($"SexualUsage={meta.SexualUssage}, CommercialUsage={meta.CommercialUssage}");
        Logger.Write($"OtherPermissionUrl={meta.OtherLicenseUrl}");
        Logger.Write($"LicenseType={meta.LicenseType}");
        Logger.Write($"OtherLicenseUrl={meta.OtherLicenseUrl}");

        await ctx.LoadAsyncTask();
        ctx.Root.transform.SetParent(transform, false);
        // TODO animation
        ctx.EnableUpdateWhenOffscreen();
        ctx.ShowMeshes();
        
        Logger.Write($"VRM loaded");
    }

    void OnDestroy()
    {
        ctx.Dispose();
    }
}

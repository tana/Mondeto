using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using VRM;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(WalkAnimation))]
public class DesktopAvatar : MonoBehaviour
{
    public string VrmPath = "avatar.vrm";

    // Locomotion-related settings
    public float MaxSpeed = 1.0f;
    public float MaxAngularSpeed = 60.0f;

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
        if (Camera.main != null)
        {
            textDisplay.transform.rotation = Quaternion.LookRotation(
                -(Camera.main.transform.position - textDisplay.transform.position),
                Camera.main.transform.up
            );
        }

        if (isOriginal && Input.GetKeyDown(KeyCode.M))
        {
            var micCap = GetComponent<MicrophoneCapture>();
            if (micCap != null)
            {
                // TODO: better way for microphone control
                micCap.enabled = !micCap.enabled;
                Logger.Debug("DesktopAvatar", "microphone " + (micCap.enabled ? "on" : "off"));
            }
        }

        // State values for walking animation
        float forward, turn;

        if (isOriginal)
        {
            // Walking control
            var characterController = GetComponent<CharacterController>();

            forward = MaxSpeed * Input.GetAxis("Vertical");
            turn = MaxAngularSpeed * Input.GetAxis("Horizontal");

            var velocity = new Vector3(0, 0, forward);
            var angularVelocity = new Vector3(0, turn, 0);
            transform.rotation *= Quaternion.Euler(angularVelocity * Time.deltaTime);
            characterController.SimpleMove(transform.rotation * velocity);

            // This two values are synchronized
            obj.SetField("forward", new Primitive<float> { Value = forward });
            obj.SetField("turn", new Primitive<float> { Value = turn });
        }
        else
        {
            forward = (obj.GetField("forward") as Primitive<float>)?.Value ?? 0.0f;
            turn = (obj.GetField("forward") as Primitive<float>)?.Value ?? 0.0f;
        }

        // Walking animation
        GetComponent<WalkAnimation>().SetAnimationParameters(forward, turn);
    }

    /*
    void FixedUpdate()
    {
        bool isOriginal = GetComponent<ObjectSync>().IsOriginal;
        SyncObject obj = GetComponent<ObjectSync>().SyncObject;

        if (isOriginal)
        {
            obj.SetField()
        }
    }
    */

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
            obj.SetField("vrm", vrmHandle);
        }
        else
        {
            BlobHandle texHandle = (BlobHandle)obj.GetField("vrm");
            vrmBlob = await node.ReadBlob(texHandle);
        }

        // Load VRM from byte array
        // https://github.com/vrm-c/UniVRM/wiki/Runtime-import
        // https://qiita.com/sh_akira/items/8155e4b69107c2a7ede6
        ctx = new VRMImporterContext();
        ctx.Root = this.gameObject;
        ctx.ParseGlb(vrmBlob.Data);

        var meta = ctx.ReadMeta();
        Logger.Log("DesktopAvatar", $"Loading VRM {meta.Title} created by {meta.Author} ({meta.ContactInformation})");
        Logger.Log("DesktopAvatar", $"AllowedUser={meta.AllowedUser}, ViolentUsage={meta.ViolentUssage}");
        Logger.Log("DesktopAvatar", $"SexualUsage={meta.SexualUssage}, CommercialUsage={meta.CommercialUssage}");
        Logger.Log("DesktopAvatar", $"OtherPermissionUrl={meta.OtherLicenseUrl}");
        Logger.Log("DesktopAvatar", $"LicenseType={meta.LicenseType}");
        Logger.Log("DesktopAvatar", $"OtherLicenseUrl={meta.OtherLicenseUrl}");

        await ctx.LoadAsyncTask();

        ctx.EnableUpdateWhenOffscreen();
        ctx.ShowMeshes();
        
        Logger.Log("DesktopAvatar", $"VRM loaded");
    }

    void OnDestroy()
    {
        ctx.Dispose();
    }
}

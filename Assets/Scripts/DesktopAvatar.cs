using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using VRM;
using Cysharp.Threading.Tasks;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(WalkAnimation))]
public class DesktopAvatar : MonoBehaviour
{
    public string VrmPath = "avatar.vrm";

    // Locomotion-related settings
    public float MaxSpeed = 1.0f;
    public float MaxAngularSpeed = 60.0f;

    private VRMImporterContext ctx;

    // State values for walking animation
    private float forward = 0.0f, turn = 0.0f;

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
        }

        // Walking animation
        GetComponent<WalkAnimation>().SetAnimationParameters(forward, turn);
    }

    void OnBeforeSync(SyncObject obj)
    {
        obj.SetField("forward", new Primitive<float> { Value = forward });
        obj.SetField("turn", new Primitive<float> { Value = turn });
    }

    void OnAfterSync(SyncObject obj)
    {
        if (GetComponent<ObjectSync>().IsOriginal) return;

        if (obj.HasField("forward") && obj.HasField("turn"))
        {
            forward = (obj.GetField("forward") as Primitive<float>)?.Value ?? 0.0f;
            turn = (obj.GetField("turn") as Primitive<float>)?.Value ?? 0.0f;
        }
    }

    // Called by ObjectSync when become ready
    async void OnSyncReady()
    {
        Debug.Log("OnSyncReady");

        SyncObject obj = GetComponent<ObjectSync>().SyncObject;
        SyncNode node = GetComponent<ObjectSync>().Node;

        obj.BeforeSync += OnBeforeSync;
        obj.AfterSync += OnAfterSync;

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
            while (!obj.HasField("vrm") || !(obj.GetField("vrm") is BlobHandle))
            {
                Logger.Debug("DesktopAvatar", "Field vrm not ready");
                await UniTask.WaitForFixedUpdate();
            }
            BlobHandle blobHandle = (BlobHandle)obj.GetField("vrm");
            vrmBlob = await node.ReadBlob(blobHandle);
        }

        // Disable collision detection during VRM load
        // When the avatar collides with something during creation,
        // it goes to wrong position (e.g. floating).
        GetComponent<Collider>().enabled = false;

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

        // Enable collision again
        GetComponent<Collider>().enabled = true;
        
        Logger.Log("DesktopAvatar", $"VRM loaded");
    }

    void OnDestroy()
    {
        ctx.Dispose();
    }
}

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
    // Locomotion-related settings
    public float SpeedCoeff = 1.0f;
    public float AngularSpeedCoeff = 60.0f;

    public Camera FirstPersonCamera;
    public Camera ThirdPersonCamera;

    private bool firstPerson = false;

    private VRMImporterContext ctx;

    // State values for walking animation
    private float forward = 0.0f, sideways = 0.0f, turn = 0.0f;

    private Vector3 velocity = Vector3.zero, angularVelocity = Vector3.zero;

    // For calculation of velocity and angular velocity
    private Vector3 lastPosition = Vector3.zero;
    private Quaternion lastRotation = Quaternion.identity;

    private Vector3 lastMousePosition;
    private Quaternion camRotBeforeDrag;

    void Start()
    {
        if (FirstPersonCamera != null)
            FirstPersonCamera.enabled = firstPerson;
        if (ThirdPersonCamera != null)
            ThirdPersonCamera.enabled = !firstPerson;
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

        // Viewpoint change
        if (isOriginal && Input.GetKeyDown(KeyCode.F))
        {
            firstPerson = !firstPerson;
            FirstPersonCamera.enabled = firstPerson;
            ThirdPersonCamera.enabled = !firstPerson;
            Logger.Debug("DesktopAvatar", (firstPerson ? "first" : "third") + "person camera");
        }

        if (isOriginal)
        {
            // Orientation and camera control
            turn = 0.0f;
            Camera cam = firstPerson ? FirstPersonCamera : ThirdPersonCamera;
            // Use mouse movement during drag (similar to Mozilla Hubs?)
            // because Unity's cursor lock feature seemed somewhat strange especially in Editor.
            if (Input.GetKeyDown(KeyCode.Mouse0))
            {
                lastMousePosition = Input.mousePosition;
                camRotBeforeDrag = cam.transform.localRotation;
            }
            if (Input.GetKey(KeyCode.Mouse0))
            {
                Vector3 mouseDiff = Input.mousePosition - lastMousePosition;
                lastMousePosition = Input.mousePosition;
                turn = -AngularSpeedCoeff * mouseDiff.x;
                float elevation = AngularSpeedCoeff * mouseDiff.y;
                if (cam != null)
                    cam.transform.localRotation *= Quaternion.Euler(elevation * Time.deltaTime, 0.0f, 0.0f);
            }
            if (Input.GetKeyUp(KeyCode.Mouse0))
            {
                // Reset camera elevation when mouse button is released
                if (cam != null)
                    cam.transform.localRotation = camRotBeforeDrag;
            }

            // Walking control
            var characterController = GetComponent<CharacterController>();

            forward = SpeedCoeff * Input.GetAxis("Vertical");
            sideways = SpeedCoeff * Input.GetAxis("Horizontal");

            var velocity = new Vector3(sideways, 0, forward);
            var angularVelocity = new Vector3(0, turn, 0);
            transform.rotation *= Quaternion.Euler(angularVelocity * Time.deltaTime);
            characterController.SimpleMove(transform.rotation * velocity);
        }

        // Walking animation
        GetComponent<WalkAnimation>().SetAnimationParameters(forward, sideways, turn);
    }

    public void FixedUpdate()
    {
        SyncObject obj = GetComponent<ObjectSync>()?.SyncObject;
        if (obj == null) return;

        if (GetComponent<ObjectSync>().IsOriginal)
        {
            velocity = (transform.position - lastPosition) / Time.fixedDeltaTime;
            angularVelocity = Mathf.Deg2Rad * (Quaternion.Inverse(lastRotation) * transform.rotation).eulerAngles / Time.fixedDeltaTime;
            lastPosition = transform.position;
            lastRotation = transform.rotation;
        }
        else
        {
            transform.position += velocity * Time.fixedDeltaTime;
            transform.rotation *= Quaternion.Euler(Mathf.Rad2Deg * angularVelocity * Time.fixedDeltaTime);
        }
    }

    void OnBeforeSync(SyncObject obj)
    {
        obj.SetField("forward", new Primitive<float> { Value = forward });
        obj.SetField("sideways", new Primitive<float> { Value = sideways });
        obj.SetField("turn", new Primitive<float> { Value = turn });

        obj.SetField("velocity", UnityUtil.ToVec(velocity));
        obj.SetField("angularVelocity", UnityUtil.ToVec(angularVelocity));
    }

    void OnAfterSync(SyncObject obj)
    {
        if (GetComponent<ObjectSync>().IsOriginal) return;

        if (obj.HasField("forward") && obj.HasField("sideways") && obj.HasField("turn"))
        {
            forward = (obj.GetField("forward") as Primitive<float>)?.Value ?? 0.0f;
            sideways = (obj.GetField("sideways") as Primitive<float>)?.Value ?? 0.0f;
            turn = (obj.GetField("turn") as Primitive<float>)?.Value ?? 0.0f;
        }

        if (obj.HasField("velocity") && obj.GetField("velocity") is Vec velocityVec)
        {
            velocity = UnityUtil.FromVec(velocityVec);
        }
        if (obj.HasField("angularVelocity") && obj.GetField("angularVelocity") is Vec angularVelocityVec)
        {
            angularVelocity = UnityUtil.FromVec(angularVelocityVec);
        }
    }

    // Called by ObjectSync when become ready
    async void OnSyncReady()
    {
        Debug.Log("OnSyncReady");

        SyncObject obj = GetComponent<ObjectSync>().SyncObject;
        SyncNode node = GetComponent<ObjectSync>().Node;

        lastPosition = transform.position;
        lastRotation = transform.rotation;

        obj.BeforeSync += OnBeforeSync;
        obj.AfterSync += OnAfterSync;

        Blob vrmBlob;
        if (GetComponent<ObjectSync>().IsOriginal)
        {
            BlobHandle vrmHandle = node.GenerateBlobHandle();
            byte[] vrmData = File.ReadAllBytes(Settings.Instance.AvatarPath);
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

        // Disable collision detection (and character control) during VRM load
        // When the avatar collides with something during creation,
        // it goes to wrong position (e.g. floating).
        // Note: CharacterController inherits Collider.
        //  (See https://docs.unity3d.com/2019.3/Documentation/ScriptReference/CharacterController.html )
        GetComponent<CharacterController>().enabled = false;

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
        Logger.Log("DesktopAvatar", $"OtherPermissionUrl={meta.OtherPermissionUrl}");
        Logger.Log("DesktopAvatar", $"LicenseType={meta.LicenseType}");
        Logger.Log("DesktopAvatar", $"OtherLicenseUrl={meta.OtherLicenseUrl}");

        await ctx.LoadAsyncTask();

        ctx.EnableUpdateWhenOffscreen();
        ctx.ShowMeshes();

        // Enable collision (and character controller) again (see the disabling line above)
        GetComponent<CharacterController>().enabled = true;
        
        Logger.Log("DesktopAvatar", $"VRM loaded");

        if (GetComponent<ObjectSync>().IsOriginal)
        {
            // Set up first person view (do not display avatar of the player)
            //  https://vrm.dev/en/univrm/components/univrm_firstperson/
            //  https://vrm.dev/en/dev/univrm-0.xx/programming/univrm_use_firstperson/
            var fp = GetComponent<VRMFirstPerson>();
            fp.Setup();
            if (FirstPersonCamera != null)
            {
                FirstPersonCamera.transform.position = fp.FirstPersonBone.position + fp.FirstPersonBone.rotation * fp.FirstPersonOffset;
                FirstPersonCamera.transform.rotation = transform.rotation;  // face forward
                // Do not render layer "VRMThirdPersonOnly" on first person camera
                FirstPersonCamera.cullingMask &= ~LayerMask.GetMask("VRMThirdPersonOnly");
            }
            if (ThirdPersonCamera != null)
            {
                // Do not render layer "VRMFirstPersonOnly" on third person camera
                ThirdPersonCamera.cullingMask &= ~LayerMask.GetMask("VRMFirstPersonOnly");
            }
        }
    }

    // Control avatar through IK
    // https://docs.unity3d.com/2019.3/Documentation/Manual/InverseKinematics.html
    // TODO: enable IK, sync looking direction
    /*
    void OnAnimatorIK()
    {
        var anim = GetComponent<Animator>();
        if (FirstPersonCamera != null)
        {
            anim.SetLookAtWeight(1.0f);
            anim.SetLookAtPosition(FirstPersonCamera.transform.TransformPoint(Vector3.forward));
        }
    }
    */

    void OnDestroy()
    {
        ctx.Dispose();
    }
}

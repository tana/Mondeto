using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR;
using VRM;
using Cysharp.Threading.Tasks;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(WalkAnimation))]
public class PlayerAvatar : MonoBehaviour
{
    // Locomotion-related settings
    public float SpeedCoeff = 1.0f;
    public float AngularSpeedCoeff = 60.0f;

    public GameObject XRRig;
    public Transform LeftHand;
    public Transform RightHand;

    public Camera ThirdPersonCamera;

    private bool firstPerson = true;

    private VRMImporterContext ctx;

    // State values for walking animation
    private float forward = 0.0f, sideways = 0.0f, turn = 0.0f;

    private Vector3 lookAt = Vector3.forward; // looking position (in local coord)

    private Vector3 leftHandPosition, rightHandPosition;
    private Quaternion leftHandRotation, rightHandRotation;

    private Vector3 velocity = Vector3.zero, angularVelocity = Vector3.zero;

    // For calculation of velocity and angular velocity
    private Vector3 lastPosition = Vector3.zero;
    private Quaternion lastRotation = Quaternion.identity;

    private Vector3 lastMousePosition;
    private Quaternion camRotBeforeDrag;

    private Camera xrCamera;

    void Start()
    {
        xrCamera = XRRig.GetComponentInChildren<Camera>();
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
                Logger.Debug("PlayerAvatar", "microphone " + (micCap.enabled ? "on" : "off"));
            }
        }

        // Viewpoint change
        if (isOriginal && Input.GetKeyDown(KeyCode.F))
        {
            firstPerson = !firstPerson;
            xrCamera.enabled = firstPerson;
            ThirdPersonCamera.enabled = !firstPerson;
            Logger.Debug("PlayerAvatar", (firstPerson ? "first" : "third") + "person camera");
        }

        if (isOriginal)
        {
            // Orientation and camera control
            turn = 0.0f;

            if (!firstPerson)
            {
                // Third person mode (non-VR)
                Camera cam = ThirdPersonCamera;
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
            }
            else
            {
                // VR (HMD) turn control
                // Search right hand controller
                // https://docs.unity3d.com/ja/2019.4/Manual/xr_input.html
                var devices = new List<InputDevice>();
                InputDevices.GetDevicesWithCharacteristics(
                    InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right,
                    devices
                );
                Vector2 stick;
                if (devices.Count != 0 && devices[0].TryGetFeatureValue(CommonUsages.primary2DAxis, out stick))
                {
                    turn = AngularSpeedCoeff * stick.x;
                }
            }

            // Walking control
            var characterController = GetComponent<CharacterController>();

            forward = SpeedCoeff * Input.GetAxis("Vertical");
            sideways = SpeedCoeff * Input.GetAxis("Horizontal");

            var velocity = new Vector3(sideways, 0, forward);
            var angularVelocity = new Vector3(0, turn, 0);
            transform.rotation *= Quaternion.Euler(angularVelocity * Time.deltaTime);
            characterController.SimpleMove(transform.rotation * velocity);

            // Head rotation
            if (xrCamera != null)
            {
                lookAt = transform.worldToLocalMatrix * xrCamera.transform.TransformPoint(Vector3.forward);
            }
            else
            {
                lookAt = Vector3.forward;
            }

            // Left hand
            // If device is not present, GetDeviceAtXRNode returns an "invalid" InputDevice.
            //   https://docs.unity3d.com/ja/2019.4/ScriptReference/XR.InputDevices.GetDeviceAtXRNode.html
            if (InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).isValid)
            {
                // If left hand device is present
                leftHandPosition = transform.worldToLocalMatrix * LeftHand.position;
                leftHandRotation = Quaternion.Inverse(transform.rotation) * Quaternion.AngleAxis(90, LeftHand.transform.forward) * LeftHand.rotation;
            }
            // Right hand
            if (InputDevices.GetDeviceAtXRNode(XRNode.RightHand).isValid)
            {
                // If right hand device is present
                rightHandPosition = transform.worldToLocalMatrix * RightHand.position;
                rightHandRotation = Quaternion.Inverse(transform.rotation) * Quaternion.AngleAxis(-90, RightHand.transform.forward) * RightHand.rotation;
            }
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

        obj.SetField("lookAt", UnityUtil.ToVec(lookAt));

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

        if (obj.TryGetField("lookAt", out Vec lookAtVec))
        {
            lookAt = UnityUtil.FromVec(lookAtVec);
        }

        if (obj.TryGetField("velocity", out Vec velocityVec))
        {
            velocity = UnityUtil.FromVec(velocityVec);
        }
        if (obj.TryGetField("angularVelocity", out Vec angularVelocityVec))
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
                Logger.Debug("PlayerAvatar", "Field vrm not ready");
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
        Logger.Log("PlayerAvatar", $"Loading VRM {meta.Title} created by {meta.Author} ({meta.ContactInformation})");
        Logger.Log("PlayerAvatar", $"AllowedUser={meta.AllowedUser}, ViolentUsage={meta.ViolentUssage}");
        Logger.Log("PlayerAvatar", $"SexualUsage={meta.SexualUssage}, CommercialUsage={meta.CommercialUssage}");
        Logger.Log("PlayerAvatar", $"OtherPermissionUrl={meta.OtherPermissionUrl}");
        Logger.Log("PlayerAvatar", $"LicenseType={meta.LicenseType}");
        Logger.Log("PlayerAvatar", $"OtherLicenseUrl={meta.OtherLicenseUrl}");

        await ctx.LoadAsyncTask();

        ctx.EnableUpdateWhenOffscreen();
        ctx.ShowMeshes();

        // Enable collision (and character controller) again (see the disabling line above)
        GetComponent<CharacterController>().enabled = true;
        
        Logger.Log("PlayerAvatar", $"VRM loaded");

        if (GetComponent<ObjectSync>().IsOriginal)
        {
            // Set up first person view (do not display avatar of the player)
            //  https://vrm.dev/en/univrm/components/univrm_firstperson/
            //  https://vrm.dev/en/dev/univrm-0.xx/programming/univrm_use_firstperson/
            var fp = GetComponent<VRMFirstPerson>();
            fp.Setup();
            if (XRRig != null)
            {
                XRRig.transform.position = fp.FirstPersonBone.position + fp.FirstPersonBone.rotation * fp.FirstPersonOffset;
                XRRig.transform.rotation = transform.rotation;  // face forward
                // Do not render layer "VRMThirdPersonOnly" on first person camera
                xrCamera.cullingMask &= ~LayerMask.GetMask("VRMThirdPersonOnly");
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
    void OnAnimatorIK()
    {
        var anim = GetComponent<Animator>();
        anim.SetLookAtWeight(1.0f);
        anim.SetLookAtPosition(transform.localToWorldMatrix * lookAt);

        anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1.0f);
        anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1.0f);
        anim.SetIKPosition(AvatarIKGoal.LeftHand, transform.localToWorldMatrix * leftHandPosition);
        anim.SetIKRotation(AvatarIKGoal.LeftHand, transform.rotation * leftHandRotation);

        anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 1.0f);
        anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 1.0f);
        anim.SetIKPosition(AvatarIKGoal.RightHand, transform.localToWorldMatrix * rightHandPosition);
        anim.SetIKRotation(AvatarIKGoal.RightHand, transform.rotation * rightHandRotation);
    }

    void OnDestroy()
    {
        ctx.Dispose();
    }
}

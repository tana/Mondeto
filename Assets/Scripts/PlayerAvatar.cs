using System.Collections;
using System.Linq;
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

    // Child objects for left and right hands (only non-null when tracking device is available)
    private SyncObject leftHandObj, rightHandObj;
    private GameObject leftHandGameObj, rightHandGameObj;

    private Vector3 lastMousePosition;
    private Quaternion camRotBeforeDrag;

    private Camera xrCamera;

    private InputDevice? rightController;

    private bool lastGripButtonValue;   // for button down/up detection
    
    private List<GameObject> headForShadow = new List<GameObject>();

    void Start()
    {
        if (XRRig != null)
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
                micCap.MicrophoneEnabled = !micCap.MicrophoneEnabled;
            }
        }

        // Viewpoint change
        if (isOriginal && Input.GetKeyDown(KeyCode.F))
        {
            firstPerson = !firstPerson;
            if (xrCamera != null)
                xrCamera.enabled = firstPerson;
            if (ThirdPersonCamera != null)
                ThirdPersonCamera.enabled = !firstPerson;
            SetHeadShadow();
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
                if (devices.Count >= 1) rightController = devices[0];
                Vector2 stick;
                if (rightController.HasValue &&
                    rightController.Value.TryGetFeatureValue(CommonUsages.primary2DAxis, out stick))
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
            if (leftHandGameObj != null)
            {
                leftHandGameObj.transform.position = LeftHand.position;
                leftHandGameObj.transform.rotation = Quaternion.AngleAxis(90, LeftHand.transform.forward) * LeftHand.rotation;
            }
            // Right hand
            if (rightHandGameObj != null)
            {
                rightHandGameObj.transform.position = RightHand.position;
                rightHandGameObj.transform.rotation = Quaternion.AngleAxis(-90, RightHand.transform.forward) * RightHand.rotation;
            }
        }

        // Walking animation
        GetComponent<WalkAnimation>().SetAnimationParameters(forward, sideways, turn);
    }

    public void FixedUpdate()
    {
        SyncBehaviour syncBehaviour = GetComponent<ObjectSync>().NetManager.GetComponent<SyncBehaviour>();
        if (leftHandObj != null)
        {
            leftHandGameObj = syncBehaviour.GameObjects[leftHandObj.Id];
            SetHandCollider(leftHandGameObj);
        }
        if (rightHandObj != null)
        {
            rightHandGameObj = syncBehaviour.GameObjects[rightHandObj.Id];
            SetHandCollider(rightHandGameObj);
        }

        if (GetComponent<ObjectSync>().IsOriginal)
        {
            // grabbing
            if (rightController.HasValue &&
                rightController.Value.TryGetFeatureValue(CommonUsages.gripButton, out bool gripButtonValue))
            {
                if (!lastGripButtonValue && gripButtonValue) GrabObject();
                else if (lastGripButtonValue && !gripButtonValue) UngrabObject();

                lastGripButtonValue = gripButtonValue;
            }
        }
    }

    void SetHandCollider(GameObject handGameObj)
    {
        if (handGameObj.GetComponent<GrabDetector>() != null) return;
        handGameObj.AddComponent<GrabDetector>();
    }

    void GrabObject()
    {
        if (rightHandGameObj == null) return;
        var detector = rightHandGameObj.GetComponent<GrabDetector>();
        foreach (var obj in detector.ObjectToGrab)
        {
            obj.GetComponent<ObjectSync>().SyncObject.SendEvent(
                "grab",
                rightHandObj.Id,
                new IValue[0]
            );
        }
    }

    void UngrabObject()
    {
        // FIXME: provisional implementation
        if (rightHandObj.TryGetField("children", out Sequence children))
        {
            foreach (var child in children.Elements.Select(elem => elem as ObjectRef).Where(elem => elem != null))
            {
                rightHandObj.Node.Objects[child.Id].SendEvent(
                    "ungrab",
                    rightHandObj.Id,
                    new IValue[0]
                );
            }
        }
    }

    void OnBeforeSync(SyncObject obj)
    {
        obj.SetField("forward", new Primitive<float> { Value = forward });
        obj.SetField("sideways", new Primitive<float> { Value = sideways });
        obj.SetField("turn", new Primitive<float> { Value = turn });

        obj.SetField("lookAt", UnityUtil.ToVec(lookAt));
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
    }

    // Called by ObjectSync when become ready
    async void OnSyncReady()
    {
        Debug.Log("OnSyncReady");

        SyncObject obj = GetComponent<ObjectSync>().SyncObject;
        SyncNode node = GetComponent<ObjectSync>().Node;

        obj.BeforeSync += OnBeforeSync;
        obj.AfterSync += OnAfterSync;
        obj.RegisterFieldUpdateHandler("leftHand", OnLeftHandUpdated);
        obj.RegisterFieldUpdateHandler("rightHand", OnRightHandUpdated);

        OnLeftHandUpdated();
        OnRightHandUpdated();

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
        ctx.Root = new GameObject();    // VRM is loaded as a separate object
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

        // Move VRM avatar inside this gameObject
        ctx.Root.transform.SetParent(transform);
        ctx.Root.transform.localPosition = Vector3.zero;
        ctx.Root.transform.localRotation = Quaternion.identity;

        GetComponent<Animator>().avatar = ctx.Root.GetComponent<Animator>().avatar;
        
        Logger.Log("PlayerAvatar", $"VRM loaded");

        if (GetComponent<ObjectSync>().IsOriginal)
        {
            // Set up first person view (do not display avatar of the player)
            //  https://vrm.dev/en/univrm/components/univrm_firstperson/
            //  https://vrm.dev/en/dev/univrm-0.xx/programming/univrm_use_firstperson/
            var fp = GetComponentInChildren<VRMFirstPerson>();
            fp.Setup();
            if (XRRig != null)
            {
                XRRig.transform.position = fp.FirstPersonBone.position + fp.FirstPersonBone.rotation * fp.FirstPersonOffset;
                XRRig.transform.rotation = transform.rotation;  // face forward
                // Do not render layer "VRMThirdPersonOnly" on first person camera
                xrCamera.cullingMask &= ~LayerMask.GetMask("VRMThirdPersonOnly");
                SetHeadShadow();
            }
            if (ThirdPersonCamera != null)
            {
                // Do not render layer "VRMFirstPersonOnly" on third person camera
                ThirdPersonCamera.cullingMask &= ~LayerMask.GetMask("VRMFirstPersonOnly");
            }

            // Left hand
            // If device is not present, GetDeviceAtXRNode returns an "invalid" InputDevice.
            //   https://docs.unity3d.com/ja/2019.4/ScriptReference/XR.InputDevices.GetDeviceAtXRNode.html
            if (InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).isValid)
            {
                // If left hand device is present
                var leftId = await node.CreateObject();
                leftHandObj = node.Objects[leftId];
                leftHandObj.SetField("parent", obj.GetObjectRef());
                leftHandObj.SetField("tag", new Sequence(new IValue[] {
                    new Primitive<string>("constantVelocity"),
                    new Primitive<string>("collider")
                }));
                obj.SetField("leftHand", leftHandObj.GetObjectRef());
            }
            // Right hand
            if (InputDevices.GetDeviceAtXRNode(XRNode.RightHand).isValid)
            {
                // If right hand device is present
                var rightId = await node.CreateObject();
                rightHandObj = node.Objects[rightId];
                rightHandObj.SetField("parent", obj.GetObjectRef());
                rightHandObj.SetField("tag", new Sequence(new IValue[] {
                    new Primitive<string>("constantVelocity"),
                    new Primitive<string>("collider")
                }));
                obj.SetField("rightHand", rightHandObj.GetObjectRef());
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

        if (leftHandGameObj != null)
        {
            anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1.0f);
            anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1.0f);
            anim.SetIKPosition(AvatarIKGoal.LeftHand, leftHandGameObj.transform.position);
            anim.SetIKRotation(AvatarIKGoal.LeftHand, leftHandGameObj.transform.rotation);
        }

        if (rightHandGameObj != null)
        {
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 1.0f);
            anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 1.0f);
            anim.SetIKPosition(AvatarIKGoal.RightHand, rightHandGameObj.transform.position);
            anim.SetIKRotation(AvatarIKGoal.RightHand, rightHandGameObj.transform.rotation);
        }
    }
    
    void OnLeftHandUpdated()
    {
        SyncObject obj = GetComponent<ObjectSync>().SyncObject;

        if (obj.TryGetField("leftHand", out ObjectRef leftHandRef))
        {
            leftHandObj = obj.Node.Objects[leftHandRef.Id];
        }
    }

    void OnRightHandUpdated()
    {
        SyncObject obj = GetComponent<ObjectSync>().SyncObject;

        if (obj.TryGetField("rightHand", out ObjectRef rightHandRef))
        {
            rightHandObj = obj.Node.Objects[rightHandRef.Id];
        }
    }

    void SetHeadShadow()
    {
        if (!firstPerson)
        {
            foreach (var obj in headForShadow)
            {
                Destroy(obj);
            }
            headForShadow.Clear();
            return;
        }

        // Seemingly, if some objects are excluded from rendering using layer, those objects cannot cast shadow.
        // To show shadow of avatar's head (hidden from first-person camera), clone hidden objects.
        // This solution is based on @Sesleria's article https://qiita.com/Sesleria/items/875566585e8cb1888256
        // Note: This operation is somewhat costly because requires iterating on renderers.
        int thirdPersonOnlyLayer = LayerMask.NameToLayer("VRMThirdPersonOnly");
        int firstPersonOnlyLayer = LayerMask.NameToLayer("VRMFirstPersonOnly");
        headForShadow.Clear();
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            if (renderer.gameObject.layer == thirdPersonOnlyLayer)
            {
                GameObject obj = Instantiate(renderer.gameObject);
                obj.transform.SetParent(transform, worldPositionStays: true);
                obj.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                obj.layer = firstPersonOnlyLayer;
                headForShadow.Add(obj);
            }
        }
    }

    void OnDestroy()
    {
        ctx.Dispose();
    }
}

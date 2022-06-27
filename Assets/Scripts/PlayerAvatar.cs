using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.XR;
using VRM;
using Cysharp.Threading.Tasks;
using Mondeto.Core;

namespace Mondeto
{

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(WalkAnimation))]
public class PlayerAvatar : MonoBehaviour, ITag
{
    // Locomotion-related settings
    public float SpeedCoeff = 1.0f;
    public float AngularSpeedCoeff = 60.0f;

    public GameObject XROrigin;
    public Transform LeftHand;
    public Transform RightHand;

    public Camera ThirdPersonCamera;

    private bool firstPerson = true;

    // State values for walking animation
    private float forward = 0.0f, sideways = 0.0f, turn = 0.0f;

    private Vector3 lookAt = Vector3.forward; // looking position (in local coord)

    // Child objects for left and right hands (only non-null when tracking device is available)
    private SyncObject leftHandObj, rightHandObj;
    private GameObject leftHandGameObj, rightHandGameObj;
    // and for desktop-mode grabbing
    private SyncObject mouseGrabberObj;
    private GameObject mouseGrabberGameObj;

    private Vector3 lastMousePosition;
    private Quaternion camRotBeforeDrag;

    private Camera xrCamera;

    private InputDevice? leftController, rightController;

    private ButtonDetector leftGripDetector, rightGripDetector;
    private ButtonDetector leftTriggerDetector, rightTriggerDetector;
    
    private List<GameObject> headForShadow = new List<GameObject>();

    private List<ObjectSync> clickedObjects = new List<ObjectSync>();
    private List<ObjectSync> mouseGrabbedObjects = new List<ObjectSync>();
    private List<ObjectSync> leftGrabbedObjects = new List<ObjectSync>();
    private List<ObjectSync> rightGrabbedObjects = new List<ObjectSync>();

    // TODO:
    public void Setup(SyncObject syncObject)
    {
        // Don't play sound captured by microphone locally
        syncObject.SetField("noLocalAudioOutput", new Primitive<int>(1));
    }
    // TODO:
    public void Cleanup(SyncObject syncObject)
    {
    }

    void Start()
    {
        if (XROrigin != null)
            xrCamera = XROrigin.GetComponentInChildren<Camera>();
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
            obj.WriteDebugLog("PlayerAvatar", (firstPerson ? "first" : "third") + "person camera");
        }

        if (isOriginal)
        {
            // VR controller related
            if (!leftController.HasValue)
            {
                SetupLeftController();
            }
            else
            {
                leftTriggerDetector.Detect();
                leftGripDetector.Detect();
            }

            if (!rightController.HasValue)
            {
                SetupRightController();
            }
            else
            {
                rightTriggerDetector.Detect();
                rightGripDetector.Detect();
            }

            // Orientation and camera control
            turn = 0.0f;

            Vector2 stick;
            if (rightController.HasValue &&
                rightController.Value.TryGetFeatureValue(CommonUsages.primary2DAxis, out stick))
            {
                // VR (HMD) turn control
                turn = AngularSpeedCoeff * stick.x;
            }
            else
            {
                // non-VR turn/head control
                Camera cam = Camera.main;
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
            else if (firstPerson)
            {
                lookAt = transform.worldToLocalMatrix * Camera.main.transform.TransformPoint(Vector3.forward);
            }
            else
            {
                lookAt = transform.forward;
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

            // Mouse click
            // Use primary button (button number 0)
            //  See https://docs.unity3d.com/ja/current/ScriptReference/Input.GetMouseButtonDown.html
            if (Input.GetMouseButtonDown(0))
            {
                var clickedObj = FindMouseClickedObject();
                if (clickedObj != null)
                {
                    StartClicking(obj, clickedObj);
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                EndClicking(obj);
            }

            // Grab using mouse secondary button
            // secondary = 1 (See https://docs.unity3d.com/ja/current/ScriptReference/Input.GetMouseButtonDown.html )
            if (Input.GetMouseButtonDown(1))
            {
                var clickedObj = FindMouseClickedObject();
                if (clickedObj != null)
                {
                    // mouseGrabber moves to the place of clicked object
                    mouseGrabberGameObj.transform.position = clickedObj.transform.position;
                    // Because this is inside Update(), position fields are not immediately updated unless forcibly updated.
                    mouseGrabberGameObj.GetComponent<ObjectSync>().UpdateFields();
                    StartClicking(mouseGrabberObj, clickedObj, "grab", mouseGrabbedObjects);
                }
            }
            else if (Input.GetMouseButtonUp(1))
            {
                EndClicking(mouseGrabberObj, "ungrab", mouseGrabbedObjects);
            }
            // Change position of mouseGrabber using mouse
            if (mouseGrabbedObjects.Count != 0)
            {
                MoveObjectByMouse(mouseGrabberGameObj);
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
            SetHandCollider(leftHandGameObj, leftHandObj);
        }
        if (rightHandObj != null)
        {
            rightHandGameObj = syncBehaviour.GameObjects[rightHandObj.Id];
            SetHandCollider(rightHandGameObj, rightHandObj);
        }
        if (mouseGrabberObj != null)
        {
            mouseGrabberGameObj = syncBehaviour.GameObjects[mouseGrabberObj.Id];
        }
    }

    void SetupLeftController()
    {
        // Search left hand controller
        // https://docs.unity3d.com/ja/2019.4/Manual/xr_input.html
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left,
            devices
        );
        if (devices.Count >= 1) leftController = devices[0];
        else return;    // fail

        // button setting
        // trigger
        leftTriggerDetector = new ButtonDetector(leftController.Value, CommonUsages.triggerButton);
        leftTriggerDetector.ButtonDown += (bd) => StartClickingByHand(leftHandGameObj);
        leftTriggerDetector.ButtonUp += (bd) => EndClicking(leftHandObj);
        // grab
        leftGripDetector = new ButtonDetector(leftController.Value, CommonUsages.gripButton);
        leftGripDetector.ButtonDown += (bd) => StartClickingByHand(leftHandGameObj, "grab", leftGrabbedObjects);
        leftGripDetector.ButtonUp += (bd) => EndClicking(leftHandObj, "ungrab", leftGrabbedObjects);
    }
    
    void SetupRightController()
    {
        // Search right hand controller
        // https://docs.unity3d.com/ja/2019.4/Manual/xr_input.html
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right,
            devices
        );
        if (devices.Count >= 1) rightController = devices[0];
        else return;    // fail

        // button setting
        // trigger
        rightTriggerDetector = new ButtonDetector(rightController.Value, CommonUsages.triggerButton);
        rightTriggerDetector.ButtonDown += (bd) => StartClickingByHand(rightHandGameObj);
        rightTriggerDetector.ButtonUp += (bd) => EndClicking(rightHandObj);
        // grab-related
        rightGripDetector = new ButtonDetector(rightController.Value, CommonUsages.gripButton);
        rightGripDetector.ButtonDown += (bd) => StartClickingByHand(rightHandGameObj, "grab", rightGrabbedObjects);
        rightGripDetector.ButtonUp += (bd) => EndClicking(rightHandObj, "ungrab", rightGrabbedObjects);
    }

    void SetHandCollider(GameObject handGameObj, SyncObject obj)
    {
        if (handGameObj.GetComponent<GrabDetector>() != null) return;
        handGameObj.AddComponent<GrabDetector>();
        handGameObj.GetComponent<GrabDetector>().Setup(obj);
    }

    ObjectSync FindMouseClickedObject()
    {
        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out var hit))
        {
            return hit.collider.GetComponentInParent<ObjectSync>();
        }
        else
        {
            return null;
        }
    }

    void MoveObjectByMouse(GameObject obj)
    {
        // The object moves within the same plane (keeps camera-coordinate Z)
        var z = Camera.main.transform.InverseTransformPoint(obj.transform.position).z;
        var mousePos = Input.mousePosition;
        obj.transform.position = Camera.main.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, z));
    }

    // This is also used for grabbing
    void StartClicking(SyncObject thisObj, ObjectSync clickedObj, string eventName, List<ObjectSync> objectList)
    {
        objectList.Add(clickedObj);
        clickedObj.SyncObject.SendEvent(eventName, thisObj.Id, new IValue[0]);
    }

    void StartClicking(SyncObject thisObj, ObjectSync clickedObj) => StartClicking(thisObj, clickedObj, "clickStart", clickedObjects);

    // Also used for grabbing
    void StartClickingByHand(GameObject handGameObj, string eventName, List<ObjectSync> objectList)
    {
        if (handGameObj == null) return;
        var detector = handGameObj.GetComponent<GrabDetector>();
        var handObj = handGameObj.GetComponent<ObjectSync>().SyncObject;
        foreach (var obj in detector.ObjectsToGrab)
        {
            var objSync = obj.GetComponent<ObjectSync>();
            objectList.Add(objSync);
            objSync.SyncObject.SendEvent(eventName, handObj.Id, new IValue[0]);
        }
    }

    void StartClickingByHand(GameObject handGameObj) => StartClickingByHand(handGameObj, "clickStart", clickedObjects);

    // Also used for grabbing
    void EndClicking(SyncObject thisObj, string eventName, List<ObjectSync> objectList)
    {
        foreach (var objSync in objectList)
        {
            objSync.SyncObject.SendEvent(eventName, thisObj.Id, new IValue[0]);
        }
        objectList.Clear();
    }

    void EndClicking(SyncObject thisObj) => EndClicking(thisObj, "clickEnd", clickedObjects);

    void OnBeforeSync(SyncObject obj, float dt)
    {
        obj.SetField("forward", new Primitive<float> { Value = forward });
        obj.SetField("sideways", new Primitive<float> { Value = sideways });
        obj.SetField("turn", new Primitive<float> { Value = turn });

        obj.SetField("lookAt", UnityUtil.ToVec(lookAt));
    }

    void OnAfterSync(SyncObject obj, float dt)
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
        obj.RegisterFieldUpdateHandler("mouseGrabber", OnMouseGrabberUpdated);

        OnLeftHandUpdated();
        OnRightHandUpdated();
        OnMouseGrabberUpdated();

        Blob vrmBlob;
        if (GetComponent<ObjectSync>().IsOriginal)
        {
            byte[] vrmData = File.ReadAllBytes(Settings.Instance.AvatarPath);
            // Use MIME type for GLTF binary https://www.iana.org/assignments/media-types/model/gltf-binary
            vrmBlob = new Blob(vrmData, "model/gltf-binary");
            BlobHandle vrmHandle = vrmBlob.GenerateHandle();
            node.WriteBlob(vrmHandle, vrmBlob);
            obj.SetField("vrm", vrmHandle);
        }
        else
        {
            while (!obj.HasField("vrm") || !(obj.GetField("vrm") is BlobHandle))
            {
                obj.WriteDebugLog("PlayerAvatar", "Field vrm not ready");
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
        // https://vrm-c.github.io/UniVRM/ja/api/sample/SimpleViewer.html
        // https://github.com/vrm-c/UniVRM/blob/e91ab9fc519aa387dc9b39044aa2189ff0382f15/Assets/VRM_Samples/SimpleViewer/ViewerUI.cs
        UniGLTF.GltfData gltf = new UniGLTF.GlbBinaryParser(vrmBlob.Data, vrmBlob.ToString()).Parse();
        VRMData vrm = new VRMData(gltf);

        using (var ctx = new VRMImporterContext(vrm))
        {
            var meta = ctx.ReadMeta();
            obj.WriteLog("PlayerAvatar", $"Loading VRM {meta.Title} created by {meta.Author} ({meta.ContactInformation})");
            obj.WriteLog("PlayerAvatar", $"AllowedUser={meta.AllowedUser}, ViolentUsage={meta.ViolentUssage}");
            obj.WriteLog("PlayerAvatar", $"SexualUsage={meta.SexualUssage}, CommercialUsage={meta.CommercialUssage}");
            obj.WriteLog("PlayerAvatar", $"OtherPermissionUrl={meta.OtherPermissionUrl}");
            obj.WriteLog("PlayerAvatar", $"LicenseType={meta.LicenseType}");
            obj.WriteLog("PlayerAvatar", $"OtherLicenseUrl={meta.OtherLicenseUrl}");

            UniGLTF.RuntimeGltfInstance instance = await ctx.LoadAsync(new VRMShaders.RuntimeOnlyAwaitCaller());

            instance.EnableUpdateWhenOffscreen();
            instance.ShowMeshes();

            // Enable collision (and character controller) again (see the disabling line above)
            GetComponent<CharacterController>().enabled = true;

            // Move VRM avatar inside this gameObject
            instance.Root.transform.SetParent(transform);
            instance.Root.transform.localPosition = Vector3.zero;
            instance.Root.transform.localRotation = Quaternion.identity;

            GetComponent<Animator>().avatar = instance.Root.GetComponent<Animator>().avatar;
        }
        
        obj.WriteLog("PlayerAvatar", $"VRM loaded");

        if (GetComponent<ObjectSync>().IsOriginal)
        {
            // Set up first person view (do not display avatar of the player)
            //  https://vrm.dev/en/univrm/components/univrm_firstperson/
            //  https://vrm.dev/en/dev/univrm-0.xx/programming/univrm_use_firstperson/
            var fp = GetComponentInChildren<VRMFirstPerson>();
            fp.Setup();
            if (XROrigin != null)
            {
                XROrigin.transform.position = fp.FirstPersonBone.position + fp.FirstPersonBone.rotation * fp.FirstPersonOffset;
                XROrigin.transform.rotation = transform.rotation;  // face forward
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
            if (leftController.HasValue)
            {
                // If left hand device is present
                var leftId = await node.CreateObject();
                leftHandObj = node.Objects[leftId];
                leftHandObj.SetField("parent", obj.GetObjectRef());
                leftHandObj.SetField("tags", new Sequence(new IValue[] {
                    new Primitive<string>("constantVelocity"),
                    new Primitive<string>("collider")
                }));
                obj.SetField("leftHand", leftHandObj.GetObjectRef());
            }
            // Right hand
            if (rightController.HasValue)
            {
                // If right hand device is present
                var rightId = await node.CreateObject();
                rightHandObj = node.Objects[rightId];
                rightHandObj.SetField("parent", obj.GetObjectRef());
                rightHandObj.SetField("tags", new Sequence(new IValue[] {
                    new Primitive<string>("constantVelocity"),
                    new Primitive<string>("collider")
                }));
                obj.SetField("rightHand", rightHandObj.GetObjectRef());
            }
            // Mouse
            var mouseGrabberId = await node.CreateObject();
            mouseGrabberObj = node.Objects[mouseGrabberId];
            mouseGrabberObj.SetField("parent", obj.GetObjectRef());
            mouseGrabberObj.SetField("tags", new Sequence(new IValue[] {
                new Primitive<string>("constantVelocity"),
                new Primitive<string>("collider")
            }));
            obj.SetField("mouseGrabber", mouseGrabberObj.GetObjectRef());
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

    void OnMouseGrabberUpdated()
    {
        SyncObject obj = GetComponent<ObjectSync>().SyncObject;

        if (obj.TryGetField("mouseGrabber", out ObjectRef mouseGrabberRef))
        {
            mouseGrabberObj = obj.Node.Objects[mouseGrabberRef.Id];
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
}

}   // end namespace
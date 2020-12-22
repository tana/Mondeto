using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class SyncBehaviour : MonoBehaviour
{
    public bool IsServer = false;

    public SyncNode Node { get; private set; }
    public bool Ready = false;

    public readonly Dictionary<uint, GameObject> GameObjects = new Dictionary<uint, GameObject>();

    // For adding objects not defined in YAML (e.g. player avatar)
    public GameObject[] OriginalObjects = new GameObject[0];

    // For FPS measurement
    float countStartTime = -1;
    int count = 0;
    const int countPeriod = 5000;

    HashSet<uint> OriginalObjectIds = new HashSet<uint>();

    public GameObject PlayerPrefab;

    // For initial blob loading
    bool isFirst = true;
    public GameObject LoadingScreen;
    const float FadeOutDuration = 1.0f;

    // Start is called before the first frame update
    async void Start()
    {
        if (Application.isEditor)
        {
            // Set directory settings (when running in Unity Editor)
            Settings.Instance.AvatarPath = Application.streamingAssetsPath + "/default_avatar.vrm";
            Settings.Instance.MimeTypesPath = Application.streamingAssetsPath + "/config/mime.types";
            Settings.Instance.SceneFile = Application.streamingAssetsPath + "/scene.yml";
            Settings.Instance.TempDirectory = Application.temporaryCachePath;
        }

        if (IsServer)
        {
            Node = new SyncServer(Settings.Instance.SignalerUrlForServer);
        }
        else
        {
            Node = new SyncClient(Settings.Instance.SignalerUrlForClient);
        }

        Node.ObjectCreated += OnObjectCreated;
        Node.ObjectDeleted += OnObjectDeleted;

        RegisterTags();

        try
        {
            await Node.Initialize();
        }
        catch (SignalingException e)
        {
            await FadeLoadingScreen();
            await GameObject.Find("LocalPlayer")?.GetComponent<Menu>()?.ShowDialog(
                "Signaling Error",
                e.ToString()
            );
            Application.Quit(); // Note: Does not stop in Unity Editor. https://docs.unity3d.com/ja/current/ScriptReference/Application.html
            return;
        }
        catch (ConnectionException e)
        {
            await FadeLoadingScreen();
            await GameObject.Find("LocalPlayer")?.GetComponent<Menu>()?.ShowDialog(
                "Connection Error",
                e.ToString()
            );
            Application.Quit(); // Note: Does not stop in Unity Editor. https://docs.unity3d.com/ja/current/ScriptReference/Application.html
            return;
        }

        Ready = true;

        if (IsServer)
        {
            // Load scene from YAML
            var loader = new SceneLoader(Node);
            await loader.LoadFile(Settings.Instance.SceneFile);
        }

        // Add objects defined in Unity scene, such as player avatar
        foreach (GameObject obj in OriginalObjects)
        {
            var id = await Node.CreateObject();
            OriginalObjectIds.Add(id);
            if (GameObjects.ContainsKey(id))
            {
                // Delete provisional GameObject created in OnObjectCreated
                ReplaceObject(Node.Objects[id], obj);
            }
            // With SynchronizationContext of Unity, the line below will run in main thread.
            // https://qiita.com/toRisouP/items/a2c1bb1b0c4f73366bc6
            SetupObjectSync(obj, Node.Objects[id]);
        }
    }

    void RegisterTags()
    {
        // Tags that create new GameObject
        RegisterObjectTag<PlayerAvatar>("player", () => Instantiate(PlayerPrefab));
        // primitives
        var primitives = new (string, PrimitiveType)[] {
            ("cube", PrimitiveType.Cube),
            ("sphere", PrimitiveType.Sphere),
            ("plane", PrimitiveType.Plane),
            ("cylinder", PrimitiveType.Cylinder),
        };
        foreach (var (name, primitiveType) in primitives)
        {
            RegisterObjectTag<PrimitiveTag>(name, () => {
                var gameObj = GameObject.CreatePrimitive(primitiveType);
                Destroy(gameObj.GetComponent<Collider>());
                return gameObj;
            });
        }
        RegisterObjectTag<ModelSync>("model", () => new GameObject());
        RegisterObjectTag<LightTag>("light", () => new GameObject());
        RegisterObjectTag<TextSync>("text", () => new GameObject());
        RegisterObjectTag<MeshTag>("mesh", () => new GameObject());
        RegisterObjectTag<LineTag>("line", () => new GameObject());

        // Tags that uses already existing GameObject
        RegisterComponentTag<RigidbodySync>("physics");
        RegisterComponentTag<ColliderSync>("collider");
        RegisterComponentTag<MaterialSync>("material");
        RegisterComponentTag<ConstantVelocity>("constantVelocity");
        RegisterComponentTag<AudioPlayer>("audioPlayer");
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (!Ready) return;

        // FPS measurement
        if (count == 0)
        {
            if (countStartTime > 0)
            {
                float fps = countPeriod / (Time.time - countStartTime);
                Logger.Log("SyncBehaviour", $"Average frame rate = {fps} fps");
            }
            countStartTime = Time.time;
        }
        count = (count + 1) % countPeriod;

        //if (Time.frameCount % 6 != 0) return;

        Node.SyncFrame(Time.fixedDeltaTime);

        if (isFirst)
        {
            isFirst = false;
            LoadBlobs();
        }
    }

    async void LoadBlobs()
    {
        var blobs = EnumerateBlobs();
        Logger.Debug("SyncBehaviour", $"Number of initial blobs = {blobs.Count}");
        foreach (var blobHandle in blobs)
        {
            await Node.ReadBlob(blobHandle);
        }
        Logger.Debug("SyncBehaviour", "Received all blobs");
        await UniTask.Delay(2500); // Fixed delay
        await FadeLoadingScreen();
    }

    // Prepare Unity GameObject for new SyncObject
    void OnObjectCreated(uint id)
    {
        SyncObject obj = Node.Objects[id];
        // Provisional empty GameObject for all objects
        var gameObj = new GameObject();
        gameObj.transform.SetParent(this.transform);
        SetupObjectSync(gameObj, obj);
    }

    public void RegisterObjectTag<T>(string tagName, Func<GameObject> objectCreaetor) where T : MonoBehaviour, ITag
    {
        Node.RegisterTag(tagName, obj => {
            if (OriginalObjectIds.Contains(obj.Id))
            {
                return GameObjects[obj.Id].GetComponent<T>();   // TODO: null handling (might happen for constantVelocity of local player?)
            }

            // Because Object Tags creates a new Unity GameObject,
            // these can be added only once per one SyncObject.
            // This also happens for GameObjects in OriginalObjects that have the above tags in initialTags.

            var gameObj = objectCreaetor();
            T tag = gameObj.GetComponent<T>() ?? gameObj.AddComponent<T>();
            gameObj.transform.SetParent(this.transform);
            if (GameObjects.ContainsKey(obj.Id))
            {
                // Replace old GameObject
                Logger.Log("SyncBehaviour", $"Replacing GameObject because a GameObject is already created for object {obj.Id}");
                ReplaceObject(obj, gameObj);
            }
            SetupObjectSync(gameObj, obj);

            return tag;
        });
    }

    public void RegisterComponentTag<T>(string tagName) where T : MonoBehaviour, ITag
    {
        Node.RegisterTag(tagName, obj => {
            // A Component Tag requires a GameObject
            if (!GameObjects.ContainsKey(obj.Id))
            {
                obj.WriteErrorLog("SyncBehaviour", $"Cannot add tag {tagName} because there is no GameObject corresponds to object {obj.Id}");
                return null;    // failed
            }

            var gameObj = GameObjects[obj.Id];
            return gameObj.AddComponent<T>();
        });
    }

    void SetupObjectSync(GameObject gameObj, SyncObject obj)
    {
        var id = obj.Id;

        if (!GameObjects.ContainsKey(id))
        {
            ObjectSync sync = gameObj.GetComponent<ObjectSync>();
            if (sync == null) sync = gameObj.AddComponent<ObjectSync>();
            sync.IsOriginal = (obj.OriginalNodeId == Node.NodeId);
            sync.NetManager = this.gameObject;
            GameObjects[id] = gameObj;
            sync.Initialize(obj);
            Logger.Debug("SyncBehaviour", "Created GameObject " + gameObj.ToString() + " for ObjectId=" + id);
        }
    }

    void ReplaceObject(SyncObject obj, GameObject newGameObj)
    {
        var oldGameObj = GameObjects[obj.Id];
        // Move children
        while (oldGameObj.transform.childCount > 0)
        {
            oldGameObj.transform.GetChild(0).SetParent(newGameObj.transform);
        }
        // Delete
        Destroy(oldGameObj);
        GameObjects.Remove(obj.Id);
    }

    void OnObjectDeleted(uint id)
    {
        // destroy and remove deleted objects
        if (GameObjects.ContainsKey(id))
        {
            Destroy(GameObjects[id]);
            GameObjects.Remove(id);
        }
    }

    public void DeleteOriginal(GameObject obj)
    {
        var ids = new HashSet<uint>();
        foreach (var pair in GameObjects)
        {
            if (pair.Value == obj) ids.Add(pair.Key);
        }
        foreach (var id in ids)
        {
            Node.DeleteObject(id);
            GameObjects.Remove(id);
        }
    }

    List<BlobHandle> EnumerateBlobs()
    {
        var handles = new List<BlobHandle>();
        foreach (var obj in Node.Objects.Values)
        {
            foreach (var field in obj.Fields.Values)
            {
                // Currently, BlobHandle inside Sequence is ignored
                if (field.Value is BlobHandle blobHandle)
                {
                    handles.Add(blobHandle);
                }
            }
        }

        return handles;
    }

    async Task FadeLoadingScreen()
    {
        var renderers = LoadingScreen.GetComponentsInChildren<Renderer>();

        float t = 0.0f;
        
        while (t < FadeOutDuration)
        {
            float opacity = 0.5f * (1 + Mathf.Cos(Mathf.PI * t / FadeOutDuration));
            foreach (Renderer renderer in renderers)
            {
                renderer.material.SetFloat("_Opacity", opacity);
            }

            await UniTask.WaitForEndOfFrame();
            t += Time.deltaTime;
        }

        LoadingScreen.SetActive(false);
    }

    public void OnDestroy()
    {
        Node.Dispose();
    }
}

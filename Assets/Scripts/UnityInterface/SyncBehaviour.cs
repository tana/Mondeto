using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class SyncBehaviour : MonoBehaviour
{
    public bool IsServer = false;

    public SyncNode Node { get; private set; }
    public bool Ready = false;

    public readonly Dictionary<uint, GameObject> GameObjects = new Dictionary<uint, GameObject>();

    // For adding objects not defined in YAML (e.g. player avatar)
    public GameObject[] OriginalObjects = new GameObject[0];

    public string SceneYamlPath = "scene.yml";

    // For FPS measurement
    float countStartTime = -1;
    int count = 0;
    const int countPeriod = 5000;

    // Object Tag (Tags that create a new GameObject)
    Dictionary<string, Func<SyncObject, GameObject>> ObjectTagInitializers = new Dictionary<string, Func<SyncObject, GameObject>>();

    // Component Tag (Tags that need a GameObject)
    Dictionary<string, Action<SyncObject, GameObject>> ComponentTagInitializers = new Dictionary<string, Action<SyncObject, GameObject>>();

    HashSet<uint> OriginalObjectIds = new HashSet<uint>();

    public GameObject PlayerPrefab;

    // Start is called before the first frame update
    async void Start()
    {
        // Tags that create new GameObject
        RegisterObjectTag("player", obj => Instantiate(PlayerPrefab));
        // primitives
        var primitives = new (string, PrimitiveType)[] {
            ("cube", PrimitiveType.Cube),
            ("sphere", PrimitiveType.Sphere),
            ("plane", PrimitiveType.Plane),
            ("cylinder", PrimitiveType.Cylinder),
        };
        foreach (var (name, primitiveType) in primitives)
        {
            RegisterObjectTag(name, obj => {
                var gameObj = GameObject.CreatePrimitive(primitiveType);
                Destroy(gameObj.GetComponent<Collider>());
                return gameObj;
            });
        }
        RegisterObjectTag("model", obj => {
            var gameObj = new GameObject();
            gameObj.AddComponent<ModelSync>().Initialize(obj);
            return gameObj;
        });
        RegisterObjectTag("light", obj => {
            var gameObj = new GameObject();
            // https://docs.unity3d.com/ja/2019.4/Manual/Lighting.html
            var light = gameObj.AddComponent<Light>();

            light.shadows = LightShadows.Soft;  // TODO:

            // TODO: real-time sync
            if (obj.TryGetField("color", out Vec colorVec))
            {
                light.color = new Color(colorVec.X, colorVec.Y, colorVec.Z);
            }
            // https://docs.unity3d.com/ja/2019.4/Manual/Lighting.html
            if (obj.TryGetFieldPrimitive("lightType", out string lightType))
            {
                switch (lightType)
                {
                    case "directional":
                        light.type = LightType.Directional;
                        break;
                    case "point":
                        light.type = LightType.Point;
                        break;
                    // TODO
                }
            }

            return gameObj;
        });

        // Tags that uses already existing GameObject
        RegisterComponentTag("physics", (obj, gameObj) => {
            gameObj.AddComponent<RigidbodySync>().Initialize(obj);
        });
        RegisterComponentTag("collider", (obj, gameObj) => {
            gameObj.AddComponent<ColliderSync>().Initialize(obj);
        });
        // TODO: think more appropriate name for this tag
        RegisterComponentTag("material", (obj, gameObj) => {
            gameObj.AddComponent<MaterialSync>().Initialize(obj);
        });
        RegisterComponentTag("constantVelocity", (obj, gameObj) => {
            gameObj.AddComponent<ConstantVelocity>().Initialize(obj);
        });

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

        await Node.Initialize();
        Ready = true;

        if (IsServer)
        {
            // Load scene from YAML
            var loader = new SceneLoader(Node);
            await loader.Load(new System.IO.StreamReader(SceneYamlPath));
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
    }

    // Prepare Unity GameObject for new SyncObject
    void OnObjectCreated(uint id)
    {
        SyncObject obj = Node.Objects[id];
        // Provisional empty GameObject for all objects
        var gameObj = new GameObject();
        gameObj.transform.SetParent(this.transform);
        SetupObjectSync(gameObj, obj);
        obj.TagAdded += OnTagAdded;
    }

    void OnTagAdded(SyncObject obj, string tag)
    {
        if (ObjectTagInitializers.ContainsKey(tag) && !OriginalObjectIds.Contains(obj.Id))
        {
            // Because these tags creates a new Unity GameObject,
            // these can be added only once per one SyncObject.
            // This also happens for GameObjects in OriginalObjects that have the above tags in initialTags.

            var gameObj = ObjectTagInitializers[tag](obj);
            gameObj.transform.SetParent(this.transform);
            if (GameObjects.ContainsKey(obj.Id))
            {
                // Replace old GameObject
                Logger.Log("SyncBehaviour", $"Replacing GameObject because a GameObject is already created for object {obj.Id}");
                ReplaceObject(obj, gameObj);
            }
            SetupObjectSync(gameObj, obj);
        }
        else if (ComponentTagInitializers.ContainsKey(tag))
        {
            // These tag require GameObject
            if (!GameObjects.ContainsKey(obj.Id))
            {
                Logger.Log("SyncBehaviour", $"Tag {tag} is ignored because there is no GameObject corresponds to  object {obj.Id}");
                return;
            }

            var gameObj = GameObjects[obj.Id];
            ComponentTagInitializers[tag](obj, gameObj);
        }
        else if (tag == "grabbable")    // TODO: move to somewhere of core, not in Unity-specific code
        {
            (new GrabbableTag()).Initialize(obj);
        }
        else if (tag == "objectMoveButton") // TODO: move to somewhere of core, not in Unity-specific code
        {
            (new ObjectMoveButtonTag()).Initialize(obj);
        }
        else
        {
            Logger.Log("SyncBehaviour", "Unknown tag " + tag);
        }
    }

    public void RegisterObjectTag(string tagName, Func<SyncObject, GameObject> initializer)
    {
        ObjectTagInitializers[tagName] = initializer;
    }

    public void RegisterComponentTag(string tagName, Action<SyncObject, GameObject> initializer)
    {
        ComponentTagInitializers[tagName] = initializer;
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

    public void OnDestroy()
    {
        Node.Dispose();
    }
}

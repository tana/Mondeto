﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class SyncBehaviour : MonoBehaviour
{
    public bool IsServer = false;
    public string signalerUri;

    public SyncNode Node { get; private set; }
    public bool Ready = false;

    Dictionary<uint, GameObject> gameObjects = new Dictionary<uint, GameObject>();

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

    public GameObject PlayerPrefab, StagePrefab;

    // Start is called before the first frame update
    async void Start()
    {
        // Tags that create new GameObject
        RegisterObjectTag("desktopAvatar", obj =>  Instantiate(PlayerPrefab));
        RegisterObjectTag("stage", obj =>  Instantiate(StagePrefab, transform));
        RegisterObjectTag("cube", obj => {
            var gameObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(gameObj.GetComponent<Collider>());
            return gameObj;
        });
        RegisterObjectTag("sphere", obj => {
            var gameObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(gameObj.GetComponent<Collider>());
            return gameObj;
        });
        // Tags that uses already existing GameObject
        RegisterComponentTag("physics", (obj, gameObj) => {
            var rb = gameObj.AddComponent<Rigidbody>();
            gameObj.AddComponent<RigidbodySync>();
            gameObj.GetComponent<ObjectSync>().ForceApplyState();   // TODO: consider better design
        });
        RegisterComponentTag("collider", (obj, gameObj) => {
            if (obj.HasTag("cube"))
            {
                gameObj.AddComponent<BoxCollider>();
            }
            else if (obj.HasTag("sphere"))
            {
                gameObj.AddComponent<SphereCollider>();
            }
            else    // FIXME:
            {
                gameObj.AddComponent<MeshCollider>();
            }
        });

        if (IsServer)
        {
            Node = new SyncServer(signalerUri);
        }
        else
        {
            Node = new SyncClient(signalerUri);
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
            // With SynchronizationContext of Unity, the line below will run in main thread.
            // https://qiita.com/toRisouP/items/a2c1bb1b0c4f73366bc6
            gameObjects[id] = obj;
            obj.GetComponent<ObjectSync>().SyncObject = Node.Objects[id];
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

        Node.SyncFrame();
    }

    // Prepare Unity GameObject for new SyncObject
    void OnObjectCreated(uint id)
    {
        SyncObject obj = Node.Objects[id];
        obj.TagAdded += OnTagAdded;
    }

    void OnTagAdded(SyncObject obj, string tag)
    {
        if (ObjectTagInitializers.ContainsKey(tag))
        {
            // Because these tags creates an Unity GameObject,
            // these can be added only once per one SyncObject.
            // This also happens for GameObjects in OriginalObjects that have the above tags in initialTags.
            if (gameObjects.ContainsKey(obj.Id))
            {
                Logger.Log("SyncBehaviour", $"Tag {tag} is ignored because GameObject is already created for object {obj.Id}");
                return;
            }

            var gameObj = ObjectTagInitializers[tag](obj);
            gameObj.transform.SetParent(this.transform);
            SetUpObjectSync(gameObj, obj);
        }
        else if (ComponentTagInitializers.ContainsKey(tag))
        {
            // These tag require GameObject
            if (!gameObjects.ContainsKey(obj.Id))
            {
                Logger.Log("SyncBehaviour", $"Tag {tag} is ignored because there is no GameObject corresponds to  object {obj.Id}");
                return;
            }

            var gameObj = gameObjects[obj.Id];
            ComponentTagInitializers[tag](obj, gameObj);
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

    void SetUpObjectSync(GameObject gameObj, SyncObject obj)
    {
        var id = obj.Id;

        if (!gameObjects.ContainsKey(id))
        {
            var sync = gameObj.AddComponent<ObjectSync>();
            sync.IsOriginal = (obj.OriginalNodeId == Node.NodeId);
            sync.NetManager = this.gameObject;
            gameObjects[id] = gameObj;
            gameObjects[id].GetComponent<ObjectSync>().SyncObject = obj;
            Logger.Debug("SyncBehaviour", "Created GameObject " + gameObj.ToString() + " for ObjectId=" + id);
        }
    }

    void OnObjectDeleted(uint id)
    {
        // destroy and remove deleted objects
        if (gameObjects.ContainsKey(id))
        {
            Destroy(gameObjects[id]);
            gameObjects.Remove(id);
        }
    }

    public void DeleteOriginal(GameObject obj)
    {
        var ids = new HashSet<uint>();
        foreach (var pair in gameObjects)
        {
            if (pair.Value == obj) ids.Add(pair.Key);
        }
        foreach (var id in ids)
        {
            Node.DeleteObject(id);
            gameObjects.Remove(id);
        }
    }

    public void OnDestroy()
    {
        Node.Dispose();
    }
}

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

    Dictionary<int, GameObject> prefabs = new Dictionary<int, GameObject>();

    public PrefabEntry[] PrefabsForClones = new PrefabEntry[0];

    // For adding objects not defined in YAML (e.g. player avatar)
    public GameObject[] OriginalObjects = new GameObject[0];

    public string SceneYamlPath = "scene.yml";

    // For FPS measurement
    float countStartTime = -1;
    int count = 0;
    const int countPeriod = 5000;

    [System.Serializable]
    public struct PrefabEntry
    {
        public int Tag;
        public GameObject Prefab;
    }

    // Start is called before the first frame update
    async void Start()
    {
        foreach (PrefabEntry entry in PrefabsForClones)
        {
            prefabs[entry.Tag] = entry.Prefab;
        }

        if (IsServer)
        {
            Node = new SyncServer(signalerUri);
        }
        else
        {
            Node = new SyncClient(signalerUri);
        }

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

        foreach (var pair in Node.Objects)
        {
            var id = pair.Key;
            var obj = pair.Value;

            if (obj.OriginalNodeId == Node.NodeId)
            {
                if (!gameObjects.ContainsKey(id) || gameObjects[id] == null)
                    continue;
                gameObjects[id].GetComponent<ObjectSync>().SyncObject = obj;
            }
        }

        Node.SyncFrame();

        foreach (var pair in Node.Objects)
        {
            var id = pair.Key;
            var obj = pair.Value;

            // Prepare Unity GameObject for new SyncObject
            var tag = ((Primitive<int>)obj.GetField("tag")).Value;
            if (tag == 0)
            {
                Logger.Log("SyncBehaviour", "ObjectId=" + id + " is not ready");
                continue;   // state is not ready
            }
            if (!gameObjects.ContainsKey(id))
            {
                var gameObj = Instantiate(prefabs[tag], transform);
                var sync = gameObj.AddComponent<ObjectSync>();
                sync.ObjectTag = tag;
                sync.IsOriginal = (obj.OriginalNodeId == Node.NodeId);
                sync.NetManager = this.gameObject;
                gameObjects[id] = gameObj;
                gameObjects[id].GetComponent<ObjectSync>().SyncObject = obj;
                Logger.Debug("SyncBehaviour", "Created GameObject " + gameObj.ToString() + " for ObjectId=" + id);
                // TODO: consider better design
                sync.ForceApplyState(); // Set initial state of Unity GameObject based on SyncObject
            }
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

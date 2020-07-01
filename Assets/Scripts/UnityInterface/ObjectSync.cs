using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ObjectSync : MonoBehaviour
{
    public string InitialTags = "";

    // If ObjectSync is created programatically, this flag prevents overwriting already specified tags
    public bool SetInitialTags = false; 

    public GameObject NetManager;

    public bool IsOriginal;

    public SyncObject SyncObject;

    Vector3 posOffset;

    SyncBehaviour syncBehaviour;
    
    bool added = false;
    bool ready = false;

    public SyncNode Node { get => syncBehaviour.Node; }

    // Start is called before the first frame update
    void Start()
    {
        syncBehaviour = NetManager.GetComponent<SyncBehaviour>();
        posOffset = NetManager.transform.position;
    }

    void FixedUpdate()
    {
        if (!syncBehaviour.Ready) return;
        if (SyncObject == null) return;
        if (!ready)
        {
            ready = true;

            SyncObject.BeforeSync += OnBeforeSync;
            SyncObject.AfterSync += OnAfterSync;

            if (IsOriginal && SetInitialTags)
            {
                SyncObject.SetField("tags", new Sequence { 
                    Elements = InitialTags.Split(' ').Where(str => str.Length > 0).Select(tag => (IValue)(new Primitive<string> { Value = tag })).ToList()
                });
            }

            // TODO: consider better design
            ForceApplyState(); // Set initial state of Unity GameObject based on SyncObject

            SendMessage("OnSyncReady", options: SendMessageOptions.DontRequireReceiver);
            return;
        }
    }

    void OnDestroy()
    {
        if (IsOriginal)
        {
            syncBehaviour.DeleteOriginal(gameObject);
        }
    }

    void OnBeforeSync(SyncObject obj)
    {
        obj.SetField("position", UnityUtil.ToVec(transform.position - posOffset));
        obj.SetField("rotation", UnityUtil.ToQuat(transform.rotation));
    }

    void OnAfterSync(SyncObject obj)
    {
        if (IsOriginal) return;
        ForceApplyState();
    }

    public void ForceApplyState()   // TODO: move
    {
        if (SyncObject.HasField("position") && SyncObject.GetField("position") is Vec position)
            transform.position = UnityUtil.FromVec(position) + posOffset;
        if (SyncObject.HasField("rotation") && SyncObject.GetField("rotation") is Quat rotation)
            transform.rotation = UnityUtil.FromQuat(rotation);
    }
}

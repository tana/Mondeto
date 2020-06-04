using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ObjectSync : MonoBehaviour
{
    public int ObjectTag;
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
        if (!added)
        {
            if (IsOriginal)
            {
                syncBehaviour.AddOriginal(gameObject);
            }
            added = true;
        }
        if (SyncObject == null) return;
        if (!ready)
        {
            ready = true;
            SendMessage("OnSyncReady", options: SendMessageOptions.DontRequireReceiver);
            return;
        }

        if (!IsOriginal)
        {
            ApplyState(SyncObject);
        }

        if (IsOriginal)
        {
            EncodeState(SyncObject);
        }
    }

    void OnDestroy()
    {
        if (IsOriginal)
        {
            syncBehaviour.DeleteOriginal(gameObject);
        }
    }

    void EncodeState(SyncObject obj)
    {
        obj.SetField("tag", new Primitive<int> { Value = this.ObjectTag });
        obj.SetField("position", UnityUtil.ToVec(transform.position - posOffset));
        obj.SetField("rotation", UnityUtil.ToQuat(transform.rotation));
        obj.SetField("velocity", UnityUtil.ToVec(GetComponent<Rigidbody>().velocity));
        obj.SetField("angularVelocity", UnityUtil.ToVec(GetComponent<Rigidbody>().angularVelocity));
    }

    void ApplyState(SyncObject obj)
    {
        var rb = GetComponent<Rigidbody>();
        // TODO error check
        transform.position = UnityUtil.FromVec((Vec)obj.GetField("position")) + posOffset;
        transform.rotation = UnityUtil.FromQuat((Quat)obj.GetField("rotation"));
        rb.velocity = UnityUtil.FromVec((Vec)obj.GetField("velocity"));
        rb.angularVelocity = UnityUtil.FromVec((Vec)obj.GetField("angularVelocity"));
    }
}

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
        obj.Fields["tag"] = new Primitive<int> { Value = this.ObjectTag };
        obj.Fields["position"] = UnityUtil.ToVec(transform.position - posOffset);
        obj.Fields["rotation"] = UnityUtil.ToQuat(transform.rotation);
        obj.Fields["velocity"] = UnityUtil.ToVec(GetComponent<Rigidbody>().velocity);
        obj.Fields["angularVelocity"] = UnityUtil.ToVec(GetComponent<Rigidbody>().angularVelocity);
    }

    void ApplyState(SyncObject obj)
    {
        var rb = GetComponent<Rigidbody>();
        // TODO error check
        transform.position = UnityUtil.FromVec((Vec)obj.Fields["position"]) + posOffset;
        transform.rotation = UnityUtil.FromQuat((Quat)obj.Fields["rotation"]);
        rb.velocity = UnityUtil.FromVec((Vec)obj.Fields["velocity"]);
        rb.angularVelocity = UnityUtil.FromVec((Vec)obj.Fields["angularVelocity"]);
    }
}

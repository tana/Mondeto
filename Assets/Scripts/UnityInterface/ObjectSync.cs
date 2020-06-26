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
        if (SyncObject == null) return;
        if (!ready)
        {
            ready = true;
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

    public void EncodeState()
    {
        if (SyncObject == null) return;

        SyncObject.SetField("tag", new Primitive<int> { Value = this.ObjectTag });
        SyncObject.SetField("position", UnityUtil.ToVec(transform.position - posOffset));
        SyncObject.SetField("rotation", UnityUtil.ToQuat(transform.rotation));
        SyncObject.SetField("velocity", UnityUtil.ToVec(GetComponent<Rigidbody>().velocity));
        SyncObject.SetField("angularVelocity", UnityUtil.ToVec(GetComponent<Rigidbody>().angularVelocity));
    }

    public void ApplyState()
    {
        if (SyncObject == null) return;

        var rb = GetComponent<Rigidbody>();
        if (SyncObject.HasField("position"))
            transform.position = UnityUtil.FromVec((Vec)SyncObject.GetField("position")) + posOffset;
        if (SyncObject.HasField("rotation"))
            transform.rotation = UnityUtil.FromQuat((Quat)SyncObject.GetField("rotation"));
        if (SyncObject.HasField("velocity"))
            rb.velocity = UnityUtil.FromVec((Vec)SyncObject.GetField("velocity"));
        if (SyncObject.HasField("angularVelocity"))
            rb.angularVelocity = UnityUtil.FromVec((Vec)SyncObject.GetField("angularVelocity"));
    }
}

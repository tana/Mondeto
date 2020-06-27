using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RigidbodySync : MonoBehaviour
{
    public void OnSyncReady()
    {
        var sync = GetComponent<ObjectSync>();
        sync.SyncObject.BeforeSync += OnBeforeSync;
        sync.SyncObject.AfterSync += OnAfterSync;
    }

    void OnBeforeSync(SyncObject obj)
    {
        obj.SetField("velocity", UnityUtil.ToVec(GetComponent<Rigidbody>().velocity));
        obj.SetField("angularVelocity", UnityUtil.ToVec(GetComponent<Rigidbody>().angularVelocity));
    }

    void OnAfterSync(SyncObject obj)
    {
        if (GetComponent<ObjectSync>().IsOriginal) return;

        var rb = GetComponent<Rigidbody>();

        if (obj.HasField("velocity") && obj.GetField("velocity") is Vec velocity)
            rb.velocity = UnityUtil.FromVec(velocity);
        if (obj.HasField("angularVelocity") && obj.GetField("angularVelocity") is Vec angularVelocity)
            rb.angularVelocity = UnityUtil.FromVec(angularVelocity);
    }
}

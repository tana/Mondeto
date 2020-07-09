using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RigidbodySync : MonoBehaviour
{
    public void Initialize(SyncObject obj)
    {
        var rb = gameObject.AddComponent<Rigidbody>();
        obj.BeforeSync += OnBeforeSync;
        obj.AfterSync += OnAfterSync;
        ApplyState(obj);
    }

    void ApplyState(SyncObject obj)
    {
        var rb = GetComponent<Rigidbody>();

        if (obj.HasField("velocity") && obj.GetField("velocity") is Vec velocity)
            rb.velocity = UnityUtil.FromVec(velocity);
        if (obj.HasField("angularVelocity") && obj.GetField("angularVelocity") is Vec angularVelocity)
            rb.angularVelocity = UnityUtil.FromVec(angularVelocity);
        if (obj.HasField("mass") && obj.GetField("mass") is Primitive<float> massValue)
            rb.mass = massValue.Value;
    }

    void OnBeforeSync(SyncObject obj)
    {
        obj.SetField("velocity", UnityUtil.ToVec(GetComponent<Rigidbody>().velocity));
        obj.SetField("angularVelocity", UnityUtil.ToVec(GetComponent<Rigidbody>().angularVelocity));
    }

    void OnAfterSync(SyncObject obj)
    {
        if (GetComponent<ObjectSync>().IsOriginal) return;

        ApplyState(obj);
    }
}

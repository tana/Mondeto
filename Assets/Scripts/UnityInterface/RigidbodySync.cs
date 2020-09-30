using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RigidbodySync : MonoBehaviour
{
    bool beforeSync = false;
    SyncObject obj;

    public void Initialize(SyncObject obj)
    {
        this.obj = obj;
        var rb = gameObject.AddComponent<Rigidbody>();
        obj.BeforeSync += OnBeforeSync;
        obj.RegisterFieldUpdateHandler("mass", HandleMassUpdate);
        obj.RegisterFieldUpdateHandler("velocity", HandleUpdate);
        obj.RegisterFieldUpdateHandler("angularVelocity", HandleUpdate);

        HandleMassUpdate();
        HandleUpdate();
    }

    void HandleUpdate()
    {
        if (beforeSync) return;

        var rb = GetComponent<Rigidbody>();

        if (obj.HasField("velocity") && obj.GetField("velocity") is Vec velocity)
            rb.velocity = transform.TransformVector(UnityUtil.FromVec(velocity));
        if (obj.HasField("angularVelocity") && obj.GetField("angularVelocity") is Vec angularVelocity)
            rb.angularVelocity = transform.TransformVector(UnityUtil.FromVec(angularVelocity));
    }

    void HandleMassUpdate()
    {
        if (beforeSync) return;

        var rb = GetComponent<Rigidbody>();
        if (obj.TryGetFieldPrimitive("mass", out float mass))
            rb.mass = mass;
    }

    void OnBeforeSync(SyncObject obj, float dt)
    {
        beforeSync = true;

        obj.SetField("velocity", UnityUtil.ToVec(transform.InverseTransformVector(GetComponent<Rigidbody>().velocity)));
        obj.SetField("angularVelocity", UnityUtil.ToVec(transform.InverseTransformVector(GetComponent<Rigidbody>().angularVelocity)));

        beforeSync = false;
    }

    void OnDestroy()
    {
        obj.DeleteFieldUpdateHandler("mass", HandleMassUpdate);
        obj.DeleteFieldUpdateHandler("velocity", HandleUpdate);
        obj.DeleteFieldUpdateHandler("angularVelocity", HandleUpdate);
    }
}

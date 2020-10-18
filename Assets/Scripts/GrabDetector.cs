using System;
using System.Collections.Generic;
using UnityEngine;

// FIXME: more flexible approach (e.g. using collider tag with shape setting and collision events)
public class GrabDetector : MonoBehaviour
{
    public readonly HashSet<GameObject> ObjectsToGrab = new HashSet<GameObject>();

    SphereCollider col;

    public void Setup(SyncObject syncObj)
    {
        // To detect collision with static colliders, kinematic Rigidbody is needed
        //  (See https://docs.unity3d.com/ja/2019.4/Manual/CollidersOverview.html )
        // Therefore, the object have to be non-tangible.
        syncObj.SetField("isTangible", new Primitive<int>(0));

        if (GetComponent<ColliderSync>() == null) return;
        col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.3f;  // FIXME: setting
        GetComponent<ColliderSync>().AddCollider(col);
    }

    void OnTriggerEnter(Collider other)
    {
        // synchronized object only
        // GetComponentInParent is used because a model can have Collider in child meshes.
        GameObject obj = other.GetComponentInParent<ObjectSync>().gameObject;
        if (obj != null)
        {
            ObjectsToGrab.Add(obj);
        }
    }

    void OnTriggerExit(Collider other)
    {
        GameObject obj = other.GetComponentInParent<ObjectSync>().gameObject;
        if (obj != null)
        {
            ObjectsToGrab.Remove(obj);
        }
    }
}
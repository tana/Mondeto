using System;
using System.Collections.Generic;
using UnityEngine;

// FIXME: more flexible approach (e.g. using collider tag with shape setting and collision events)
public class GrabDetector : MonoBehaviour
{
    public readonly HashSet<GameObject> ObjectToGrab = new HashSet<GameObject>();

    SphereCollider col;

    public void Start()
    {
        // To detect collision with static colliders, kinematic Rigidbody is needed
        //  (See https://docs.unity3d.com/ja/2019.4/Manual/CollidersOverview.html )
        var rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        if (GetComponent<Collider>() != null) return;
        col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.3f;  // FIXME: setting
    }

    void OnTriggerEnter(Collider other)
    {
        // synchronized object only
        if (other.GetComponent<ObjectSync>() != null)
        {
            ObjectToGrab.Add(other.gameObject);
        }
    }

    void OnTriggerExit(Collider other)
    {
        ObjectToGrab.Remove(other.gameObject);
    }
}
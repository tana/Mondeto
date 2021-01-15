using System;
using System.Collections.Generic;
using UnityEngine;

// FIXME: more flexible approach (e.g. using collider tag with shape setting and collision events)
public class GrabDetector : MonoBehaviour
{
    public readonly HashSet<GameObject> ObjectsToGrab = new HashSet<GameObject>();
    
    GameObject display;
    SphereCollider col;
    MeshRenderer meshRenderer;
    float radius = 0.3f;

    public void Setup(SyncObject syncObj)
    {
        // To detect collision with static colliders, kinematic Rigidbody is needed
        //  (See https://docs.unity3d.com/ja/2019.4/Manual/CollidersOverview.html )
        // Therefore, the object have to be non-tangible.
        syncObj.SetField("isTangible", new Primitive<int>(0));

        if (GetComponent<ColliderSync>() == null) return;
        col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = radius;
        GetComponent<ColliderSync>().AddCollider(col);

        display = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(display.GetComponent<Collider>());  // display is just for display.
        display.transform.SetParent(transform, false);
        display.transform.localScale = Vector3.one * (2 * radius);  // scale = diameter = 2*radius
        meshRenderer = display.GetComponent<MeshRenderer>();
        meshRenderer.material = Resources.Load<Material>("Materials/HandColliderMaterial");
        meshRenderer.enabled = false;   // display is disabled by default
    }

    public bool Display
    {
        get => meshRenderer.enabled;
        set => meshRenderer.enabled = value;
    }

    public float Radius
    {
        get => radius;
        set
        {
            radius = value;
            col.radius = radius;
            display.transform.localScale = Vector3.one * (2 * radius);  // scale = diameter = 2*radius
        }
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
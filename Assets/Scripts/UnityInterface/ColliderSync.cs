using System.Collections.Generic;
using UnityEngine;

public class ColliderSync : MonoBehaviour, ITag
{
    // Because parameter setting did not work correctly with GetComponent<Collider> in ApplyState,
    // the collider is stored right after creating with AddComponent<XxxCollider> .
    List<Collider> addedColliders = new List<Collider>();

    PhysicMaterial material;

    SyncObject obj;

    bool isTangible = false;
    bool isStatic = false;

    public void Setup(SyncObject obj)
    {
        this.obj = obj;
        material = new PhysicMaterial();

        if (obj.HasTag("cube"))
        {
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.sharedMaterial = material;
            RegisterCollider(collider);
        }
        else if (obj.HasTag("sphere"))
        {
            var collider = gameObject.AddComponent<SphereCollider>();
            collider.sharedMaterial = material;
            RegisterCollider(collider);
        }
        else if (obj.HasTag("model"))
        {
            GetComponent<ModelSync>().LoadComplete += AddMeshCollidersForModel;
        }
        else if (obj.HasTag("plane") || obj.HasTag("cylinder"))
        {
            // For primitives that use MeshCollider
            var collider = gameObject.AddComponent<MeshCollider>();
            collider.sharedMaterial = material;
            RegisterCollider(collider);
        }
        else if (GetComponent<Collider>() != null)
        {
            // If the object already has a Unity Collider component (e.g. hands, player avatar)
            // Note:
            //  At this time, the default Collider created by CreatePrimitive may not destroyed.
            //  To ignore this collider, "GetComponent<Collider() != null" has to be the last of else-if.
            var collider = GetComponent<Collider>();
            collider.sharedMaterial = material;
            RegisterCollider(collider);
        }

        obj.RegisterFieldUpdateHandler("friction", HandleUpdate);
        obj.RegisterFieldUpdateHandler("restitution", HandleUpdate);
        obj.RegisterFieldUpdateHandler("isTangible", HandleUpdate);
        obj.RegisterFieldUpdateHandler("isStatic", HandleUpdate);

        HandleUpdate();
    }

    public void AddCollider(Collider collider)
    {
        collider.sharedMaterial = material;
        RegisterCollider(collider);
    }

    public void OnCollisionEnter(Collision collision)
    {
        SendEventToGameObject(collision.gameObject, "collisionStart", new IValue[0]);
    }

    public void OnCollisionExit(Collision collision)
    {
        SendEventToGameObject(collision.gameObject, "collisionEnd", new IValue[0]);
    }

    public void OnTriggerEnter(Collider other)
    {
        SendEventToGameObject(other.gameObject, "collisionStart", new IValue[0]);
    }

    public void OnTriggerExit(Collider other)
    {
        SendEventToGameObject(other.gameObject, "collisionEnd", new IValue[0]);
    }

    void SendEventToGameObject(GameObject recvGameObject, string eventName, IValue[] args)
    {
        var sync = GetComponent<ObjectSync>();

        // GetComponentInParent is used because a model can have Collider in child meshes.
        var recvSync = recvGameObject.GetComponentInParent<ObjectSync>();
        if (recvSync == null) return;

        recvSync.SyncObject.SendEvent(eventName, sync.SyncObject.Id, args);
    }

    // To support GLB loading (multiple meshes may be dynamically added as children)
    void AddMeshCollidersForModel(ModelSync ms)
    {
        foreach (var go in ms.GetMeshes())
        {
            if (go.GetComponent<Collider>() != null)
            {
                // Already have collider
                continue;
            }
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMaterial = material;
            addedColliders.Add(collider);
            RegisterCollider(collider);
        }
    }

    void RegisterCollider(Collider collider)
    {
        SetTrigger(collider);
        addedColliders.Add(collider);
    }

    void SetTrigger(Collider collider)
    {
        // FIXME: Non-convex MeshCollider cannot be a trigger collider
        // See: https://forum.unity.com/threads/how-to-enable-trigger-on-a-mesh-collider.347428/#post-2248431
        if (collider is MeshCollider meshCollider)
        {
            // FIXME:
            //meshCollider.convex = !isTangible;
            return;
        }
        collider.isTrigger = !isTangible;
    }

    void HandleUpdate()
    {
        // Uses same value for both static and dynamic friction coefficients.
        // This is for compatibility with engines other than Unity.
        // (It seems Unreal and Bullet does not support separate static/dynamic friction)
        // See:
        //  https://docs.unrealengine.com/en-US/Engine/Physics/PhysicalMaterials/Reference/index.html
        //  https://pybullet.org/Bullet/BulletFull/classbtCollisionObject.html
        //  https://pybullet.org/Bullet/phpBB3/viewtopic.php?t=9563

        // TODO: Field to specify how to combine friction/restitution coeffs of two objects.
        // Currently Unity's default setting (average?) is used.
        // See:
        //  https://docs.unity3d.com/2019.3/Documentation/ScriptReference/PhysicMaterialCombine.html
        // (Therefore, it is needed to set restitution of two objects larger than zero to make them actually bounce)

        if (obj.TryGetFieldPrimitive("friction", out float friction))
        {
            material.staticFriction = friction;
            material.dynamicFriction = friction;
        }

        if (obj.TryGetFieldPrimitive("restitution", out float restitution))
        {
            material.bounciness = restitution;
        }

        if (obj.TryGetFieldPrimitive("isTangible", out int isTangibleInt))
        {
            isTangible = isTangibleInt != 0;
            foreach (var collider in addedColliders)
            {
                SetTrigger(collider);
            }
        }

        /*
        if (obj.TryGetFieldPrimitive("isStatic", out int isStaticInt))
        {
            isStatic = isStaticInt != 0;
        }
        */
        // Currently, isStatic is disabled because it is not correctly working
        // (does not work for trigger and non-trigger collision?)
        isStatic = false;
        var rb = GetComponent<Rigidbody>();
        if (isStatic && rb != null && rb.isKinematic == true)  // static
        {
            Destroy(rb);
        }
        else if (!isStatic && rb == null)  // moving
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
        }
        // If a rigidbody is already attached (probably physics tag is present),
        // do not set isKinematic (not to disturb physics)
    }

    public void Cleanup(SyncObject obj)
    {
        Destroy(this);
    }

    public void OnDestroy()
    {
        obj.DeleteFieldUpdateHandler("friction", HandleUpdate);
        obj.DeleteFieldUpdateHandler("restitution", HandleUpdate);
        obj.DeleteFieldUpdateHandler("isTangible", HandleUpdate);
        obj.DeleteFieldUpdateHandler("isStatic", HandleUpdate);
    }
}
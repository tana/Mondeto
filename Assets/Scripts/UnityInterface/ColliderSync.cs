using System.Collections.Generic;
using UnityEngine;

public class ColliderSync : MonoBehaviour
{
    // Because parameter setting did not work correctly with GetComponent<Collider> in ApplyState,
    // the collider is stored right after creating with AddComponent<XxxCollider> .
    List<Collider> addedColliders = new List<Collider>();

    PhysicMaterial material;

    public void Initialize(SyncObject obj)
    {
        material = new PhysicMaterial();

        if (obj.HasTag("cube"))
        {
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.sharedMaterial = material;
            addedColliders.Add(collider);
        }
        else if (obj.HasTag("sphere"))
        {
            var collider = gameObject.AddComponent<SphereCollider>();
            collider.sharedMaterial = material;
            addedColliders.Add(collider);
        }
        else if (obj.HasTag("model"))
        {
            GetComponent<ModelSync>().LoadComplete += AddMeshCollidersForModel;
        }
        else
        {
            var collider = gameObject.AddComponent<MeshCollider>();
            collider.sharedMaterial = material;
            addedColliders.Add(collider);
        }

        obj.BeforeSync += OnBeforeSync;
        obj.AfterSync += OnAfterSync;

        ApplyState(obj);
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
        }
    }

    void ApplyState(SyncObject obj)
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

        if (obj.HasField("friction") && obj.GetField("friction") is Primitive<float> friction)
        {
            material.staticFriction = friction.Value;
            material.dynamicFriction = friction.Value;
        }

        if (obj.HasField("restitution") && obj.GetField("restitution") is Primitive<float> restitution)
        {
            material.bounciness = restitution.Value;
        }
    }

    void OnBeforeSync(SyncObject obj)
    {
        obj.SetField("friction", new Primitive<float>(material.dynamicFriction));
        obj.SetField("restitution", new Primitive<float>(material.bounciness));
    }

    void OnAfterSync(SyncObject obj)
    {
        if (GetComponent<ObjectSync>().IsOriginal) return;

        ApplyState(obj);
    }
}
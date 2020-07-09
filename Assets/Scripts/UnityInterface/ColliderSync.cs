using UnityEngine;

public class ColliderSync : MonoBehaviour
{
    // Because parameter setting did not work correctly with GetComponent<Collider> in ApplyState,
    // the collider is stored right after creating with AddComponent<XxxCollider> .
    Collider addedCollider;

    public void Initialize(SyncObject obj)
    {
        if (obj.HasTag("cube"))
        {
            addedCollider = gameObject.AddComponent<BoxCollider>();
        }
        else if (obj.HasTag("sphere"))
        {
            addedCollider = gameObject.AddComponent<SphereCollider>();
        }
        else    // FIXME:
        {
            addedCollider = gameObject.AddComponent<MeshCollider>();
        }

        obj.BeforeSync += OnBeforeSync;
        obj.AfterSync += OnAfterSync;

        ApplyState(obj);
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
            addedCollider.material.staticFriction = friction.Value;
            addedCollider.material.dynamicFriction = friction.Value;
        }

        if (obj.HasField("restitution") && obj.GetField("restitution") is Primitive<float> restitution)
        {
            addedCollider.material.bounciness = restitution.Value;
        }
    }

    void OnBeforeSync(SyncObject obj)
    {
        obj.SetField("friction", new Primitive<float>(addedCollider.material.dynamicFriction));
        obj.SetField("restitution", new Primitive<float>(addedCollider.material.bounciness));
    }

    void OnAfterSync(SyncObject obj)
    {
        if (GetComponent<ObjectSync>().IsOriginal) return;

        ApplyState(obj);
    }
}
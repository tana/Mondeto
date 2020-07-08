using UnityEngine;

public class ColliderSync : MonoBehaviour
{
    public void Initialize(SyncObject obj)
    {
        if (obj.HasTag("cube"))
        {
            gameObject.AddComponent<BoxCollider>();
        }
        else if (obj.HasTag("sphere"))
        {
            gameObject.AddComponent<SphereCollider>();
        }
        else    // FIXME:
        {
            gameObject.AddComponent<MeshCollider>();
        }

        obj.BeforeSync += OnBeforeSync;
        obj.AfterSync += OnAfterSync;

        ApplyState(obj);
    }

    void ApplyState(SyncObject obj)
    {
        var collider = GetComponent<Collider>();
        
        collider.material = new PhysicMaterial();

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

        Debug.Log(obj.Id);
        Debug.Log(obj.HasField("name") ? (obj.GetField("name") as Primitive<string>)?.Value : null);

        if (obj.HasField("name") && obj.GetField("name") is Primitive<string> name)
        {
            if (name.Value == "ball")
            {
                Debug.Log("BALL!!!!!");
                Debug.Log(collider);
            }
        }

        if (obj.HasField("friction") && obj.GetField("friction") is Primitive<float> friction)
        {
            Debug.Log("friction setting");
            collider.material.staticFriction = friction.Value;
            collider.material.dynamicFriction = friction.Value;
            Debug.Log("friction set");
        }

        Debug.Log("middle");

        if (obj.HasField("restitution") && obj.GetField("restitution") is Primitive<float> restitution)
        {
            Debug.Log("restitution setting ");
            collider.material.bounciness = restitution.Value;
            Debug.Log("restitution set");
        }

        Debug.Log(collider.material.bounciness);
    }

    void OnBeforeSync(SyncObject obj)
    {
        var collider = GetComponent<Collider>();

        obj.SetField("friction", new Primitive<float>(collider.material.dynamicFriction));
        obj.SetField("restitution", new Primitive<float>(collider.material.bounciness));
    }

    void OnAfterSync(SyncObject obj)
    {
        if (GetComponent<ObjectSync>().IsOriginal) return;

        ApplyState(obj);
    }
}
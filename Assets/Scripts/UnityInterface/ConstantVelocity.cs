using UnityEngine;

// Extrapolate (predict) position and rotation
// using assumptions that the object moves with constant velocity and angular velocity.
public class ConstantVelocity : MonoBehaviour
{
    private Vector3 velocity = Vector3.zero, angularVelocity = Vector3.zero;

    // For calculation of velocity and angular velocity
    private Vector3 lastPosition = Vector3.zero;
    private Quaternion lastRotation = Quaternion.identity;

    public void Initialize(SyncObject obj)
    {
        lastPosition = transform.position;
        lastRotation = transform.rotation;

        obj.BeforeSync += OnBeforeSync;
        obj.AfterSync += OnAfterSync;

        ApplyState(obj);
    }

    public void FixedUpdate()
    {
        SyncObject obj = GetComponent<ObjectSync>()?.SyncObject;
        if (obj == null) return;

        if (GetComponent<ObjectSync>().IsOriginal)
        {
            velocity = (transform.position - lastPosition) / Time.fixedDeltaTime;
            angularVelocity = Mathf.Deg2Rad * (Quaternion.Inverse(lastRotation) * transform.rotation).eulerAngles / Time.fixedDeltaTime;
            lastPosition = transform.position;
            lastRotation = transform.rotation;
        }
        else
        {
            transform.position += velocity * Time.fixedDeltaTime;
            transform.rotation *= Quaternion.Euler(Mathf.Rad2Deg * angularVelocity * Time.fixedDeltaTime);
        }
    }

    void ApplyState(SyncObject obj)
    {
        if (obj.TryGetField("velocity", out Vec localVelocityVec))
        {
            velocity = transform.TransformVector(UnityUtil.FromVec(localVelocityVec));
        }
        if (obj.TryGetField("angularVelocity", out Vec localAngularVelocityVec))
        {
            angularVelocity = transform.TransformVector(UnityUtil.FromVec(localAngularVelocityVec));
        }
    }

    void OnBeforeSync(SyncObject obj)
    {
        obj.SetField("velocity", UnityUtil.ToVec(transform.InverseTransformVector(velocity)));
        obj.SetField("angularVelocity", UnityUtil.ToVec(transform.InverseTransformVector(angularVelocity)));
    }

    void OnAfterSync(SyncObject obj)
    {
        if (GetComponent<ObjectSync>().IsOriginal) return;

        ApplyState(obj);
    }
}
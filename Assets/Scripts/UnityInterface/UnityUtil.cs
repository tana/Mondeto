using UnityEngine;

public class UnityUtil
{
    public static Vec ToVec(Vector3 v)
    {
        return new Vec { X = v.x, Y = v.y, Z = v.z };
    }

    public static Vector3 FromVec(Vec v)
    {
        return new Vector3(v.X, v.Y, v.Z);
    }

    public static Quat ToQuat(Quaternion q)
    {
        return new Quat { W = q.w, X = q.x, Y = q.y, Z = q.z };
    }

    public static Quaternion FromQuat(Quat q)
    {
        return new Quaternion(q.X, q.Y, q.Z, q.W);
    }
}
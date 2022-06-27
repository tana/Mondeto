using System.Linq;
using UnityEngine;
using Mondeto.Core;

namespace Mondeto
{

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

    public static Vector3[] VecSequenceToUnity(Sequence seq)
    {
        return seq.Elements.Select(
            elem => (elem is Vec vec) ? UnityUtil.FromVec(vec) : Vector3.zero
        ).ToArray();
    }

    public static int[] IntSequenceToUnity(Sequence seq)
    {
        return seq.Elements.Select(
            elem => (elem is Primitive<int> idx) ? idx.Value : 0
        ).ToArray();
    }
}

}   // end namespace
using NUnit.Framework;
using UnityEngine;

// Compare results of our own quaternion math with Unity's functions
[TestFixture]
class MathTests
{
    [Test]
    public void QuatMulTest()
    {
        Quat qOurs = (new Quat(w: 1, x: 2, y: 3, z: 4)) * (new Quat(w: 5, x: 6, y: 7, z: 8));
        Quaternion qUnity = (new Quaternion(w: 1, x: 2, y: 3, z: 4)) * (new Quaternion(w: 5, x: 6, y: 7, z: 8));
        TestUtils.AssertQuat(qOurs, qUnity.w, qUnity.x, qUnity.y, qUnity.z);
    }

    [Test]
    public void AngleAxisTest()
    {
        Quat qOurs = Quat.FromAngleAxis(34 * Mathf.Deg2Rad, new Vec(1, 2, 3).Normalize());
        Quaternion qUnity = Quaternion.AngleAxis(34, new Vector3(1, 2, 3).normalized);
        TestUtils.AssertQuat(qOurs, qUnity.w, qUnity.x, qUnity.y, qUnity.z);
    }

    [Test]
    public void EulerTest()
    {
        Quat qOurs = Quat.FromEuler(12 * Mathf.Deg2Rad, 97 * Mathf.Deg2Rad, 215 * Mathf.Deg2Rad);
        Quaternion qUnity = Quaternion.Euler(12, 97, 215);
        TestUtils.AssertQuat(qOurs, qUnity.w, qUnity.x, qUnity.y, qUnity.z);
    }
}
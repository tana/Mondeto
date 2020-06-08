using System;
using System.IO;
using NUnit.Framework;

[TestFixture]
class SceneLoaderTests
{
    const string simpleYaml = @"
    objects:
        -
            position: !vec [0, 0, 0]
            rotation: !quat [1, 0, 0, 0]
            tags: [primitive]
            primitive: plane
    ";

    // Use some tolerance for floating-point comparison
    // See https://github.com/nunit/docs/wiki/Assert.AreEqual
    float tol = 1e-4f;

    [Test]
    public void SimpleLoadTest()
    {
        var node = new DummyNode();
        var loader = new SceneLoader(node);
        loader.Load(new StringReader(simpleYaml)).Wait();

        SyncObject obj = node.Objects[0];
        AssertVec(obj.GetField("position"), 0, 0, 0);
        AssertQuat(obj.GetField("rotation"), 1, 0, 0, 0);
        //Assert.That(()obj.GetField("tag"), );
        AssertPrimitive(obj.GetField("primitive"), "plane");
    }

    private void AssertVec(IValue val, float x, float y, float z)
    {
        Assert.That(val, Is.TypeOf<Vec>());
        if (val is Vec vec)
        {
            Assert.AreEqual(vec.X, x, tol);
            Assert.AreEqual(vec.Y, y, tol);
            Assert.AreEqual(vec.Z, z, tol);
        }
    }

    private void AssertQuat(IValue val, float w, float x, float y, float z)
    {
        Assert.That(val, Is.TypeOf<Quat>());
        if (val is Quat quat)
        {
            Assert.AreEqual(quat.W, w, tol);
            Assert.AreEqual(quat.X, x, tol);
            Assert.AreEqual(quat.Y, y, tol);
            Assert.AreEqual(quat.Z, z, tol);
        }
    }

    private void AssertPrimitive<T>(IValue val, float innerVal)
    {
        Assert.That(val, Is.TypeOf<Primitive<float>>());
        if (val is Primitive<float> primitive)
        {
            Assert.AreEqual(primitive.Value, innerVal, tol);
        }
    }

    private void AssertPrimitive(IValue val, double innerVal)
    {
        Assert.That(val, Is.TypeOf<Primitive<double>>());
        if (val is Primitive<double> primitive)
        {
            Assert.AreEqual(primitive.Value, innerVal, tol);
        }
    }

    private void AssertPrimitive<T>(IValue val, T innerVal)
    {
        Assert.That(val, Is.TypeOf<Primitive<T>>());
        if (val is Primitive<T> primitive)
        {
            Assert.AreEqual(primitive.Value, innerVal);
        }
    }
}
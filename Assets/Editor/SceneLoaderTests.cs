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
            tags: [primitive, foo_bar]
            primitive: plane
            foo: 123
            bar: 3.14
    ";

    // Use some tolerance for floating-point comparison
    // See https://github.com/nunit/docs/wiki/Assert.AreEqual
    // Now we use Is.Equals(x).Within(tol) instead of AreEqual
    // (See https://github.com/nunit/docs/wiki/EqualConstraint#comparing-numerics )
    // This shift was because AreEqual(a, b) treated a as expected result and b as actual value.
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
        Assert.That(((Sequence)obj.GetField("tags")).Elements, Is.EqualTo(new IValue[] {
            new Primitive<string> { Value = "primitive" },
            new Primitive<string> { Value = "foo_bar" }
        }));
        Assert.AreEqual(obj.GetField("primitive"), new Primitive<string> { Value = "plane" });
        AssertPrimitive<int>(obj.GetField("foo"), 123);
        AssertPrimitive(obj.GetField("bar"), 3.14f);
    }

    private void AssertVec(IValue val, float x, float y, float z)
    {
        Assert.That(val, Is.TypeOf<Vec>());
        if (val is Vec vec)
        {
            Assert.That(vec.X, Is.EqualTo(x).Within(tol));
            Assert.That(vec.Y, Is.EqualTo(y).Within(tol));
            Assert.That(vec.Z, Is.EqualTo(z).Within(tol));
        }
    }

    private void AssertQuat(IValue val, float w, float x, float y, float z)
    {
        Assert.That(val, Is.TypeOf<Quat>());
        if (val is Quat quat)
        {
            Assert.That(quat.W, Is.EqualTo(w).Within(tol));
            Assert.That(quat.X, Is.EqualTo(x).Within(tol));
            Assert.That(quat.Y, Is.EqualTo(y).Within(tol));
            Assert.That(quat.Z, Is.EqualTo(z).Within(tol));
        }
    }

    private void AssertPrimitive(IValue val, float innerVal)
    {
        Assert.That(val, Is.TypeOf<Primitive<float>>());
        if (val is Primitive<float> primitive)
        {
            Assert.That(primitive.Value, Is.EqualTo(innerVal).Within(tol));
        }
    }

    private void AssertPrimitive(IValue val, double innerVal)
    {
        Assert.That(val, Is.TypeOf<Primitive<double>>());
        if (val is Primitive<double> primitive)
        {
            Assert.That(primitive.Value, Is.EqualTo(innerVal).Within(tol));
        }
    }

    private void AssertPrimitive<T>(IValue val, T innerVal)
    {
        Assert.That(val, Is.TypeOf<Primitive<T>>());
        if (val is Primitive<T> primitive)
        {
            Assert.That(primitive.Value, Is.EqualTo(innerVal));
        }
    }
}
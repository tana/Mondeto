using NUnit.Framework;

class TestUtils
{
    // Use some tolerance for floating-point comparison
    // See https://github.com/nunit/docs/wiki/Assert.AreEqual
    // Now we use Is.Equals(x).Within(tol) instead of AreEqual
    // (See https://github.com/nunit/docs/wiki/EqualConstraint#comparing-numerics )
    // This shift was because AreEqual(a, b) treated a as expected result and b as actual value.
    const float tol = 1e-4f;

    public static void AssertVec(IValue val, float x, float y, float z)
    {
        Assert.That(val, Is.TypeOf<Vec>());
        if (val is Vec vec)
        {
            Assert.That(vec.X, Is.EqualTo(x).Within(tol));
            Assert.That(vec.Y, Is.EqualTo(y).Within(tol));
            Assert.That(vec.Z, Is.EqualTo(z).Within(tol));
        }
    }

    public static void AssertQuat(IValue val, float w, float x, float y, float z)
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

    public static void AssertPrimitive(IValue val, float innerVal)
    {
        Assert.That(val, Is.TypeOf<Primitive<float>>());
        if (val is Primitive<float> primitive)
        {
            Assert.That(primitive.Value, Is.EqualTo(innerVal).Within(tol));
        }
    }

    public static void AssertPrimitive(IValue val, double innerVal)
    {
        Assert.That(val, Is.TypeOf<Primitive<double>>());
        if (val is Primitive<double> primitive)
        {
            Assert.That(primitive.Value, Is.EqualTo(innerVal).Within(tol));
        }
    }

    public static void AssertPrimitive<T>(IValue val, T innerVal)
    {
        Assert.That(val, Is.TypeOf<Primitive<T>>());
        if (val is Primitive<T> primitive)
        {
            Assert.That(primitive.Value, Is.EqualTo(innerVal));
        }
    }
}
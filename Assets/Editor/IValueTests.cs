using NUnit.Framework;

[TestFixture]
class IValueTests
{
    [Test]
    public void PrimitiveEqualityTest()
    {
        IValue intA = new Primitive<int> { Value = 1 };
        IValue intB = new Primitive<int> { Value = 1 };
        Assert.That(intA.Equals(intB));
    }
}
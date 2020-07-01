using NUnit.Framework;
using System.Collections.Generic;
using MessagePack;

[TestFixture]
class IValueTests
{
    [Test]
    public void PrimitiveEqualityTest()
    {
        IValue intA = new Primitive<int> { Value = 1 };
        IValue intB = new Primitive<int> { Value = 1 };
        Assert.That(intA.Equals(intB));

        IValue strA = new Primitive<string> { Value = "foo" };
        IValue strB = new Primitive<string> { Value = "foo" };
        Assert.That(strA.Equals(strB));
    }

    [Test]
    public void SequenceEqualityTest()
    {
        Sequence seqA = new Sequence {
            Elements = new List<IValue> {
                new Primitive<string> { Value = "aaa" },
                new Primitive<string> { Value = "bbbb" },
                new Primitive<string> { Value = "ccccc" },
            }
        };
        Sequence seqB = new Sequence {
            Elements = new List<IValue> {
                new Primitive<string> { Value = "aaa" },
                new Primitive<string> { Value = "bbbb" },
                new Primitive<string> { Value = "ccccc" },
            }
        };
        Assert.That(seqA.Equals(seqB));
    }

    [Test]
    public void EncodeDecodeTest()
    {
        IValue data = new Sequence {
            Elements = new List<IValue> {
                new Primitive<string> { Value = "aaa" },
                new Primitive<string> { Value = "bbbb" },
                new Primitive<int> { Value = 123 },
            }
        };
        IValue decoded = MessagePackSerializer.Deserialize<IValue>(MessagePackSerializer.Serialize<IValue>(data));
        Assert.That(decoded, Is.TypeOf(typeof(Sequence)));
        Assert.That(((Sequence)decoded).Equals((Sequence)data));
    }
}
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
            tags: [plane, foo_bar]
            test: testtest
            foo: 123
            bar: 3.14
    ";

    const string eulerYaml = @"
    objects:
        -
            position: !vec [0, 1, 2]
            rotation: !euler [0, 0, 0]
            tags: [cube]
    ";

    [Test]
    public void SimpleLoadTest()
    {
        var node = new DummyNode();
        var loader = new SceneLoader(node);
        loader.Load(new StringReader(simpleYaml)).Wait();

        SyncObject obj = node.Objects[0];
        TestUtils.AssertVec(obj.GetField("position"), 0, 0, 0);
        TestUtils.AssertQuat(obj.GetField("rotation"), 1, 0, 0, 0);
        Assert.That(((Sequence)obj.GetField("tags")).Elements, Is.EqualTo(new IValue[] {
            new Primitive<string> { Value = "plane" },
            new Primitive<string> { Value = "foo_bar" }
        }));
        Assert.AreEqual(obj.GetField("test"), new Primitive<string> { Value = "testtest" });
        TestUtils.AssertPrimitive<int>(obj.GetField("foo"), 123);
        TestUtils.AssertPrimitive(obj.GetField("bar"), 3.14f);
    }

    [Test]
    public void EulerTest()
    {
        var node = new DummyNode();
        var loader = new SceneLoader(node);
        loader.Load(new StringReader(eulerYaml)).Wait();

        SyncObject obj = node.Objects[0];
        TestUtils.AssertQuat(obj.GetField("rotation"), 1, 0, 0, 0);
    }
}
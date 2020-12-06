using System;
using System.IO;
using System.Linq;
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

    const string blobYaml = @"
    objects:
        -
            position: !vec [0, 1, 2]
            rotation: !euler [0, 0, 0]
            tags: [plane]
            blobtest: !load_file 'Assets/Editor/test.txt' # Single quotation is used because this is inside C# double-quoted string
    ";

    const string loadedFile = "Assets/Editor/test.txt";

    const string objectRefYaml = @"
    objects:
        -
            $name: theCube
            position: !vec [0, 0, 0]
            rotation: !euler [0, 0, 0]
            tags: [cube]
        -
            position: !vec [0, 0, 0]
            rotation: !euler [0, 0, 0]
            reftest: !ref theCube
            tags: [plane, abcdef]
        -
            $name: foofoofoo
            position: !vec [3, 4, 5]
            rotation: !euler [0, 0, 0]
            tags: [sphere]
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

    [Test]
    public void BlobTest()
    {
        var node = new DummyNode();
        var loader = new SceneLoader(node);
        loader.Load(new StringReader(blobYaml), "blobtest.yml").Wait();

        SyncObject obj = node.Objects[0];
        var task = node.ReadBlob((BlobHandle)obj.GetField("blobtest"));
        task.Wait();
        Blob blob = task.Result;

        byte[] data = File.ReadAllBytes(loadedFile);

        Assert.That(blob.Data.SequenceEqual(data));
    }

    [Test]
    public void ObjectRefTest()
    {
        var node = new DummyNode();
        var loader = new SceneLoader(node);
        loader.Load(new StringReader(objectRefYaml)).Wait();
        
        SyncObject theCube = node.Objects[0];
        SyncObject secondObj = node.Objects[1];
        
        Assert.That(((ObjectRef)secondObj.GetField("reftest")).Id, Is.EqualTo(theCube.Id));
    }

    [Test]
    public void ValueConversionTest()
    {
        var node = new DummyNode();
        var loader = new SceneLoader(node);

        TestUtils.AssertPrimitive(loader.YamlToValue(SceneLoader.ValueToYamlString(new Primitive<int>(123))), 123);
        TestUtils.AssertPrimitive(loader.YamlToValue(SceneLoader.ValueToYamlString(new Primitive<float>(3.14f))), 3.14f);
        TestUtils.AssertPrimitive(loader.YamlToValue(SceneLoader.ValueToYamlString(new Primitive<string>("test"))), "test");
        TestUtils.AssertVec(loader.YamlToValue(SceneLoader.ValueToYamlString(new Vec(1, 2, 3))), 1, 2, 3);
        TestUtils.AssertQuat(loader.YamlToValue(SceneLoader.ValueToYamlString(new Quat(1, 0, 0, 0))), 1, 0, 0, 0);
        // TODO: more tests
    }
}
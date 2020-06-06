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
            rotation: !quat [1, 0, 0]
            tags: [primitive]
            primitive: plane
    ";

    [Test]
    public void SimpleLoadTest()
    {
        var node = new DummyNode();
        var loader = new SceneLoader(new StringReader(simpleYaml));
        loader.Load(node);
    }
}
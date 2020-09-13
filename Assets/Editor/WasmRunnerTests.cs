using System;
using System.IO;
using NUnit.Framework;

[TestFixture]
class WasmRunnerTests
{
    const string wasmDir = "wasm/";

    DummyNode node;
    SyncObject obj;

    [SetUp]
    public void SetUp()
    {
        node = new DummyNode();
        obj = node.Objects[node.CreateObject().Result];
    }

    [Test]
    public void WasmInitTest()
    {
        var runner = new WasmRunner(obj);
        runner.Load(File.ReadAllBytes(wasmDir + "test.wasm"));
        runner.Initialize();
        Assert.That(runner.IsReady);
    }
}
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
        using (var runner = new ObjectWasmRunner(obj))
        {
            runner.Load(File.ReadAllBytes(wasmDir + "test.wasm"));
            runner.Initialize();
            Assert.That(runner.IsReady);
        }
    }

    // Use Timeout attribute to check if infinite loops are really prevented
    // See: https://docs.nunit.org/articles/nunit/writing-tests/attributes/timeout.html
    // FIXME: Currently Timeout is not working (maybe because the infinite loop is in unmanaged code?)
    //        If this test fails, Unity Editor freezes.
    [Test, Timeout(20)]
    public void TimeLimitTest()
    {
        using (var runner = new WasmRunner())
        {
            runner.Load(File.ReadAllBytes(wasmDir + "infinite_loop_test.wasm"));
            runner.Initialize();
        }
    }
}
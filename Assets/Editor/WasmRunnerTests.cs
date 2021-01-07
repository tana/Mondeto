using System;
using System.IO;
using NUnit.Framework;

[TestFixture]
class WasmRunnerTests
{
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
            runner.Load(File.ReadAllBytes("Assets/StreamingAssets/wasm/raygun.wasm"));
            runner.Initialize();
            Assert.That(runner.IsReady);
        }
    }

    // Use Timeout attribute to check if infinite loops are really prevented
    // See: https://docs.nunit.org/articles/nunit/writing-tests/attributes/timeout.html
    // FIXME: Timeout does not work.
    //        If WasmRunner fails to stop an infinite loop , Unity Editor freezes.
    //        See: https://issuetracker.unity3d.com/issues/testrunner-nunit-tests-doesnt-time-out-when-timeout-attribute-is-set
    [Test, Timeout(20)]
    public void TimeLimitTest()
    {
        using (var runner = new WasmRunner())
        {
            runner.Load(File.ReadAllBytes("Assets/Editor/infinite_loop_test.wasm"));
            Assert.Throws<System.Reflection.TargetInvocationException>(() => runner.Initialize());
        }
    }
}
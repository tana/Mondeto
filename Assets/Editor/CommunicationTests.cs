using NUnit.Framework;
using System;
using System.Net;
using System.Threading.Tasks;
using Mondeto.Core;

[TestFixture]
class CommunicationTests
{
    const int Port = 15902;

    [SetUp]
    public void SetUp()
    {
        Logger.OnLog += WriteLog;
    }

    [TearDown]
    public void TearDown()
    {
        Logger.OnLog -= WriteLog;
    }

    void WriteLog(Logger.LogType type, string component, string msg)
    {
        UnityEngine.Debug.Log($"[{Logger.LogTypeToString(type)}] {component}: {msg}");
    }

    [Test]
    public void ServerInitTest()
    {
        Task.Run(async () => {
            using var server = new SyncServer(
                new IPEndPoint(IPAddress.Any, Port),
                @"Assets/Editor/testCert/test.key", @"Assets/Editor/testCert/test.crt"
            );
            await server.Initialize();
        }).Wait();
    }

    [Test]
    public void ConnectionTest()
    {
        // FIXME: Fail to connect (ALPN negotiation error) when server uses IPAddress.Loopback or client uses localhost. Probably related to IPv6.
        var serverTask = Task.Run(async () => {
            using var server = new SyncServer(
                new IPEndPoint(IPAddress.Any, Port),
                @"Assets/Editor/testCert/test.key", @"Assets/Editor/testCert/test.crt"
            );
            await server.Initialize();

            await Task.Delay(10000);
        });

        var clientTask = Task.Run(async () => {
            await Task.Delay(1000);

            using var client = new SyncClient("127.0.0.1", Port, noCertValidation: true);
            await client.Initialize();

            Assert.That(client.NodeId, Is.EqualTo(1));
        });

        Task.WaitAll(serverTask, clientTask);
    }
}
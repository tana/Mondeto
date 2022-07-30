using NUnit.Framework;
using System;
using System.Text;
using System.Security.Cryptography;
using System.Net;
using System.Threading.Tasks;
using Mondeto.Core;

[TestFixture]
class CommunicationTests
{
    const int Port = 15902;

    //string keyLogFile = UnityEngine.Application.temporaryCachePath + System.IO.Path.DirectorySeparatorChar + "key_log.txt";
    string keyLogFile = ""; // No key logging

    SyncServer server;
    SyncClient client;

    [SetUp]
    public void SetUp()
    {
        Logger.OnLog += WriteLog;

        // FIXME: Fail to connect (ALPN negotiation error) when server uses IPAddress.Loopback or client uses localhost. Probably related to IPv6.
        server = new SyncServer(
            new IPEndPoint(IPAddress.Any, Port),
            UnityEngine.Application.streamingAssetsPath + "/testCert/test.key",
            UnityEngine.Application.streamingAssetsPath + "/testCert/test.crt"
        );
        client = new SyncClient(
            "127.0.0.1", Port,
            noCertValidation: true,
            keyLogFile: keyLogFile
        );
    }

    [TearDown]
    public void TearDown()
    {
        server.Dispose();
        client.Dispose();

        Logger.OnLog -= WriteLog;
    }

    void WriteLog(Logger.LogType type, string component, string msg)
    {
        UnityEngine.Debug.Log($"[{Logger.LogTypeToString(type)}] {component}: {msg}");
    }

    [Test]
    public void ConnectionTest()
    {
        Task.Run(async () => {
            await server.Initialize();
            await client.Initialize();

            Assert.That(client.NodeId, Is.EqualTo(1));
        }).Wait();
    }

    [Test]
    public void BlobTransferTest()
    {
        Task.Run(async () => {
            await server.Initialize();
            await client.Initialize();

            // Server to client
            Blob serverBlob = MakeRandomBlob();
            BlobHandle serverBlobHandle = serverBlob.GenerateHandle();
            server.WriteBlob(serverBlobHandle, serverBlob);

            Blob receivedServerBlob = await client.ReadBlob(serverBlobHandle);
            Assert.That(receivedServerBlob.Data, Is.EqualTo(serverBlob.Data));  // Compared element-wise. See: https://docs.nunit.org/articles/nunit/writing-tests/constraints/EqualConstraint.html#comparing-arrays-collections-and-ienumerables

            // Client to server
            Blob clientBlob = MakeRandomBlob();
            BlobHandle clientBlobHandle = clientBlob.GenerateHandle();
            client.WriteBlob(clientBlobHandle, clientBlob);

            Blob receivedClientBlob = await server.ReadBlob(clientBlobHandle);
            Assert.That(receivedClientBlob.Data, Is.EqualTo(clientBlob.Data));  // Compared element-wise. See: https://docs.nunit.org/articles/nunit/writing-tests/constraints/EqualConstraint.html#comparing-arrays-collections-and-ienumerables
        }).Wait();
    }

    [Test]
    public void SyncTest()
    {
        var position = new Vec(1.0f, 2.0f, 3.0f);

        Task.Run(async () => {
            await server.Initialize();
            await client.Initialize();

            var createObjectTask = client.CreateObject();

            uint? objId = null;
            for (int i = 0; i < 10; i++)
            {
                if (createObjectTask.IsCompletedSuccessfully)
                {
                    objId = createObjectTask.Result;
                    client.Objects[objId.Value].SetField("position", position);
                }
                server.SyncFrame(0.2f);
                client.SyncFrame(0.2f);
                await Task.Delay(20);
            }

            TestUtils.AssertVec(server.Objects[objId.Value].GetField("position"), position.X, position.Y, position.Z);
        }).Wait();
    }

    Blob MakeRandomBlob()
    {
        // Generate random base64 file using a secure RNG
        byte[] binary = new byte[256];
        RandomNumberGenerator.Fill(binary);
        string base64Str = Convert.ToBase64String(binary);
        byte[] base64 = Encoding.ASCII.GetBytes(base64Str);

        return new Blob(base64, "text/plain");
    }
}
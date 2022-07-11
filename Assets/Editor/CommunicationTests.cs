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
        Task.Run(async () => {
            using var server = new SyncServer(
                new IPEndPoint(IPAddress.Any, Port),
                @"Assets/Editor/testCert/test.key", @"Assets/Editor/testCert/test.crt"
            );
            await server.Initialize();

            using var client = new SyncClient(
                "127.0.0.1", Port,
                noCertValidation: true,
                keyLogFile: keyLogFile
            );
            await client.Initialize();

            Assert.That(client.NodeId, Is.EqualTo(1));
        }).Wait();
    }

    [Test]
    public void BlobTransferTest()
    {
        // FIXME: Fail to connect (ALPN negotiation error) when server uses IPAddress.Loopback or client uses localhost. Probably related to IPv6.
        Task.Run(async () => {
            using var server = new SyncServer(
                new IPEndPoint(IPAddress.Any, Port),
                @"Assets/Editor/testCert/test.key", @"Assets/Editor/testCert/test.crt"
            );
            await server.Initialize();

            using var client = new SyncClient(
                "127.0.0.1", Port,
                noCertValidation: true,
                keyLogFile: keyLogFile
            );
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
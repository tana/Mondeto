using NUnit.Framework;
using System;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Mondeto.Core;

[TestFixture]
class ConnectionTests
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

    [Test]
    public void PasswordAuthSuccessTest()
    {
        var sha = SHA256.Create();

        using SyncServer server = new SyncServer(
            new IPEndPoint(IPAddress.Any, Port),
            UnityEngine.Application.streamingAssetsPath + "/testCert/test.key",
            UnityEngine.Application.streamingAssetsPath + "/testCert/test.crt",
            AuthType.Password,
            new Dictionary<string, byte[]>(
                new[] {
                    KeyValuePair.Create("user", sha.ComputeHash(Encoding.UTF8.GetBytes("pass")))
                }
            )
        );

        using SyncClient client = new SyncClient(
            "127.0.0.1", Port,
            noCertValidation: true,
            keyLogFile: keyLogFile
        );
        client.PasswordRequestCallback = async () => ("user", "pass");

        Task.Run(async () => {
            await server.Initialize();
            await client.Initialize();
        }).Wait();
    }

    [Test]
    public void PasswordAuthFailureTest()
    {
        var sha = SHA256.Create();

        using SyncServer server = new SyncServer(
            new IPEndPoint(IPAddress.Any, Port),
            UnityEngine.Application.streamingAssetsPath + "/testCert/test.key",
            UnityEngine.Application.streamingAssetsPath + "/testCert/test.crt",
            AuthType.Password,
            new Dictionary<string, byte[]>(
                new[] {
                    KeyValuePair.Create("user", sha.ComputeHash(Encoding.UTF8.GetBytes("pass")))
                }
            )
        );

        using SyncClient client = new SyncClient(
            "127.0.0.1", Port,
            noCertValidation: true,
            keyLogFile: keyLogFile
        );
        client.PasswordRequestCallback = async () => ("user", "wrongpass");

        // TODO: more specific exception
        Assert.Throws(Is.InstanceOf<Exception>(), () => Task.Run(async () => {
            await server.Initialize();
            await client.Initialize();
        }).Wait());
    }

    void WriteLog(Logger.LogType type, string component, string msg)
    {
        UnityEngine.Debug.Log($"[{Logger.LogTypeToString(type)}] {component}: {msg}");
    }
}
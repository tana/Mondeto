using NUnit.Framework;
using System.Net;
using System.Threading.Tasks;
using Mondeto.Core;

[TestFixture]
class CommunicationTests
{
    const int Port = 15902;

    [Test]
    public void ServerInitTest()
    {
        Task.Run(async () => {
            using var server = new SyncServer(
                new IPEndPoint(IPAddress.Loopback, Port),
                @"Assets/Editor/testCert/test.key", @"Assets/Editor/testCert/test.crt"
            );
            await server.Initialize();
        }).Wait();
    }

    [Test]
    public void ConnectionTest()
    {
        Task.Run(async () => {
            using var server = new SyncServer(
                new IPEndPoint(IPAddress.Loopback, Port),
                @"Assets/Editor/testCert/test.key", @"Assets/Editor/testCert/test.crt"
            );
            await server.Initialize();

            using var client = new SyncClient("localhost", Port);
            await client.Initialize();
        }).Wait();
    }
}
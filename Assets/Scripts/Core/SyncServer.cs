using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using Mondeto.Core.QuicWrapper;

namespace Mondeto.Core
{

public class SyncServer : SyncNode
{
    Dictionary<uint, Connection> clients = new Dictionary<uint, Connection>();
    protected override Dictionary<uint, Connection> Connections { get => clients; }

    public override uint NodeId { get; protected set; } = ServerNodeId; // Node ID for server is always 0

    IdRegistry nodeIdRegistry = new(ServerNodeId + 1);    // ServerNodeId (=0) is not assigned for clients

    IdRegistry objectIdRegistry = new IdRegistry(WorldObjectId + 1);

    IdRegistry symbolIdRegistry = new IdRegistry(0);

    Runner<uint> runner = new Runner<uint>();

    QuicListener listener;

    IPEndPoint endPoint;
    string privateKeyPath;
    string certificatePath;

    public SyncServer(IPEndPoint endPoint, string privateKeyPath, string certificatePath)
        : base()
    {
        this.endPoint = endPoint;
        this.privateKeyPath = privateKeyPath;
        this.certificatePath = certificatePath;
    }

    public override async Task Initialize()
    {
        // Create World object
        Objects[WorldObjectId] = new SyncObject(this, WorldObjectId, ServerNodeId);
        InvokeObjectCreated(WorldObjectId); // To invoke ObjectCreated event for World Object (ID=0)

        listener = new QuicListener();

        listener.ClientConnected += async (quicConnection, ep) => {
            // Create connection for a new client
            var conn = new Connection(quicConnection);

            uint clientNodeId = nodeIdRegistry.Create();  // Assign a new node ID
            Logger.Log("Server", $"Accepting connection from {ep} as NodeId {clientNodeId}");
            await conn.SetupServerAsync();

            await InitClient(conn, clientNodeId);
        };

        // Start accepting client connection
        listener.Start(new byte[][] { SyncNode.Alpn }, endPoint, privateKeyPath, certificatePath);
    }

    async Task InitClient(Connection conn, uint clientId)
    {
        lock (clients)
        {
            clients[clientId] = conn;
        }

        Logger.Log("Server", $"Registered client NodeId={clientId}");

        // Tell node id to the client
        conn.SendControlMessage(new NodeIdMessage { NodeId = clientId });

        // Send existing objects
        foreach (var pair in Objects)
        {
            var id = pair.Key;
            var obj = pair.Value;
            IControlMessage msg = new ObjectCreatedMessage { ObjectId = id, OriginalNodeId = obj.OriginalNodeId };
            conn.SendControlMessage(msg);
        }

        var cancelSource = new CancellationTokenSource();
        conn.OnDisconnect += () => cancelSource.Cancel();
        var _ = ProcessBlobMessagesAsync(clientId, conn, cancelSource.Token);
    }

    protected override void ProcessControlMessages()
    {
        // Filter out closed client connections
        var closedClients = clients.Where(pair => !pair.Value.Connected).Select(pair => pair.Key).ToList();
        foreach (uint id in closedClients)
        {
            clients.Remove(id);
            Logger.Log("Server", $"Client id={id} disconnected");
            // Delete objects which is original in disconnected clients
            var objectsToDelete = Objects.Where(pair => pair.Value.OriginalNodeId == id).Select(pair => pair.Key).ToList();
            foreach (var oid in objectsToDelete)
            {
                DeleteObjectAndNotify(oid, id);
            }
        }

        runner.Run();

        foreach (var pair in clients)
        {
            var id = pair.Key;
            var conn = pair.Value;

            IControlMessage msg;
            while (conn.TryReceiveControlMessage(out msg))
            {
                if (msg is CreateObjectMessage createObjectMessage)
                {
                    CreateObjectAndNotify(id);
                }
                else if (msg is DeleteObjectMessage deleteObjectMessage)
                {
                    DeleteObjectAndNotify(deleteObjectMessage.ObjectId, id);
                }
                else if (msg is EventSentMessage eventSentMessage)
                {
                    // Broadcast to other clients
                    BroadcastEventSentMessage(id, eventSentMessage);

                    HandleEventSentMessage(
                        eventSentMessage.Name,
                        eventSentMessage.Sender, eventSentMessage.Receiver,
                        eventSentMessage.Args
                    );
                }
            }
        }
    }

    void BroadcastEventSentMessage(uint fromClientId, EventSentMessage msg)
    {
        foreach (var pair in clients)
        {
            uint id = pair.Key;
            Connection conn = pair.Value;

            if (id == fromClientId) continue;   // Don't send back to the client that sent this msg
            
            conn.SendControlMessage(msg);
        }
    }

    uint CreateObjectAndNotify(uint originalNodeId)
    {
        uint id = objectIdRegistry.Create();
        var obj = new SyncObject(this, id, originalNodeId);
        Objects[id] = obj;

        // Notify object creation to all clients
        IControlMessage msg = new ObjectCreatedMessage { ObjectId = id, OriginalNodeId = originalNodeId };
        SendToAllClients(msg);

        Logger.Debug("Server", $"Created ObjectId={id}");

        InvokeObjectCreated(id);

        return id;
    }

    void DeleteObjectAndNotify(uint id, uint nodeId)
    {
        if (!Objects.ContainsKey(id)) return;
        if (Objects[id].OriginalNodeId != nodeId) return;

        Objects.Remove(id);
        
        IControlMessage msg = new ObjectDeletedMessage { ObjectId = id };
        SendToAllClients(msg);

        Logger.Debug("Server", $"Deleted ObjectId={id}");

        InvokeObjectDeleted(id);
    }

    uint RegisterSymbolAndNotify(string symbol)
    {
        var sid = symbolIdRegistry.Create();
        SymbolTable.Add(symbol, sid);

        IControlMessage msg = new SymbolRegisteredMessage { Symbol = symbol, SymbolId = sid };
        SendToAllClients(msg);

        Logger.Debug("Server", $"Registered Symbol {symbol}->{sid}");

        return sid;
    }

    void SendToAllClients(IControlMessage msg)
    {
        foreach (Connection conn in clients.Values)
        {
            conn.SendControlMessage(msg);
        }
    }

    public override Task<uint> CreateObject()
    {
        return runner.Schedule(() => CreateObjectAndNotify(NodeId));
    }

    public override void DeleteObject(uint id)
    {
        runner.Schedule(() => { DeleteObjectAndNotify(id, NodeId); return 0; });
    }

    protected override async Task<uint> InternSymbol(string symbol)
    {
        if (SymbolTable.Forward.ContainsKey(symbol))
        {
            // No wait if the symbol is already registered
            return SymbolTable.Forward[symbol];
        }

        return await runner.Schedule(() => RegisterSymbolAndNotify(symbol));
    }

    protected override void OnNewBlob(BlobHandle handle, Blob blob)
    {
        // Do nothing.
    }

    protected override void RequestBlob(BlobHandle handle)
    {
        // Do nothing. Server does not have to request blobs
    }

    public override void Dispose()
    {
        foreach (var conn in Connections.Values)
        {
            conn.Dispose();
        }

        if (listener != null)
        {
            listener.Dispose();
        }

        base.Dispose();
    }
}

} // end namespace
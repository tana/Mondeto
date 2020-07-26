using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using MessagePack;

public class SyncServer : SyncNode
{
    Dictionary<uint, Connection> clients = new Dictionary<uint, Connection>();
    protected override Dictionary<uint, Connection> Connections { get => clients; }
    Signaler signaler;

    public readonly int UdpPort;
    public readonly int TcpPort;

    public override uint NodeId { get; protected set; } = ServerNodeId; // Node ID for server is always 0

    IdRegistry objectIdRegistry = new IdRegistry(WorldObjectId + 1);

    IdRegistry symbolIdRegistry = new IdRegistry(0);

    Runner<uint> runner = new Runner<uint>();

    public SyncServer(string signalerUri)
    {
        signaler = new Signaler(signalerUri, true);
    }

    public override async Task Initialize()
    {
        // Create World object
        Objects[WorldObjectId] = new SyncObject(this, WorldObjectId, ServerNodeId);
        InvokeObjectCreated(WorldObjectId); // To invoke ObjectCreated event for World Object (ID=0)

        // Start communication
        await signaler.ConnectAsync();
        Logger.Log("Server", "Connected to signaling server");
        signaler.ClientConnected += async (uint clientNodeId) => {
            Logger.Log("Server", $"Accepting client {clientNodeId}");
            var conn = new Connection();
            await conn.SetupAsync(signaler, true, clientNodeId);
            // Node ID is assigned by signaling server
            await InitClient(conn, clientNodeId);
        };
    }

    async Task InitClient(Connection conn, uint clientId)
    {
        lock (clients)
        {
            clients[clientId] = conn;
            Logger.Log("Server", $"Registered client NodeId={clientId}");
        }

        // NodeIdMessage have to be sent after client become ready to receive it.
        await Task.Delay(1000); // FIXME

        // Connection procedures
        // FIXME: this message has not meaningful but seems working as a kind of "ready"
        conn.SendMessage<ITcpMessage>(Connection.ChannelType.Control, new NodeIdMessage { NodeId = clientId });

        // Send existing objects
        foreach (var pair in Objects)
        {
            var id = pair.Key;
            var obj = pair.Value;
            ITcpMessage msg = new ObjectCreatedMessage { ObjectId = id, OriginalNodeId = obj.OriginalNodeId };
            conn.SendMessage<ITcpMessage>(Connection.ChannelType.Control, msg);
        }

        var cancelSource = new CancellationTokenSource();
        conn.OnDisconnect += () => cancelSource.Cancel();
        var _ = ProcessBlobMessagesAsync(clientId, conn, cancelSource.Token);
        //_ = ProcessAudioMessagesAsync(clientId, conn, cancelSource.Token);
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

            ITcpMessage msg;
            while (conn.TryReceiveMessage<ITcpMessage>(Connection.ChannelType.Control, out msg))
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
                    HandleEventSentMessage(
                        eventSentMessage.Name,
                        eventSentMessage.Sender, eventSentMessage.Receiver,
                        eventSentMessage.Args
                    );
                }
            }

            // FIXME
            ProcessAudioMessages(id, conn);
        }
    }

    /*
    async Task ProcessAudioMessagesAsync(uint clientId, Connection conn, CancellationToken cancel)
    {
        while (true)
        {
            cancel.ThrowIfCancellationRequested();
            var msg = await conn.ReceiveMessageAsync<AudioDataMessage>(Connection.ChannelType.Audio);
    */
    void ProcessAudioMessages(uint clientId, Connection conn)
    {
        AudioDataMessage msg;
        while (conn.TryReceiveMessage<AudioDataMessage>(Connection.ChannelType.Audio, out msg)) {
            if (!Objects.ContainsKey(msg.ObjectId)) return;  // Something is wrong

            // forward to other nodes
            foreach (var pair in clients)
            {
                if (pair.Key == clientId) continue;  // Avoid sending the message back to the sender
                pair.Value.SendMessage<AudioDataMessage>(Connection.ChannelType.Audio, msg);
            }

            if (Objects[msg.ObjectId].OriginalNodeId != NodeId)
            {
                // when original is not on the server
                HandleAudioDataMessage(msg);
            }
        }
    }

    uint CreateObjectAndNotify(uint originalNodeId)
    {
        uint id = objectIdRegistry.Create();
        var obj = new SyncObject(this, id, originalNodeId);
        Objects[id] = obj;

        // Notify object creation to all clients
        ITcpMessage msg = new ObjectCreatedMessage { ObjectId = id, OriginalNodeId = originalNodeId };
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
        
        ITcpMessage msg = new ObjectDeletedMessage { ObjectId = id };
        SendToAllClients(msg);

        Logger.Debug("Server", $"Deleted ObjectId={id}");

        InvokeObjectDeleted(id);
    }

    uint RegisterSymbolAndNotify(string symbol)
    {
        var sid = symbolIdRegistry.Create();
        SymbolTable.Add(symbol, sid);

        ITcpMessage msg = new SymbolRegisteredMessage { Symbol = symbol, SymbolId = sid };
        SendToAllClients(msg);

        Logger.Debug("Server", $"Registered Symbol {symbol}->{sid}");

        return sid;
    }

    void SendToAllClients(ITcpMessage msg)
    {
        foreach (Connection conn in clients.Values)
        {
            conn.SendMessage<ITcpMessage>(Connection.ChannelType.Control, msg);
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

        signaler.Dispose();
    }
}
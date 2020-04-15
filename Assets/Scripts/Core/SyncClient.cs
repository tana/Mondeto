using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

public class SyncClient : SyncNode
{
    protected override Dictionary<uint, Connection> Connections { get; } = new Dictionary<uint, Connection>();
    Connection conn;
    Signaler signaler;

    public override uint NodeId { get; protected set; }

    Queue<TaskCompletionSource<uint>> creationQueue = new Queue<TaskCompletionSource<uint>>();

    CompletionNotifier<string, uint> symbolNotifier = new CompletionNotifier<string, uint>();

    public SyncClient(string signalerUri)
    {
        conn = new Connection();
        Connections[0] = conn;
        signaler = new Signaler(signalerUri, false);
    }

    public override async Task Initialize()
    {
        await Task.Delay(1000);
        await signaler.ConnectAsync();
        Logger.Log("Client", "Connected to signaling server");
        await Task.Delay(1000);
        await conn.SetupAsync(signaler, false);
        Logger.Log("Client", "Connected to server");

        if (await conn.ReceiveMessageAsync<ITcpMessage>(Connection.ChannelType.Control) is NodeIdMessage nodeIdMsg)
        {
            NodeId = nodeIdMsg.NodeId;
            Logger.Debug("Client", $"Received NodeId={NodeId}");
        }
        else
        {
            throw new ProtocolException(nameof(NodeIdMessage) + " expected");
        }

        var cancelSource = new CancellationTokenSource();
        conn.OnDisconnect += () => cancelSource.Cancel();
        var _ = ProcessBlobMessagesAsync(ServerNodeId, conn, cancelSource.Token);
    }

    protected override void ProcessControlMessages()
    {
        if (!conn.Connected) return;
        ITcpMessage msg;
        while (conn.TryReceiveMessage<ITcpMessage>(Connection.ChannelType.Control, out msg))
        {
            HandleTcpMessage(msg);
        }

        // FIXME
        ProcessAudioMessages();
    }

    void HandleTcpMessage(ITcpMessage msg)
    {
        switch (msg)
        {
            case ObjectCreatedMessage objMsg:
            {
                var id = objMsg.ObjectId;
                Objects[id] = new SyncObject(this, id, objMsg.OriginalNodeId);

                if (creationQueue.Count > 0 && objMsg.OriginalNodeId == NodeId)
                {
                    lock (creationQueue)
                    {
                        creationQueue.Dequeue().SetResult(id);
                    }
                }

                Logger.Debug("Client", $"Received ObjectId={id}");
                break;
            }
            case ObjectDeletedMessage delMsg:
            {
                var id = delMsg.ObjectId;
                Objects.Remove(id);

                Logger.Debug("Client", $"Received Deletion of ObjectId={id}");
                break;
            }
            case SymbolRegisteredMessage symMsg:
            {
                SymbolTable.Add(symMsg.Symbol, symMsg.SymbolId);
                if (symbolNotifier.IsWaiting(symMsg.Symbol))
                    symbolNotifier.Notify(symMsg.Symbol, symMsg.SymbolId);

                Logger.Debug("Client", $"Received Symbol {symMsg.Symbol}->{symMsg.SymbolId}");
                break;
            }
        }
    }

    void ProcessAudioMessages()
    {
        AudioDataMessage msg;
        while (conn.TryReceiveMessage<AudioDataMessage>(Connection.ChannelType.Audio, out msg))
        {
            HandleAudioDataMessage(msg);
        }
    }

    public override async Task<uint> CreateObject()
    {
        Logger.Debug("Client", "Creating object");
        conn.SendMessage<ITcpMessage>(Connection.ChannelType.Control, new CreateObjectMessage());
        var tcs = new TaskCompletionSource<uint>();
        lock (creationQueue)
        {
            creationQueue.Enqueue(tcs);
        }
        //var id = await Task.WhenAny(tcs.Task, ProtocolUtil.Timeout<int>(100000, "object creation timeout")).Result;
        var id = await tcs.Task;
        Logger.Debug("Client", $"Created ObjectId={id}");
        return id;
    }

    public override void DeleteObject(uint id)
    {
        Logger.Debug("Client", $"Deleting ObjectId={id}");
        conn.SendMessage<ITcpMessage>(Connection.ChannelType.Control, new DeleteObjectMessage { ObjectId = id });
    }

    protected override async Task<uint> InternSymbol(string symbol)
    {
        if (SymbolTable.Forward.ContainsKey(symbol))
        {
            // No wait if the symbol is already registered
            return SymbolTable.Forward[symbol];
        }

        conn.SendMessage<ITcpMessage>(Connection.ChannelType.Control, new RegisterSymbolMessage { Symbol = symbol });

        return await symbolNotifier.Wait(symbol);
    }

    protected override void OnNewBlob(BlobHandle handle, Blob blob)
    {
        SendBlob(ServerNodeId, handle, blob);
    }

    protected override void RequestBlob(BlobHandle handle)
    {
        conn.SendMessage<IBlobMessage>(
            Connection.ChannelType.Blob,
            new BlobRequestMessage { Handle = handle }
        );
    }

    public override void Dispose()
    {
        conn.Dispose();
        signaler.Dispose();
    }
}
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mondeto.Core.QuicWrapper;

namespace Mondeto.Core
{

public class SyncClient : SyncNode
{
    protected override Dictionary<uint, Connection> Connections { get; } = new Dictionary<uint, Connection>();
    Connection conn;

    public override uint NodeId { get; protected set; }

    Queue<TaskCompletionSource<uint>> creationQueue = new Queue<TaskCompletionSource<uint>>();

    CompletionNotifier<string, uint> symbolNotifier = new CompletionNotifier<string, uint>();

    const int NodeIdRetryTimeout = 10000;

    string serverHost;
    int serverPort;

    bool noCertValidation;
    string keyLogFile = "";

    public SyncClient(string serverHost, int serverPort, bool noCertValidation = false, string keyLogFile = "")
        : base()
    {
        this.serverHost = serverHost;
        this.serverPort = serverPort;
        this.noCertValidation = noCertValidation;
        this.keyLogFile = keyLogFile;

        if (this.noCertValidation)
        {
            Logger.Log("SyncClient", "Skipping TLS certificate validation");
        }

        if (this.keyLogFile != "")
        {
            Logger.Log("SyncClient", $"TLS key log will be saved as {keyLogFile}");
        }
    }

    public override async Task Initialize()
    {
        var connectCancelSource = new CancellationTokenSource();
        connectCancelSource.CancelAfter(NodeIdRetryTimeout);
        var connectCancel = connectCancelSource.Token;

        QuicConnection quicConnection = new(enableKeyLog: this.keyLogFile != "");
        quicConnection.Start(new byte[][] { SyncNode.Alpn }, serverHost, serverPort, noCertValidation);

        conn = new Connection(quicConnection);  // Disposal of QuicConnection is done by Connection
        Connections[0] = conn;

        await conn.SetupClientAsync(connectCancel);

        // TODO: authentication (e.g. password)

        while (true)
        {
            connectCancel.ThrowIfCancellationRequested();

            if (await conn.ReceiveControlMessageAsync(connectCancel) is NodeIdMessage nodeIdMsg)
            {
                NodeId = nodeIdMsg.NodeId;
                Logger.Debug("Client", $"Received NodeId={nodeIdMsg.NodeId}");
                break;
            }
        }

        var cancelSource = new CancellationTokenSource();
        conn.OnDisconnect += () => cancelSource.Cancel();
        var _ = ProcessBlobMessagesAsync(ServerNodeId, conn, cancelSource.Token);
    }

    protected override void ProcessControlMessages()
    {
        if (!conn.Connected) return;
        IControlMessage msg;
        while (conn.TryReceiveControlMessage(out msg))
        {
            HandleControlMessage(msg);
        }

        // FIXME
        // ProcessAudioMessages();
    }

    void HandleControlMessage(IControlMessage msg)
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

                InvokeObjectCreated(id);

                break;
            }
            case ObjectDeletedMessage delMsg:
            {
                var id = delMsg.ObjectId;
                Objects.Remove(id);

                Logger.Debug("Client", $"Received Deletion of ObjectId={id}");

                InvokeObjectDeleted(id);

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
            case EventSentMessage eventSentMessage:
            {
                HandleEventSentMessage(
                    eventSentMessage.Name,
                    eventSentMessage.Sender, eventSentMessage.Receiver,
                    eventSentMessage.Args
                );
                break;
            }
        }
    }

    /*
    void ProcessAudioMessages()
    {
        AudioDataMessage msg;
        while (conn.TryReceiveMessage<AudioDataMessage>(Connection.ChannelType.Audio, out msg))
        {
            HandleAudioDataMessage(msg);
        }
    }
    */

    public override async Task<uint> CreateObject()
    {
        Logger.Debug("Client", "Creating object");
        conn.SendControlMessage(new CreateObjectMessage());
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
        conn.SendControlMessage(new DeleteObjectMessage { ObjectId = id });
    }

    protected override async Task<uint> InternSymbol(string symbol)
    {
        if (SymbolTable.Forward.ContainsKey(symbol))
        {
            // No wait if the symbol is already registered
            return SymbolTable.Forward[symbol];
        }

        conn.SendControlMessage(new RegisterSymbolMessage { Symbol = symbol });

        return await symbolNotifier.Wait(symbol);
    }

    protected override void OnNewBlob(BlobHandle handle, Blob blob)
    {
        SendBlob(ServerNodeId, handle, blob);
    }

    protected override void RequestBlob(BlobHandle handle)
    {
        conn.SendBlobMessage(
            new BlobRequestMessage { Handle = handle }
        );
    }

    public override void Dispose()
    {
        if (conn != null)
        {
            if (keyLogFile != "")
            {
                using (var writer = new System.IO.StreamWriter(keyLogFile))
                {
                    writer.WriteLine(conn.GetKeyLog());
                }
            }

            conn.Dispose();
        }

        base.Dispose();
    }
}

} // end namespace
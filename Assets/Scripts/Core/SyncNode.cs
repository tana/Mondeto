using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Mondeto.Core
{

public abstract class SyncNode : IDisposable
{
    protected abstract Dictionary<uint, Connection> Connections { get; }

    public abstract uint NodeId { get; protected set; }

    public const uint ServerNodeId = 0;

    public const uint WorldObjectId = 0;    // Object ID 0 is reserved for World object (system object)

    public static readonly byte[] Alpn = new byte[] { (byte)'m', (byte)'o', (byte)'n', (byte)'d', (byte)'e', (byte)'t', (byte)'o' };

    // Do not modify Objects outside main loop! Otherwise data corrupts (e.g. strange null)
    public Dictionary<uint, SyncObject> Objects { get; } = new Dictionary<uint, SyncObject>();

    public BidirectionalDictionary<string, uint> SymbolTable { get; } = new BidirectionalDictionary<string, uint>();

    // 2^32 * 1/50 s = approx. 994 days
    public uint Tick { get; protected set; } = 0;

    // Make it ready to SyncFrame
    public abstract Task Initialize();

    protected BlobStorage BlobStorage = new BlobStorage();

    private BlobCache BlobCache;

    protected const int BlobChunkSize = 65536;

    private ConcurrentDictionary<uint, object> blobSendLockTokens = new ConcurrentDictionary<uint, object>();

    // TODO: garbage collection of these two dictionaries
    // Latest Tick received from a particular node
    private ConcurrentDictionary<(uint nodeId, uint objectId, string fieldName), uint> LastReceivedTick = new();
    // Last tick acknowledged by a particular node
    private ConcurrentDictionary<(uint nodeId, uint objectId, string fieldName), uint> LastAcknowledgedTick = new();

    public delegate void ObjectCreatedHandler(uint objId);
    // Fired when an object is created 
    public event ObjectCreatedHandler ObjectCreated;

    public delegate void ObjectDeletedHandler(uint objId);
    public event ObjectDeletedHandler ObjectDeleted;

    private Dictionary<string, Func<SyncObject, ITag>> TagCreators = new Dictionary<string, Func<SyncObject, ITag>>();

    public SyncNode()
    {
        // Register simple (Unity-independent) tags
        RegisterTag("grabbable", _ => new GrabbableTag());
        RegisterTag("objectMoveButton", _ => new ObjectMoveButtonTag());

        // For synth demo
        RegisterTag("midiInput", _ => new MidiInputTag());
        // For LED demo
        RegisterTag("ledButton", _ => new LedButtonTag());
    }

    // TODO naming
    public void SyncFrame(float dt)
    {
        foreach (var obj in Objects.Values)
        {
            obj.ProcessBeforeSync(dt);
        }

        ProcessControlMessages();

        // Receive states of copy objects and ACK from other nodes
        List<KeyValuePair<uint, Connection>> pairs;
        lock (Connections)
        {
            pairs = Connections.ToList();
        }

        foreach (var connPair in pairs)
        {
            uint connNodeId = connPair.Key;
            Connection conn = connPair.Value;
            
            IDatagramMessage msg;
            while (conn.TryReceiveDatagramMessage(out msg))
            {
                switch (msg)
                {
                    case UpdateMessage updateMsg:
                        ProcessUpdateMessage(updateMsg, connNodeId, conn);
                        break;
                }
            }
        }

        // Send states of objects
        foreach (var (connNodeId, conn) in pairs)
        {
            foreach (var (id, obj) in Objects)
            {
                // If this is the server, don't send object to the node which has original
                // If this is a client, don't send objects that this node does not have original
                if (obj.OriginalNodeId == connNodeId || (NodeId != ServerNodeId && obj.OriginalNodeId != NodeId))
                    continue;

                foreach (var (fieldName, field) in obj.Fields)
                {
                    // Send fields which have been updated
                    // Because updates run after sending, lastUpdatedTick==lastTickAcknowledged must be included.
                    var key = (connNodeId, id, fieldName);
                    if (!LastAcknowledgedTick.ContainsKey(key) || field.LastUpdatedTick >= LastAcknowledgedTick[key])
                    {
                        var msg = new UpdateMessage { Tick = Tick, ObjectId = id, FieldName = fieldName, FieldValue = field.Value };
                        conn.SendDatagramMessage(msg, () => {
                            LastAcknowledgedTick[key] = Tick;
                        });
                    }
                }
            }
        }

        Tick += 1;

        foreach (var obj in Objects.Values)
        {
            obj.ProcessAfterSync(dt);
        }
    }

    private void ProcessUpdateMessage(UpdateMessage msg, uint connNodeId, Connection conn)
    {
        var id = msg.ObjectId;
        if (!Objects.ContainsKey(id))
        {
            Logger.Log("Node", $"Ignoring update for non-registered ObjectId={id}");
            return;
        }

        var obj = Objects[id];

        if (obj.OriginalNodeId != connNodeId && connNodeId != ServerNodeId)
        {
            Logger.Error("Node", $"Blocked invalid update for ObjectId={id}");
            return;   // Original object cannot be updated by nodes other than OriginalNodeId or the server (NodeId=0)
        }

        var key = (connNodeId, id, msg.FieldName);

        // Ignore out-of-order updates
        if (LastReceivedTick.ContainsKey(key) && LastReceivedTick[key] > msg.Tick)
        {
            return;
        }
        LastReceivedTick[key] = msg.Tick;

        obj.SetField(msg.FieldName, msg.FieldValue);
    }

    private void InitBlobCache()
    {
        BlobCache = new BlobCache(Settings.Instance.TempDirectory);
    }

    public void WriteBlob(BlobHandle handle, Blob blob)
    {
        BlobStorage.Write(handle, blob);
        OnNewBlob(handle, blob);

        if (BlobCache == null) InitBlobCache();
        BlobCache.Add(handle, blob);
    }

    public async Task<Blob> ReadBlob(BlobHandle handle)
    {
        if (BlobCache == null) InitBlobCache();
        Blob? cache = BlobCache.Find(handle);
        if (cache.HasValue) return cache.Value;

        // Request blob if it was not found in cache
        RequestBlob(handle);
        return await BlobStorage.Read(handle);
    }

    public async Task<(string path, string mimeType)> GetBlobTempFile(BlobHandle handle)
    {
        await ReadBlob(handle);
        return (BlobCache.HandleToPath(handle), BlobCache.GetMimeType(handle));
    }

    protected void SendBlob(uint nodeId, BlobHandle handle, Blob blob)
    {
        blobSendLockTokens.TryAdd(nodeId, new object());

        lock (blobSendLockTokens[nodeId])
        {
            Logger.Debug("Node", $"Sending Blob {handle} to NodeId={nodeId}");

            Connection conn = Connections[nodeId];
            conn.SendBlobMessage(
                new BlobInfoMessage { Handle = handle, Size = (uint)blob.Data.Length, MimeType = blob.MimeType }
            );
            int pos = 0;
            while (pos < blob.Data.Length)
            {
                int len = Math.Min(BlobChunkSize, blob.Data.Length - pos);
                byte[] chunk = new byte[len];
                Array.Copy(blob.Data, pos, chunk, 0, len);
                conn.SendBlobMessage(
                    new BlobBodyMessage { Handle = handle, Offset = (uint)pos, Data = chunk }
                );
                pos += len;
            }

            Logger.Debug("Node", $"Sent Blob {handle}");
        }
    }

    protected async Task ProcessBlobMessagesAsync(uint nodeId, Connection conn, CancellationToken cancel = default)
    {
        while (true)
        {
            cancel.ThrowIfCancellationRequested();
            var msg = await conn.ReceiveBlobMessageAsync(cancel);
            if (msg is BlobInfoMessage infoMsg)
            {
                Logger.Debug("Node", $"Receiving Blob {infoMsg.Handle} (size={infoMsg.Size}) from Node {nodeId}");
                // 今はBlobInfoMessageの後にBlobBodyMessageが連続で来て全部送られるという想定
                byte[] data = await ReceiveBlobBodyAsync(conn, (int)infoMsg.Size, cancel);
                BlobStorage.Write(
                    infoMsg.Handle,
                    new Blob(data, infoMsg.MimeType)
                );
                Logger.Debug("Node", $"Received Blob {infoMsg.Handle}");
            }
            else if (msg is BlobRequestMessage requestMsg)
            {
                Logger.Debug("Node", $"Received request for Blob {requestMsg.Handle}");
                var _ = Task.Run(async () => {
                    Blob blob = await BlobStorage.Read(requestMsg.Handle);
                    SendBlob(nodeId, requestMsg.Handle, blob);
                }, cancel);
            }
            else if (msg is BlobBodyMessage bodyMsg)
            {
                Logger.Error("Node", $"Blob body Offset={bodyMsg.Offset} DataLen={bodyMsg.Data.Length}");
            }
            else
            {
                Logger.Error("Node", $"Unknown blob message {msg}");
            }
        }
    }

    protected async Task<byte[]> ReceiveBlobBodyAsync(Connection conn, int size, CancellationToken cancel = default)
    {
        var data = new byte[size];

        int pos = 0;
        do {
            cancel.ThrowIfCancellationRequested();
            var msg = await conn.ReceiveBlobMessageAsync(cancel);
            if (msg is BlobBodyMessage bodyMsg)
            {
                // TODO check bodyMsg.Handle
                // TODO support bodyMsg.Offset ?
                Array.Copy(
                    bodyMsg.Data, 0, data, pos,
                    Math.Min(bodyMsg.Data.Length, size - pos)
                );
                pos += bodyMsg.Data.Length;
                //Logger.Debug("Node", $"Received {bodyMsg.Data.Length} bytes");
            }
            else
            {
                Logger.Error("Node", "Invalid message during receiving blob body " + msg.ToString());
                break;
            }
        } while (pos < size);

        return data;
    }

    protected abstract void ProcessControlMessages();
    public abstract Task<uint> CreateObject();
    public abstract void DeleteObject(uint id);

    public void SendEvent(string name, uint sender, uint receiver, IValue[] args)
    {
        foreach (Connection conn in Connections.Values)
        {
            conn.SendControlMessage(new EventSentMessage {
                Name = name,
                Sender = sender,
                Receiver = receiver,
                Args = args
            });
        }
    }

    protected void HandleEventSentMessage(string name, uint sender, uint receiver, IValue[] args)
    {
        if (!Objects.ContainsKey(sender) || Objects[sender].OriginalNodeId == NodeId)
        {
            // Something wrong
            Logger.Error("SyncNode", "Blocked invalid EventSentMessage");
        }

        if (Objects.TryGetValue(receiver, out SyncObject obj))
        {
            obj.HandleEvent(name, sender, args);
        }
        else
        {
            Logger.Log("SyncNode", $"Event receiver (object {receiver}) not found");
        }
    }

    protected abstract Task<uint> InternSymbol(string symbol);

    protected abstract void OnNewBlob(BlobHandle handle, Blob blob);
    protected abstract void RequestBlob(BlobHandle handle);

    public void SendAudioData(uint oid, byte[] opusData)
    {
        foreach (var conn in Connections.Values)
        {
            var msg = new AudioDataMessage { ObjectId = oid, OpusData = opusData };
            conn.SendDatagramMessage(msg);
        }
    }

    protected void HandleAudioDataMessage(AudioDataMessage msg)
    {
        if (!Objects.ContainsKey(msg.ObjectId)) return;  // Something is wrong
        SyncObject obj = Objects[msg.ObjectId];
        if (obj.OriginalNodeId == NodeId) return;

        obj.HandleAudio(msg.OpusData);
    }

    protected void InvokeObjectCreated(uint objId) => ObjectCreated?.Invoke(objId);

    protected void InvokeObjectDeleted(uint objId) => ObjectDeleted?.Invoke(objId);

    public void RegisterTag(string name, Func<SyncObject, ITag> creator)
    {
        TagCreators[name] = creator;
    }

    public ITag CreateTag(string name, SyncObject obj) => TagCreators[name](obj);

    public bool IsTagRegistered(string name) => TagCreators.ContainsKey(name);

    public virtual void Dispose()
    {
    }
}

} // end namespace
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public abstract class SyncNode : IDisposable
{
    protected abstract Dictionary<uint, Connection> Connections { get; }

    public abstract uint NodeId { get; protected set; }

    public const uint ServerNodeId = 0;

    //public Dictionary<IPAddress, int> UdpEpToNodeId { get; } = new Dictionary<IPAddress, int>();

    // Do not modify Objects outside main loop! Otherwise data corrupts (e.g. strange null)
    public Dictionary<uint, SyncObject> Objects { get; } = new Dictionary<uint, SyncObject>();

    public BidirectionalDictionary<string, uint> SymbolTable { get; } = new BidirectionalDictionary<string, uint>();

    // 2^32 * 1/50 s = approx. 994 days
    public uint Tick { get; protected set; } = 0;

    // Make it ready to SyncFrame
    public abstract Task Initialize();

    protected BlobStorage BlobStorage = new BlobStorage();

    protected const int BlobChunkSize = 65536;

    private ConcurrentDictionary<uint, object> blobSendLockTokens = new ConcurrentDictionary<uint, object>();

    // Latest Tick received from a particular node
    private Dictionary<uint, uint> LatestTickReceived = new Dictionary<uint, uint>();

    // Last tick acknowledged by a particular node
    private Dictionary<uint, uint> LastTickAcknowledged = new Dictionary<uint, uint>();

    public delegate void ObjectCreatedHandler(uint objId);
    // Fired when an object is created 
    public event ObjectCreatedHandler ObjectCreated;

    public delegate void ObjectDeletedHandler(uint objId);
    public event ObjectDeletedHandler ObjectDeleted;

    // TODO naming
    public void SyncFrame()
    {
        foreach (var obj in Objects.Values)
        {
            obj.ProcessBeforeSync();
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
            
            ISyncMessage msg;
            while (conn.TryReceiveMessage<ISyncMessage>(Connection.ChannelType.Sync, out msg))
            {
                switch (msg)
                {
                    case UpdateMessage updateMsg:
                        ProcessUpdateMessage(updateMsg, connNodeId, conn);
                        break;
                    case AckMessage ackMsg:
                        ProcessAckMessage(ackMsg, connNodeId);
                        break;
                }
            }
        }

        // Send states of objects
        foreach (var connPair in pairs)
        {
            uint connNodeId = connPair.Key;
            Connection conn = connPair.Value;
            // If no ACK has came from the connection yet (e.g. new connection), send all fields (any uint is larger than -1)
            long ackedTick = LastTickAcknowledged.ContainsKey(connNodeId)
                                ? (long)LastTickAcknowledged[connNodeId] : -1;

            // Collect object updates
            var updates = new List<ObjectUpdate>();
            foreach (var pair in Objects)
            {
                var id = pair.Key;
                var obj = pair.Value;
                // If this is the server, don't send object to the node which has original
                // If this is a client, don't send objects that this node does not have original
                if (obj.OriginalNodeId == connNodeId || (NodeId != ServerNodeId && obj.OriginalNodeId != NodeId))
                    continue;

                // field updates
                var fields = obj.Fields.Where(field =>
                    // Send fields which have been updated
                    // Because updates run after sending, lastUpdatedTick==ackedTick must be included.
                    field.Value.LastUpdatedTick >= ackedTick
                ).Select(field =>
                    new FieldUpdate { Name = field.Key, Value = field.Value.Value }
                ).ToList();

                if (fields.Count == 0) continue;    // When no field have to be sent, skip entire object

                ObjectUpdate update = new ObjectUpdate { ObjectId = id, Fields = fields };
                updates.Add(update);
            }

            if (updates.Count == 0) continue;   // When no object have to be sent, skip the receiving node

            // Construct message and send
            UpdateMessage msg = new UpdateMessage {
                Tick = Tick,
                ObjectUpdates = updates
            };
            conn.SendMessage<ISyncMessage>(Connection.ChannelType.Sync, msg);
        }

        Tick += 1;

        foreach (var obj in Objects.Values)
        {
            obj.ProcessAfterSync();
        }
    }

    private void ProcessUpdateMessage(UpdateMessage msg, uint connNodeId, Connection conn)
    {
        // Ignore out-of-order updates
        if (LatestTickReceived.ContainsKey(connNodeId)
            && LatestTickReceived[connNodeId] > msg.Tick)
        {
            return;
        }
        LatestTickReceived[connNodeId] = msg.Tick;

        foreach (ObjectUpdate update in msg.ObjectUpdates)
        {
            var id = update.ObjectId;
            if (!Objects.ContainsKey(id))
            {
                Logger.Log("Node", $"Ignoring update for non-registered ObjectId={id}");
                continue;
            }
            if (Objects[id].OriginalNodeId != connNodeId && connNodeId != ServerNodeId)
            {
                Logger.Error("Node", $"Blocked invalid update for ObjectId={id}");
                continue;   // Original object cannot be updated by nodes other than OriginalNodeId or the server (NodeId=0)
            }

            //Logger.Log("Node", $"ObjectId={id} updated");

            // fields
            foreach (var field in update.Fields)
            {
                //Logger.Log("Node", $"Field {field.Name} = {field.Value}");
                /*
                if (field.Name == "position" && field.Value is Vec vec)
                {
                    Logger.Debug("Node", $"{connNodeId} {id} ({vec.X}, {vec.Y}, {vec.Z})");
                }
                */
                //Objects[id].Fields[field.Name] = field.Value;
                Objects[id].SetField(field.Name, field.Value);
            }
        }

        // Notify the sender that this node have processed the message
        conn.SendMessage<ISyncMessage>(
            Connection.ChannelType.Sync,
            new AckMessage { AcknowledgedTick = msg.Tick }
        );
    }

    private void ProcessAckMessage(AckMessage msg, uint connNodeId)
    {
        if (!LastTickAcknowledged.ContainsKey(connNodeId)
            || LastTickAcknowledged[connNodeId] < msg.AcknowledgedTick)
        {
            LastTickAcknowledged[connNodeId] = msg.AcknowledgedTick;
        }
        else
        {
            // If AcknowledgedTick is decreasing, something is wrong
            Logger.Debug("SyncNode", $"Acknowledged Tick from NodeId={connNodeId} is decreasing. It's strange!");
        }
    }

    public BlobHandle GenerateBlobHandle()
    {
        Guid guid = System.Guid.NewGuid();
        return new BlobHandle { Guid = guid.ToByteArray() };
    }

    public void WriteBlob(BlobHandle handle, Blob blob)
    {
        BlobStorage.Write(handle, blob);
        OnNewBlob(handle, blob);
    }

    public async Task<Blob> ReadBlob(BlobHandle handle)
    {
        RequestBlob(handle);
        return await BlobStorage.Read(handle);
    }

    protected void SendBlob(uint nodeId, BlobHandle handle, Blob blob)
    {
        blobSendLockTokens.TryAdd(nodeId, new object());

        lock (blobSendLockTokens[nodeId])
        {
            Logger.Debug("Node", $"Sending Blob {handle} to NodeId={nodeId}");

            Connection conn = Connections[nodeId];
            conn.SendMessage<IBlobMessage>(
                Connection.ChannelType.Blob,
                new BlobInfoMessage { Handle = handle, Size = (uint)blob.Data.Length, MimeType = blob.MimeType }
            );
            int pos = 0;
            while (pos < blob.Data.Length)
            {
                int len = Math.Min(BlobChunkSize, blob.Data.Length - pos);
                byte[] chunk = new byte[len];
                Array.Copy(blob.Data, pos, chunk, 0, len);
                conn.SendMessage<IBlobMessage>(
                    Connection.ChannelType.Blob,
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
            var msg = await conn.ReceiveMessageAsync<IBlobMessage>(Connection.ChannelType.Blob, cancel);
            if (msg is BlobInfoMessage infoMsg)
            {
                Logger.Debug("Node", $"Receiving Blob {infoMsg.Handle} (size={infoMsg.Size}) from Node {nodeId}");
                // 今はBlobInfoMessageの後にBlobBodyMessageが連続で来て全部送られるという想定
                byte[] data = await ReceiveBlobBodyAsync(conn, (int)infoMsg.Size, cancel);
                BlobStorage.Write(
                    infoMsg.Handle,
                    new Blob { MimeType = infoMsg.MimeType, Data = data }
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
            var msg = await conn.ReceiveMessageAsync<IBlobMessage>(Connection.ChannelType.Blob, cancel);
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

    protected abstract Task<uint> InternSymbol(string symbol);

    protected abstract void OnNewBlob(BlobHandle handle, Blob blob);
    protected abstract void RequestBlob(BlobHandle handle);

    public void SendAudioData(uint oid, byte[] data)
    {
        foreach (var conn in Connections.Values)
        {
            var msg = new AudioDataMessage { ObjectId = oid, Data = data };
            conn.SendMessage<AudioDataMessage>(Connection.ChannelType.Audio, msg);
        }
    }

    protected void HandleAudioDataMessage(AudioDataMessage msg)
    {
        if (!Objects.ContainsKey(msg.ObjectId)) return;  // Something is wrong
        SyncObject obj = Objects[msg.ObjectId];
        if (obj.OriginalNodeId == NodeId) return;
        obj.HandleAudio(msg.Data);
    }

    protected void InvokeObjectCreated(uint objId) => ObjectCreated?.Invoke(objId);

    protected void InvokeObjectDeleted(uint objId) => ObjectDeleted?.Invoke(objId);

    public abstract void Dispose();
}
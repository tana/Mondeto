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

    protected const int BlobChunkSize = 1024;

    private ConcurrentDictionary<uint, object> blobSendLockTokens = new ConcurrentDictionary<uint, object>();

    // Latest Tick received from a particular node
    private Dictionary<uint, uint> LatestTickReceived = new Dictionary<uint, uint>();

    // TODO naming
    public void SyncFrame()
    {
        ProcessControlMessages();

        // Receive states of copy objects and ACK from other nodes
        foreach (var connPair in Connections)
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
                        // TODO
                        break;
                }
            }
        }

        // Send states of objects
        foreach (var connPair in Connections)
        {
            uint connNodeId = connPair.Key;
            Connection conn = connPair.Value;
            // Collect object updates
            var updates = new List<ObjectUpdate>();
            foreach (var pair in Objects)
            {
                var id = pair.Key;
                var obj = pair.Value;
                // Don't send object to the node which has original
                if (obj.OriginalNodeId == connNodeId)
                    continue;

                // field updates
                var fields = obj.Fields.Select(field =>
                    new FieldUpdate { Name = field.Key, Value = field.Value.Value }).ToList();
                ObjectUpdate update = new ObjectUpdate { ObjectId = id, Fields = fields };
                updates.Add(update);
            }
            // Construct message and send
            UpdateMessage msg = new UpdateMessage {
                Tick = Tick,
                ObjectUpdates = updates
            };
            conn.SendMessage<ISyncMessage>(Connection.ChannelType.Sync, msg);
        }

        Tick += 1;
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
            if (Objects[id].OriginalNodeId == NodeId)
            {
                Logger.Error("Node", $"Blocked invalid update for ObjectId={id}");
                continue;   // Original object cannot be updated by nodes other than OriginalNodeId
            }

            //Logger.Log("Node", $"ObjectId={id} updated");

            // fields
            foreach (var field in update.Fields)
            {
                //Logger.Log("Node", $"Field {field.Name} = {field.Value}");
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
            Logger.Debug("Node", $"Sending Blob");

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

    public abstract void Dispose();
}
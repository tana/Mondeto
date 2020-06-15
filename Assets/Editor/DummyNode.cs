
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// Dummy Node that does nothing (for testing)
class DummyNode : SyncNode
{
    protected override Dictionary<uint, Connection> Connections { get; } = new Dictionary<uint, Connection>();
    public override uint NodeId { get; protected set; } = 0;

    private uint objectIdCounter = 0;

    public override Task Initialize() => new Task(() => {});

    protected override void ProcessControlMessages() {}

    public override Task<uint> CreateObject()
    {
        var obj = new SyncObject(this, objectIdCounter, NodeId);
        Objects[obj.Id] = obj;
        objectIdCounter++;
        // This task always completes immediately
        var tcs = new TaskCompletionSource<uint>();
        tcs.SetResult(obj.Id);
        return tcs.Task;
    }

    public override void DeleteObject(uint id) => throw new NotImplementedException();

    protected override Task<uint> InternSymbol(string symbol) => throw new NotImplementedException();

    protected override void RequestBlob(BlobHandle handle) => throw new NotImplementedException();

    protected override void OnNewBlob(BlobHandle handle, Blob blob) => throw new NotImplementedException();

    public override void Dispose() {}
}
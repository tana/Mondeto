using System;
using System.Collections.Generic;

// TODO name change
public class SyncObject
{
    public uint Id;

    public uint OriginalNodeId;

    public SyncNode Node;

    public readonly Dictionary<string, IValue> Fields = new Dictionary<string, IValue>();

    public delegate void AudioReceivedDelegate(byte[] data);
    // Called when the original used SendAudio
    public event AudioReceivedDelegate AudioReceived;

    public SyncObject(SyncNode node, uint id, uint originalNodeId)
    {
        Node = node;
        Id = id;
        OriginalNodeId = originalNodeId;

        // All objects have position and rotation fields
        Fields["position"] = new Vec();
        Fields["rotation"] = new Quat();

        Fields["velocity"] = new Vec();
        Fields["angularVelocity"] = new Vec();

        Fields["tag"] = new Primitive<int> { Value = 0 };
    }

    public void SendAudio(byte[] data)
    {
        Node.SendAudioData(Id, data);
    }

    internal void HandleAudio(byte[] data)
    {
        AudioReceived?.Invoke(data);
    }
}
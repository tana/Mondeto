using System;
using System.Collections.Generic;

// TODO name change
public class SyncObject
{
    public uint Id;

    public uint OriginalNodeId;

    public SyncNode Node;

    public readonly Dictionary<string, Field> Fields = new Dictionary<string, Field>();

    public delegate void AudioReceivedDelegate(byte[] data);
    // Called when the original used SendAudio
    public event AudioReceivedDelegate AudioReceived;

    public SyncObject(SyncNode node, uint id, uint originalNodeId)
    {
        Node = node;
        Id = id;
        OriginalNodeId = originalNodeId;

        // All objects have position and rotation fields
        SetField("position", new Vec());
        SetField("rotation", new Quat());

        SetField("velocity", new Vec());
        SetField("angularVelocity", new Vec());

        SetField("tag", new Primitive<int> { Value = 0 });
    }

    // Update field value and refresh last updated time.
    public void SetField(string key, IValue val)
    {
        Field field = Fields.ContainsKey(key) ? Fields[key] : new Field();
        field.Value = val;
        field.LastUpdatedTick = Node.Tick;
        Fields[key] = field;
    }

    public IValue GetField(string key)
    {
        return Fields[key].Value;
    }

    public void SendAudio(byte[] data)
    {
        Node.SendAudioData(Id, data);
    }

    internal void HandleAudio(byte[] data)
    {
        AudioReceived?.Invoke(data);
    }

    public struct Field
    {
        public IValue Value;
        public uint LastUpdatedTick;
    }
}
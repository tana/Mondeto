using System;
using System.Collections.Generic;

// TODO name change
public class SyncObject
{
    public uint Id;

    public uint OriginalNodeId;

    public SyncNode Node;

    public readonly Dictionary<string, Field> Fields = new Dictionary<string, Field>();

    public delegate void AudioReceivedDelegate(float[] data);
    // Called when the original used SendAudio
    public event AudioReceivedDelegate AudioReceived;

    public delegate void BeforeAfterSyncDelegate(SyncObject sender);
    public event BeforeAfterSyncDelegate BeforeSync;
    public event BeforeAfterSyncDelegate AfterSync;

    public delegate void TagAddedDelegate(SyncObject sender, string tag);
    public event TagAddedDelegate TagAdded;

    HashSet<string> tags = new HashSet<string>();

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

        SetField("tags", new Sequence { 
            Elements = new List<IValue> {
            }
        });
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

    public bool HasField(string key)
    {
        return Fields.ContainsKey(key);
    }

    public bool TryGetField<T>(string key, out T value)
    {
        if (HasField(key) && (GetField(key) is T val))
        {
            value = val;
            return true;
        }
        else
        {
            value = default;
            return false;
        }
    }

    public bool TryGetFieldPrimitive<T>(string key, out T value)
    {
        Primitive<T> primitive;
        if (TryGetField<Primitive<T>>(key, out primitive))
        {
            value = primitive.Value;
            return true;
        }
        else
        {
            value = default;
            return false;
        }
    }

    public bool HasTag(string tag)
    {
        return tags.Contains(tag);
    }

    public void SendAudio(float[] data)
    {
        Node.SendAudioData(Id, data);
    }

    internal void HandleAudio(float[] data)
    {
        AudioReceived?.Invoke(data);
    }

    internal void ProcessBeforeSync()
    {
        BeforeSync?.Invoke(this);
    }

    internal void ProcessAfterSync()
    {
        AfterSync?.Invoke(this);

        // Set behavior based on tags
        if (GetField("tags") is Sequence tagsSeq)
        {
            foreach (var elem in tagsSeq.Elements)
            {
                if (!(elem is Primitive<string>)) continue;
                string tag = ((Primitive<string>)elem).Value;
                
                if (!tags.Contains(tag))
                {
                    TagAdded?.Invoke(this, tag);
                    tags.Add(tag);
                    Logger.Debug("Object", $"Tag {tag} is added to object {Id}");
                }
                // TODO: handle deleted tags?
            }
        }
    }

    public struct Field
    {
        public IValue Value;
        public uint LastUpdatedTick;
    }
}
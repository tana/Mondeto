using System;
using System.Collections.Generic;

// TODO name change
public class SyncObject
{
    public uint Id;

    public uint OriginalNodeId;

    public SyncNode Node;

    public readonly Dictionary<string, Field> Fields = new Dictionary<string, Field>();

    public readonly Dictionary<string, HashSet<Action>> FieldUpdateHandlers = new Dictionary<string, HashSet<Action>>();

    public readonly Dictionary<string, HashSet<Action<uint, IValue[]>>> EventHandlers = new Dictionary<string, HashSet<Action<uint, IValue[]>>>();

    public delegate void AudioReceivedDelegate(float[] data);
    // Called when the original used SendAudio
    public event AudioReceivedDelegate AudioReceived;

    public delegate void BeforeAfterSyncDelegate(SyncObject sender, float dt);
    public event BeforeAfterSyncDelegate BeforeSync;
    public event BeforeAfterSyncDelegate AfterSync;

    Dictionary<string, ITag> tags = new Dictionary<string, ITag>();

    Dictionary<BlobHandle, ObjectWasmRunner> codes = new Dictionary<BlobHandle, ObjectWasmRunner>();

    // Logging-related
    Queue<Logger.LogEntry> logEntries = new Queue<Logger.LogEntry>();
    public IEnumerable<Logger.LogEntry> Logs { get => logEntries; }

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
        
        SetField("codes", new Sequence { 
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

        if (FieldUpdateHandlers.ContainsKey(key))
        {
            foreach (Action handler in FieldUpdateHandlers[key])
            {
                handler();
            }
        }
    }

    public IValue GetField(string key)
    {
        return Fields[key].Value;
    }

    public bool HasField(string key)
    {
        return Fields.ContainsKey(key);
    }
    
    public void RegisterFieldUpdateHandler(string key, Action handler)
    {
        if (!FieldUpdateHandlers.ContainsKey(key))
        {
            FieldUpdateHandlers[key] = new HashSet<Action>();
        }

        FieldUpdateHandlers[key].Add(handler);
    }

    public void DeleteFieldUpdateHandler(string key, Action handler)
    {
        if (!FieldUpdateHandlers.ContainsKey(key)) return;

        FieldUpdateHandlers[key].Remove(handler);
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
        return tags.ContainsKey(tag);
    }

    public void SendAudio(float[] data)
    {
        Node.SendAudioData(Id, data);
    }

    public ObjectRef GetObjectRef()
    {
        return new ObjectRef { Id = this.Id };
    }

    public void RegisterEventHandler(string name, Action<uint, IValue[]> handler)
    {
        if (!EventHandlers.ContainsKey(name))
        {
            EventHandlers[name] = new HashSet<Action<uint, IValue[]>>();
        }

        EventHandlers[name].Add(handler);
    }

    public void DeleteEventHandler(string name, Action<uint, IValue[]> handler)
    {
        if (!EventHandlers.ContainsKey(name)) return;

        EventHandlers[name].Remove(handler);
    }

    internal void HandleEvent(string name, uint sender, IValue[] args)
    {
        if (EventHandlers.TryGetValue(name, out var handlers))
        {
            foreach (var handler in handlers)
            {
                handler(sender, args);
            }
        }
    }

    public void SendEvent(string name, uint sender, IValue[] args, bool localOnly = false)
    {
        HandleEvent(name, sender, args);
        if (!localOnly)
        {
            Node.SendEvent(name, sender, Id, args);
        }
    }

    public void SendEvent(string name, uint sender) => SendEvent(name, sender, new IValue[0]);
    public void SendEvent(string name, IValue[] args) => SendEvent(name, SyncNode.WorldObjectId, args);
    public void SendEvent(string name) => SendEvent(name, SyncNode.WorldObjectId);

    public void WriteLog(Logger.LogType type, string component, string message)
    {
        Logger.Write(type, $"{component} (Object {Id})", message);

        logEntries.Enqueue(new Logger.LogEntry(type, component, message));
        if (logEntries.Count > Settings.Instance.ObjectLogSize)
        {
            logEntries.Dequeue();
        }
    }

    public void WriteLog(string component, string message) => WriteLog(Logger.LogType.Log, component, message);
    public void WriteDebugLog(string component, string message) => WriteLog(Logger.LogType.Debug, component, message);
    public void WriteErrorLog(string component, string message) => WriteLog(Logger.LogType.Error, component, message);

    internal void HandleAudio(float[] data)
    {
        AudioReceived?.Invoke(data);
    }

    internal void ProcessBeforeSync(float dt)
    {
        BeforeSync?.Invoke(this, dt);
    }

    internal void ProcessAfterSync(float dt)
    {
        AfterSync?.Invoke(this, dt);

        // Set behavior based on tags
        if (GetField("tags") is Sequence tagsSeq)
        {
            foreach (var elem in tagsSeq.Elements)
            {
                if (!(elem is Primitive<string>)) continue;
                string tagName = ((Primitive<string>)elem).Value;
                
                if (!tags.ContainsKey(tagName))
                {
                    ITag tag = Node.CreateTag(tagName, this);
                    if (tag == null) continue;  // if error, the creator returns null
                    tags[tagName] = tag;
                    tag.Setup(this);
                    WriteDebugLog("Object", $"Tag {tagName} has been added");
                }
                // TODO: handle deleted tags?
            }
        }

        if (GetField("codes") is Sequence codesSeq)
        {
            foreach (var elem in codesSeq.Elements)
            {
                if (elem is BlobHandle codeHandle && !codes.ContainsKey(codeHandle))
                {
                    // new code is added
                    codes[codeHandle] = new ObjectWasmRunner(this);   // TODO: dispose
                    Node.ReadBlob(codeHandle).ContinueWith(task => {
                        var runner = codes[codeHandle];
                        try
                        {
                            runner.Load(task.Result.Data);
                            runner.RegisterHandlers();
                            runner.Initialize();
                        }
                        catch (Exception e)
                        {
                            WriteErrorLog("SyncObject", "WASM init error " + e);
                        }
                    });
                }
            }
        }
    }

    // Calculate world coordinate of the object
    // Note: when return value is false (failed), position and rotation will be null (because these are reference type)
    public bool CalcWorldCoord(out Vec position, out Quat rotation) => CalcWorldCoord(this, out position, out rotation);

    static bool CalcWorldCoord(SyncObject obj, out Vec position, out Quat rotation, int depth = 0)
    {
        if (obj.TryGetField("position", out Vec pos) && obj.TryGetField("rotation", out Quat rot))
        {
            if (obj.TryGetField("parent", out ObjectRef parent))
            {
                // Recursively calculate world coord of parent
                // (with recursion depth limit)
                if (depth < 20 && CalcWorldCoord(obj.Node.Objects[parent.Id], out Vec parentPos, out Quat parentRot, depth + 1))
                {
                    position = parentPos + parentRot * pos;
                    rotation = parentRot * rot;
                    return true;
                }
                else
                {
                    position = default;
                    rotation = default;
                    return false;
                }
            }
            else
            {
                // No parent
                position = pos;
                rotation = rot;
                return true;
            }
        }
        else
        {
            position = default;
            rotation = default;
            return false;
        }
    }

    // Calculate coordinate of the object relative to refObj
    // Note: when return value is false (failed), position and rotation will be null (because these are reference type)
    public bool CalcRelativeCoord(SyncObject refObj, out Vec position, out Quat rotation) => CalcRelativeCoord(this, refObj, out position, out rotation);

    static bool CalcRelativeCoord(SyncObject obj, SyncObject refObj, out Vec position, out Quat rotation)
    {
        position = default;
        rotation = default;

        // Calculate world coordinate of obj
        Vec worldPos;
        Quat worldRot;
        if (!CalcWorldCoord(obj, out worldPos, out worldRot)) return false;

        // Calculate world coordinate of refObj
        Vec refWorldPos;
        Quat refWorldRot;
        if (!CalcWorldCoord(refObj, out refWorldPos, out refWorldRot)) return false;

        // Calculate relative coordinate
        position = refWorldRot.Conjugate() * (worldPos - refWorldPos);
        rotation = refWorldRot.Conjugate() * worldRot;
        return true;
    }

    public struct Field
    {
        public IValue Value;
        public uint LastUpdatedTick;
    }
}
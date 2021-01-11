using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using WebAssembly;
using WebAssembly.Runtime;

public class ObjectWasmRunner : WasmRunner
{
    public SyncObject Object;

    public List<IValue> valueList = new List<IValue>();

    ConcurrentQueue<uint> createdObjects = new ConcurrentQueue<uint>();

    const int Success = 0, Failure = -1;

    public ObjectWasmRunner(SyncObject obj)
    {
        // Object manipulation functions
        AddImportFunction("mondeto", "request_new_object", (Action)RequestNewObject);
        AddImportFunction("mondeto", "get_new_object", (Func<long>)GetNewObject);
        AddImportFunction("mondeto", "get_object_id", (Func<int>)GetObjectId);
        AddImportFunction("mondeto", "object_is_original", (Func<int, int>)ObjectIsOriginal);
        AddImportFunction("mondeto", "delete_self", (Action)DeleteSelf);
        // Field manipulation functions
        AddImportFunction("mondeto", "get_field", (Func<int, int, long>)GetField);
        AddImportFunction("mondeto", "set_field", (Action<int, int, int>)SetField);
        AddImportFunction("mondeto", "object_get_field", (Func<int, int, int, long>)ObjectGetField);
        AddImportFunction("mondeto", "object_set_field", (Func<int, int, int, int, int>)ObjectSetField);
        // IValue-related functions
        AddImportFunction("mondeto", "get_type", (Func<int, int>)GetValueType);
        AddImportFunction("mondeto", "read_vec", (Action<int, int, int, int>)ReadVec);
        AddImportFunction("mondeto", "read_quat", (Action<int, int, int, int, int>)ReadQuat);
        AddImportFunction("mondeto", "read_int", (Func<int, int>)ReadPrimitive<int>);
        AddImportFunction("mondeto", "read_long", (Func<int, long>)ReadPrimitive<long>);
        AddImportFunction("mondeto", "read_float", (Func<int, float>)ReadPrimitive<float>);
        AddImportFunction("mondeto", "read_double", (Func<int, double>)ReadPrimitive<double>);
        AddImportFunction("mondeto", "get_string_length", (Func<int, int>)GetStringLength);
        AddImportFunction("mondeto", "read_string", (Func<int, int, int, int>)ReadString);
        AddImportFunction("mondeto", "read_object_ref", (Func<int, int>)ReadObjectRef);
        AddImportFunction("mondeto", "make_int", (Func<int, int>)MakePrimitive<int>);
        AddImportFunction("mondeto", "make_long", (Func<long, int>)MakePrimitive<long>);
        AddImportFunction("mondeto", "make_float", (Func<float, int>)MakePrimitive<float>);
        AddImportFunction("mondeto", "make_double", (Func<double, int>)MakePrimitive<double>);
        AddImportFunction("mondeto", "make_vec", (Func<float, float, float, int>)MakeVec);
        AddImportFunction("mondeto", "make_quat", (Func<float, float, float, float, int>)MakeQuat);
        AddImportFunction("mondeto", "make_string", (Func<int, int, int>)MakeString);
        AddImportFunction("mondeto", "make_sequence", (Func<int, int, int>)MakeSequence);
        AddImportFunction("mondeto", "make_object_ref", (Func<int, int>)MakeObjectRef);
        // Event-related functions
        AddImportFunction("mondeto", "send_event", (Func<int, int, int, int, int, int, int>)SendEvent);
        // Other functions
        AddImportFunction("mondeto", "get_world_coordinate", (Func<int, int, int, int, int, int, int, int, int>)GetWorldCoordinate);
        AddImportFunction("mondeto", "write_audio", (Action<int, int>)WriteAudio);

        Object = obj;
    }

    public void RegisterHandlers()
    {
        // Search event handlers
        foreach (var export in Module.Exports.Where(ex => ex.Kind == WebAssembly.ExternalKind.Function))
        {
            // Event handlers should have names like handle_EVENTNAME
            if (!export.Name.StartsWith("handle_")) continue;
            // TODO: signature check (event handlers should have signatures "void handle_EVENTNAME(i32 sender)")

            string funcName = export.Name;
            string eventName = export.Name.Substring("handle_".Length);
            // Register as a handler
            // TODO: unregister
            Object.RegisterEventHandler(eventName, (sender, args) => {
                CallWasmFuncWithExceptionHandling(funcName, (int)sender);
            });
        }

        // Search "update" function
        WebAssembly.Export updateExport = FindExport("update", WebAssembly.ExternalKind.Function);
        if (updateExport != null)
        {
            // TODO: signature check (update should have "void update(f32 dt)")
            Object.BeforeSync += CallUpdateFunction;
        }
    }

    uint RegisterValue(IValue value)
    {
        valueList.Add(value);
        return (uint)(valueList.Count - 1);
    }

    IValue FindValue(uint valueId)
    {
        // For debug. TODO: move to better error handling
        if (!IsValueIdValid(valueId))
        {
            throw new ArgumentException($"Invalid value ID {valueId} (max={valueList.Count})");
        }
        return valueList[(int)valueId];
    }

    bool IsValueIdValid(uint valueId)
    {
        return valueId < valueList.Count;
    }

    protected override void AfterCall()
    {
        base.AfterCall();

        valueList.Clear();
    }

    void CallUpdateFunction(SyncObject obj, float dt)
    {
        CallWasmFuncWithExceptionHandling("update", dt);
    }

    void CallWasmFuncWithExceptionHandling(string name, params object[] args)   // TODO: rename
    {
        if (!IsReady) return;

        try
        {
            CallWasmFunc(name, args);
        }
        catch (Exception e)
        {
            Object.WriteErrorLog("ObjectWasmRunner", e.ToString());
        }
    }

    // void request_new_object()
    void RequestNewObject()
    {
        Object.Node.CreateObject().ContinueWith(task => {
            uint objId = task.Result;
            createdObjects.Enqueue(objId);
        });
    }

    // i64 get_new_object()
    long GetNewObject()
    {
        if (createdObjects.TryDequeue(out uint objId))
        {
            return objId;
        }
        else
        {
            return -1;  // new object is not ready
        }
    }

    // i32 get_object_id()
    int GetObjectId()
    {
        return (int)Object.Id;
    }

    // i32 object_is_original(i32 obj_id)
    int ObjectIsOriginal(int objId)
    {
        if (Object.Node.Objects.TryGetValue((uint)objId, out SyncObject obj))
        {
            return (obj.OriginalNodeId == Object.Node.NodeId) ? 1 : 0;
        }
        else
        {
            return 0;   // object not found TODO: should throw an exception?
        }
    }

    // void delete_self()
    void DeleteSelf()
    {
        // Stop further WASM execution even if the object is not original
        IsReady = false;

        Object.Node.DeleteObject(Object.Id);
    }

    // i64 get_field(i32 name_ptr, i32 name_len)
    long GetField(int namePtr, int nameLen)
    {
        // Read field name from WASM memory
        string name = ReadStringFromWasm(Instance.Exports.memory, namePtr, nameLen);
        
        if (Object.TryGetField<IValue>(name, out IValue value))
        {
            return RegisterValue(value);
        }
        else
        {
            return -1;
        }
    }

    // i64 object_get_field(i32 obj_id, i32 name_ptr, i32 name_len)
    long ObjectGetField(int objId, int namePtr, int nameLen)
    {
        if (!Object.Node.Objects.ContainsKey((uint)objId))
        {
            return -1;  // object not found
        }

        string name = ReadStringFromWasm(Instance.Exports.memory, namePtr, nameLen);
        if (Object.Node.Objects[(uint)objId].TryGetField<IValue>(name, out IValue value))
        {
            return RegisterValue(value);
        }
        else
        {
            return -1;  // field not found
        }
    }

    // void set_field(i32 name_ptr, i32 name_len, i32 value_id)
    void SetField(int namePtr, int nameLen, int valueId)
    {
        string name = ReadStringFromWasm(Instance.Exports.memory, namePtr, nameLen);
        // TODO: error check
        Object.SetField(name, FindValue((uint)valueId));
    }
    
    // i32 object_set_field(i32 obj_id, i32 name_ptr, i32 name_len, i32 value_id)
    int ObjectSetField(int objId, int namePtr, int nameLen, int valueId)
    {
        if (!Object.Node.Objects.ContainsKey((uint)objId))
        {
            return Failure;  // object not found
        }
        if (!IsValueIdValid((uint)valueId))
        {
            return Failure; // invalid value id
        }

        string name = ReadStringFromWasm(Instance.Exports.memory, namePtr, nameLen);
        Object.Node.Objects[(uint)objId].SetField(name, FindValue((uint)valueId));
        return Success;
    }

    // i32 get_type(i32 value_id)
    int GetValueType(int valueId)
    {
        // TODO: error check
        return (int)FindValue((uint)valueId).Type;
    }

    // void read_vec(i32 value_id, i32 x_ptr, i32 y_ptr, i32 z_ptr)
    void ReadVec(int valueId, int xPtr, int yPtr, int zPtr)
    {
        var vec = (Vec)FindValue((uint)valueId);
        WriteVecToWasm(vec, Instance.Exports.memory, xPtr, yPtr, zPtr);
    }

    // void read_quat(i32 value_id, i32 w_ptr, i32 x_ptr, i32 y_ptr, i32 z_ptr)
    void ReadQuat(int valueId, int wPtr, int xPtr, int yPtr, int zPtr)
    {
        var quat = (Quat)FindValue((uint)valueId);
        WriteQuatToWasm(quat, Instance.Exports.memory, wPtr, xPtr, yPtr, zPtr);
    }

    // i32 reaed_int(i32 value_id)
    // i64 reaed_long(i32 value_id)
    // f32 reaed_float(i32 value_id)
    // f64 reaed_double(i32 value_id)
    T ReadPrimitive<T>(int valueId)
    {
        // TODO: error check
        var val = (Primitive<T>)FindValue((uint)valueId);
        return val.Value;
    }

    // i32 get_string_length(i32 value_id)
    int GetStringLength(int valueId)
    {
        var val = (Primitive<string>)FindValue((uint)valueId);
        return Encoding.UTF8.GetByteCount(val.Value);
    }

    // i32 read_string(i32 value_id, i32 ptr, i32 max_len)
    int ReadString(int valueId, int ptr, int maxLen)
    {
        var val = (Primitive<string>)FindValue((uint)valueId);
        byte[] bytes = Encoding.UTF8.GetBytes(val.Value);

        int len = Math.Min(bytes.Length, maxLen);
        Marshal.Copy(bytes, 0, WasmToIntPtr(Instance.Exports.memory, ptr), len);

        return len;
    }

    // i32 read_object_ref(i32 value_id)
    int ReadObjectRef(int valueId)
    {
        var val = (ObjectRef)FindValue((uint)valueId);
        return (int)val.Id;
    }

    // i32 make_int(i32 value)
    // i32 make_long(i64 value)
    // i32 make_float(f32 value)
    // i32 make_double(f64 value)
    int MakePrimitive<T>(T value)
    {
        var primitive = new Primitive<T>(value);
        return (int)RegisterValue(primitive);
    }

    // i32 make_vec(f32 x, f32 y, f32 z)
    int MakeVec(float x, float y, float z)
    {
        var vec = new Vec(x, y, z);
        return (int)RegisterValue(vec);
    }

    // i32 make_quat(f32 w, f32 x, f32 y, f32 z)
    int MakeQuat(float w, float x, float y, float z)
    {
        var quat = new Quat(w, x, y, z);
        return (int)RegisterValue(quat);
    }

    // i32 make_string(i32 ptr, i32 len)
    int MakeString(int ptr, int len)
    {
        string str = ReadStringFromWasm(Instance.Exports.memory, ptr, len);

        return (int)RegisterValue(new Primitive<string>(str));
    }

    // i32 make_sequence(i32 elems_ptr, i32 elems_len)
    int MakeSequence(int elemsPtr, int elemsLen)
    {
        // read value ID array
        uint[] valueIds = ReadUIntArrayFromWasm(Instance.Exports.memory, elemsPtr, elemsLen);

        // TODO: error handling of invalid value ID
        List<IValue> elems = valueIds.Select(vid => FindValue(vid)).ToList();
        return (int)RegisterValue(new Sequence(elems));
    }

    // i32 make_object_ref(i32 obj_id)
    int MakeObjectRef(int objId)
    {
        return (int)RegisterValue(new ObjectRef { Id = (uint)objId });
    }
    
    // i32 send_event(i32 receiver_id, i32 name_ptr, i32 name_len, i32 args_ptr, i32 args_len, i32 local_only)
    int SendEvent(int receiverId, int namePtr, int nameLen, int argsPtr, int argsLen, int localOnly)
    {
        if (Object.Node.Objects.TryGetValue((uint)receiverId, out SyncObject receiver))
        {
            string name = ReadStringFromWasm(Instance.Exports.memory, namePtr, nameLen);
            uint[] argValueIds = ReadUIntArrayFromWasm(Instance.Exports.memory, argsPtr, argsLen);

            // TODO: error handling of invalid value ID
            IValue[] args = argValueIds.Select(vid => FindValue(vid)).ToArray();

            receiver.SendEvent(name, Object.Id, args, localOnly != 0);

            return Success;
        }
        else
        {
            return Failure; // Cannot find receiving object
        }
    }

    // i32 get_world_coordinate(i32 obj_id, i32 vx_ptr, i32 vy_ptr, i32 vz_ptr, i32 qw_ptr, i32 qx_ptr, i32 qy_ptr, i32 qz_ptr)
    int GetWorldCoordinate(int objId, int vxPtr, int vyPtr, int vzPtr, int qwPtr, int qxPtr, int qyPtr, int qzPtr)
    {
        // Check object ID
        if (!Object.Node.Objects.ContainsKey((uint)objId)) return Failure;

        Vec worldPos;
        Quat worldRot;
        if (Object.CalcWorldCoord(out worldPos, out worldRot))
        {
            WriteVecToWasm(worldPos, Instance.Exports.memory, vxPtr, vyPtr, vzPtr);
            WriteQuatToWasm(worldRot, Instance.Exports.memory, qwPtr, qxPtr, qyPtr, qzPtr);

            return Success;
        }
        else
        {
            return Failure;
        }
    }

    // i32 write_audio(i32 ptr, i32 len)
    void WriteAudio(int ptr, int len)
    {
        IntPtr samplesPtr = WasmToIntPtr(Instance.Exports.memory, ptr);
        var samples = new float[len];
        Marshal.Copy(samplesPtr, samples, 0, len);
        
        Object.WriteAudio(samples);
    }

    bool WriteVecToWasm(Vec vec, UnmanagedMemory memory, int xPtr, int yPtr, int zPtr)
    {
        if (!(CheckWasmPtr(memory, xPtr) && CheckWasmPtr(memory, yPtr) && CheckWasmPtr(memory, zPtr)))
        {
            return false;
        }

        var xyz = new float[] { vec.X, vec.Y, vec.Z };
        Marshal.Copy(xyz, 0, WasmToIntPtr(memory, xPtr), 1);
        Marshal.Copy(xyz, 1, WasmToIntPtr(memory, yPtr), 1);
        Marshal.Copy(xyz, 2, WasmToIntPtr(memory, zPtr), 1);
        return true;
    }

    bool WriteQuatToWasm(Quat quat, UnmanagedMemory memory, int wPtr, int xPtr, int yPtr, int zPtr)
    {
        if (!(CheckWasmPtr(memory, wPtr) && CheckWasmPtr(memory, xPtr) && CheckWasmPtr(memory, yPtr) && CheckWasmPtr(memory, zPtr)))
        {
            return false;
        }

        var wxyz = new float[] { quat.W, quat.X, quat.Y, quat.Z };
        Marshal.Copy(wxyz, 0, WasmToIntPtr(memory, wPtr), 1);
        Marshal.Copy(wxyz, 1, WasmToIntPtr(memory, xPtr), 1);
        Marshal.Copy(wxyz, 2, WasmToIntPtr(memory, yPtr), 1);
        Marshal.Copy(wxyz, 3, WasmToIntPtr(memory, zPtr), 1);

        return true;
    }

    uint[] ReadUIntArrayFromWasm(UnmanagedMemory memory, int elemsPtr, int elemsLen)
    {
        // boundary check
        if (elemsPtr < 0 || elemsLen + sizeof(int) * elemsLen >= memory.Size) throw new Exception();  // TODO: change exception type
        // read array (read as int[] because Marshal.Copy does not support uint[])
        var intArray = new int[elemsLen];
        Marshal.Copy(WasmToIntPtr(memory, elemsPtr), intArray, 0, elemsLen);
        return intArray.Select(intValue => (uint)intValue).ToArray();
    }

    public override void WriteLog(Logger.LogType type, string component, string message)
    {
        Object.WriteLog(type, component, message);
    }

    string ReadStringFromWasm(UnmanagedMemory memory, int ptr, int len)
    {
        // boundary check
        if (ptr < 0 || ptr + len >= memory.Size) throw new Exception();  // TODO: change exception
        
        unsafe
        {
            return Encoding.UTF8.GetString((byte*)WasmToIntPtr(memory, ptr), len);
        }
    }

    public override void Dispose()
    {
        base.Dispose();
    }
}
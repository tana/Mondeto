using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using WasmerSharp;

public class ObjectWasmRunner : WasmRunner
{
    public SyncObject Object;

    public List<IValue> valueList = new List<IValue>();

    Queue<(Logger.LogType, string, string)> logQueue = new Queue<(Logger.LogType, string, string)>();

    ConcurrentQueue<uint> createdObjects = new ConcurrentQueue<uint>();

    const int Success = 0, Failure = -1;

    public ObjectWasmRunner(SyncObject obj)
    {
        // Object manipulation functions
        AddImportFunction("mondeto", "request_new_object", (Action<InstanceContext>)RequestNewObject);
        AddImportFunction("mondeto", "get_new_object", (Func<InstanceContext, long>)GetNewObject);
        // Field manipulation functions
        AddImportFunction("mondeto", "get_field", (Func<InstanceContext, int, int, long>)GetField);
        AddImportFunction("mondeto", "set_field", (Action<InstanceContext, int, int, int>)SetField);
        AddImportFunction("mondeto", "object_get_field", (Func<InstanceContext, int, int, int, long>)ObjectGetField);
        AddImportFunction("mondeto", "object_set_field", (Func<InstanceContext, int, int, int, int, int>)ObjectSetField);
        // IValue-related functions
        AddImportFunction("mondeto", "get_type", (Func<InstanceContext, int, int>)GetValueType);
        AddImportFunction("mondeto", "get_vec", (Action<InstanceContext, int, int, int, int>)GetVec);
        AddImportFunction("mondeto", "get_quat", (Action<InstanceContext, int, int, int, int, int>)GetQuat);
        AddImportFunction("mondeto", "get_int", (Func<InstanceContext, int, int>)GetPrimitive<int>);
        AddImportFunction("mondeto", "get_long", (Func<InstanceContext, int, long>)GetPrimitive<long>);
        AddImportFunction("mondeto", "get_float", (Func<InstanceContext, int, float>)GetPrimitive<float>);
        AddImportFunction("mondeto", "get_double", (Func<InstanceContext, int, double>)GetPrimitive<double>);
        AddImportFunction("mondeto", "get_string_length", (Func<InstanceContext, int, int>)GetStringLength);
        AddImportFunction("mondeto", "get_string", (Func<InstanceContext, int, int, int, int>)GetString);
        AddImportFunction("mondeto", "make_int", (Func<InstanceContext, int, int>)MakePrimitive<int>);
        AddImportFunction("mondeto", "make_long", (Func<InstanceContext, long, int>)MakePrimitive<long>);
        AddImportFunction("mondeto", "make_float", (Func<InstanceContext, float, int>)MakePrimitive<float>);
        AddImportFunction("mondeto", "make_double", (Func<InstanceContext, double, int>)MakePrimitive<double>);
        AddImportFunction("mondeto", "make_vec", (Func<InstanceContext, float, float, float, int>)MakeVec);
        AddImportFunction("mondeto", "make_quat", (Func<InstanceContext, float, float, float, float, int>)MakeQuat);
        AddImportFunction("mondeto", "make_string", (Func<InstanceContext, int, int, int>)MakeString);

        Object = obj;

        Object.AfterSync += OutputLogs;
    }

    public void RegisterHandlers()
    {
        // Search event handlers
        foreach (var export in module.Exports.Where(ex => ex.Kind == WebAssembly.ExternalKind.Function))
        {
            // Event handlers should have names like handle_EVENTNAME
            if (!export.Name.StartsWith("handle_")) continue;
            // TODO: signature check (event handlers should have signatures "void handle_EVENTNAME(i32 sender)")

            string funcName = export.Name;
            string eventName = export.Name.Substring("handle_".Length);
            // Register as a handler
            // TODO: unregister
            Object.RegisterEventHandler(eventName, (sender, args) => {
                CallWasmFunc(funcName, (int)sender);
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
        CallWasmFunc("update", dt);
    }

    // void request_new_object()
    void RequestNewObject(InstanceContext ctx)
    {
        Object.Node.CreateObject().ContinueWith(task => {
            uint objId = task.Result;
            createdObjects.Enqueue(objId);
        });
    }

    // i64 get_new_object()
    long GetNewObject(InstanceContext ctx)
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

    // i64 get_field(i32 name_ptr, i32 name_len)
    long GetField(InstanceContext ctx, int namePtr, int nameLen)
    {
        // Get memory from InstanceContext
        //  https://migueldeicaza.github.io/WasmerSharp/api/WasmerSharp/WasmerSharp.InstanceContext.html
        Memory memory = ctx.GetMemory(0);

        // Read field name from WASM memory
        string name = ReadStringFromWasm(memory, namePtr, nameLen);
        
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
    long ObjectGetField(InstanceContext ctx, int objId, int namePtr, int nameLen)
    {
        Memory memory = ctx.GetMemory(0);

        if (!Object.Node.Objects.ContainsKey((uint)objId))
        {
            return -1;  // object not found
        }

        string name = ReadStringFromWasm(memory, namePtr, nameLen);
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
    void SetField(InstanceContext ctx, int namePtr, int nameLen, int valueId)
    {
        Memory memory = ctx.GetMemory(0);

        string name = ReadStringFromWasm(memory, namePtr, nameLen);
        // TODO: error check
        Object.SetField(name, FindValue((uint)valueId));
    }
    
    // i32 object_set_field(i32 obj_id, i32 name_ptr, i32 name_len, i32 value_id)
    int ObjectSetField(InstanceContext ctx, int objId, int namePtr, int nameLen, int valueId)
    {
        Memory memory = ctx.GetMemory(0);

        if (!Object.Node.Objects.ContainsKey((uint)objId))
        {
            return Failure;  // object not found
        }
        if (!IsValueIdValid((uint)valueId))
        {
            return Failure; // invalid value id
        }

        string name = ReadStringFromWasm(memory, namePtr, nameLen);
        Object.Node.Objects[(uint)objId].SetField(name, FindValue((uint)valueId));
        return Success;
    }

    // i32 get_type(i32 value_id)
    int GetValueType(InstanceContext ctx, int valueId)
    {
        // TODO: error check
        return (int)FindValue((uint)valueId).Type;
    }

    // void get_vec(i32 value_id, i32 x_ptr, i32 y_ptr, i32 z_ptr)
    void GetVec(InstanceContext ctx, int valueId, int xPtr, int yPtr, int zPtr)
    {
        Memory memory = ctx.GetMemory(0);

        // TODO: error check and boundary check
        var vec = (Vec)FindValue((uint)valueId);
        var xyz = new float[] { vec.X, vec.Y, vec.Z };
        Marshal.Copy(xyz, 0, WasmToIntPtr(memory, xPtr), 1);
        Marshal.Copy(xyz, 1, WasmToIntPtr(memory, yPtr), 1);
        Marshal.Copy(xyz, 2, WasmToIntPtr(memory, zPtr), 1);
    }

    // void get_quat(i32 value_id, i32 w_ptr, i32 x_ptr, i32 y_ptr, i32 z_ptr)
    void GetQuat(InstanceContext ctx, int valueId, int wPtr, int xPtr, int yPtr, int zPtr)
    {
        Memory memory = ctx.GetMemory(0);

        // TODO: error check and boundary check
        var quat = (Quat)FindValue((uint)valueId);
        var wxyz = new float[] { quat.W, quat.X, quat.Y, quat.Z };
        Marshal.Copy(wxyz, 0, WasmToIntPtr(memory, wPtr), 1);
        Marshal.Copy(wxyz, 1, WasmToIntPtr(memory, xPtr), 1);
        Marshal.Copy(wxyz, 2, WasmToIntPtr(memory, yPtr), 1);
        Marshal.Copy(wxyz, 3, WasmToIntPtr(memory, zPtr), 1);
    }

    // i32 get_int(i32 value_id)
    // i64 get_long(i32 value_id)
    // f32 get_float(i32 value_id)
    // f64 get_double(i32 value_id)
    T GetPrimitive<T>(InstanceContext ctx, int valueId)
    {
        // TODO: error check
        var val = (Primitive<T>)FindValue((uint)valueId);
        return val.Value;
    }

    // i32 get_string_length(i32 value_id)
    int GetStringLength(InstanceContext ctx, int valueId)
    {
        var val = (Primitive<string>)FindValue((uint)valueId);
        return Encoding.UTF8.GetByteCount(val.Value);
    }

    // i32 get_string(i32 value_id, i32 ptr, i32 max_len)
    int GetString(InstanceContext ctx, int valueId, int ptr, int maxLen)
    {
        Memory memory = ctx.GetMemory(0);

        var val = (Primitive<string>)FindValue((uint)valueId);
        byte[] bytes = Encoding.UTF8.GetBytes(val.Value);

        int len = Math.Min(bytes.Length, maxLen);
        Marshal.Copy(bytes, 0, WasmToIntPtr(memory, ptr), len);

        return len;
    }

    // i32 make_int(i32 value)
    // i32 make_long(i64 value)
    // i32 make_float(f32 value)
    // i32 make_double(f64 value)
    int MakePrimitive<T>(InstanceContext ctx, T value)
    {
        var primitive = new Primitive<T>(value);
        return (int)RegisterValue(primitive);
    }

    // i32 make_vec(f32 x, f32 y, f32 z)
    int MakeVec(InstanceContext ctx, float x, float y, float z)
    {
        var vec = new Vec(x, y, z);
        return (int)RegisterValue(vec);
    }

    // i32 make_quat(f32 w, f32 x, f32 y, f32 z)
    int MakeQuat(InstanceContext ctx, float w, float x, float y, float z)
    {
        var quat = new Quat(w, x, y, z);
        return (int)RegisterValue(quat);
    }

    // i32 make_string(i32 ptr, i32 len)
    int MakeString(InstanceContext ctx, int ptr, int len)
    {
        Memory memory = ctx.GetMemory(0);
        
        string str = ReadStringFromWasm(memory, ptr, len);

        return (int)RegisterValue(new Primitive<string>(str));
    }

    public override void WriteLog(Logger.LogType type, string component, string message)
    {
        logQueue.Enqueue((type, component, message));
    }

    void OutputLogs(SyncObject obj, float dt)
    {
        while (logQueue.Count > 0)
        {
            var (type, component, message) = logQueue.Dequeue();
            obj.WriteLog(type, component, message);
        }
    }

    string ReadStringFromWasm(Memory memory, int ptr, int len)
    {
        // boundary check
        if (ptr < 0 || ptr + len >= memory.DataLength) throw new Exception();  // TODO: change exception
        
        unsafe
        {
            return Encoding.UTF8.GetString((byte*)WasmToIntPtr(memory, ptr), len);
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        Object.AfterSync -= OutputLogs;
    }
}
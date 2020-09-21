using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using WasmerSharp;

public class ObjectWasmRunner : WasmRunner
{
    public SyncObject Object;

    public List<IValue> valueList = new List<IValue>();

    public ObjectWasmRunner(SyncObject obj)
    {
        AddImportFunction("mondeto", "get_field", (Func<InstanceContext, int, int, long>)GetField);
        AddImportFunction("mondeto", "get_type", (Func<InstanceContext, int, int>)GetValueType);
        AddImportFunction("mondeto", "decomp_vec", (Action<InstanceContext, int, int, int, int>)DecompVec);
        AddImportFunction("mondeto", "get_int", (Func<InstanceContext, int, int>)GetInt);
        AddImportFunction("mondeto", "get_long", (Func<InstanceContext, int, long>)GetLong);
        AddImportFunction("mondeto", "get_float", (Func<InstanceContext, int, float>)GetFloat);
        AddImportFunction("mondeto", "get_double", (Func<InstanceContext, int, double>)GetDouble);
        AddImportFunction("mondeto", "get_string_length", (Func<InstanceContext, int, int>)GetStringLength);
        AddImportFunction("mondeto", "get_string", (Func<InstanceContext, int, int, int, int>)GetString);

        Object = obj;
    }

    public void RegisterEventHandlers()
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

    protected override void AfterCall()
    {
        base.AfterCall();

        valueList.Clear();
    }

    // i64 get_field(i32 name_ptr, i32 name_len)
    long GetField(InstanceContext ctx, int namePtr, int nameLen)
    {
        // Get memory from InstanceContext
        //  https://migueldeicaza.github.io/WasmerSharp/api/WasmerSharp/WasmerSharp.InstanceContext.html
        Memory memory = ctx.GetMemory(0);

        // Read field name from WASM memory
        byte[] buf = new byte[nameLen];
        // TODO: memory boundary check
        Marshal.Copy(WasmToIntPtr(memory, namePtr), buf, 0, nameLen);
        string name = Encoding.UTF8.GetString(buf);
        
        if (Object.TryGetField<IValue>(name, out IValue value))
        {
            return RegisterValue(value);
        }
        else
        {
            return -1;
        }
    }

    // i32 get_type(i32 value_id)
    int GetValueType(InstanceContext ctx, int valueId)
    {
        // TODO: error check
        return (int)FindValue((uint)valueId).Type;
    }

    // void decomp_vec(i32 value_id, i32 x_ptr, i32 y_ptr, i32 z_ptr)
    void DecompVec(InstanceContext ctx, int valueId, int xPtr, int yPtr, int zPtr)
    {
        Memory memory = ctx.GetMemory(0);

        // TODO: error check and boundary check
        var vec = (Vec)FindValue((uint)valueId);
        var xyz = new float[] { vec.X, vec.Y, vec.Z };
        Marshal.Copy(xyz, 0, WasmToIntPtr(memory, xPtr), 1);
        Marshal.Copy(xyz, 1, WasmToIntPtr(memory, yPtr), 1);
        Marshal.Copy(xyz, 2, WasmToIntPtr(memory, zPtr), 1);
    }

    // i32 get_int(i32 value_id)
    int GetInt(InstanceContext ctx, int valueId)
    {
        // TODO: error check
        var val = (Primitive<int>)FindValue((uint)valueId);
        return val.Value;
    }

    // i64 get_long(i32 value_id)
    long GetLong(InstanceContext ctx, int valueId)
    {
        // TODO: error check
        var val = (Primitive<long>)FindValue((uint)valueId);
        return val.Value;
    }

    // f32 get_float(i32 value_id)
    float GetFloat(InstanceContext ctx, int valueId)
    {
        // TODO: error check
        var val = (Primitive<float>)FindValue((uint)valueId);
        return val.Value;
    }

    // f64 get_double(i32 value_id)
    double GetDouble(InstanceContext ctx, int valueId)
    {
        // TODO: error check
        var val = (Primitive<double>)FindValue((uint)valueId);
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

    public override void WriteLog(Logger.LogType type, string component, string message)
    {
        Object.WriteLog(type, component, message);
    }
}
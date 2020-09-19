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
}
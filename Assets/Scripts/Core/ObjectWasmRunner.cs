using System;
using System.Linq;
using WasmerSharp;
using WebAssembly;

public class ObjectWasmRunner : WasmRunner
{
    public SyncObject Object;

    public ObjectWasmRunner(SyncObject obj)
    {
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
                //CallWasmFunc(func, new object[] { (int)sender });
                instance.Call(funcName, (int)sender);
            });
        }
    }
}
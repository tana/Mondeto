using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using WasmerSharp;

// A class for compiling and running WASM using WasmerSharp
// For usage of WasmerSharp library:
//  https://migueldeicaza.github.io/WasmerSharp/articles/intro.html
// However, dotnet-webassembly library is also used.
//  https://github.com/RyanLamansky/dotnet-webassembly
public class WasmRunner : IDisposable
{
    public SyncObject Object;
    public bool IsReady { get; private set; }

    WebAssembly.Module module;

    Instance instance;
    Import[] imports;

    List<byte> outBuf = new List<byte>();

    Delegate fdWriteDelegate, procExitDelegate;

    public WasmRunner(SyncObject obj)
    {
        // WasmerSharp uses Marshal.GetFunctionPointerForDelegate.
        //  ( https://github.com/migueldeicaza/WasmerSharp/blob/0f168586501cd9a22800c1b447f2625d0dbfbea3/WasmerSharp/Wasmer.cs#L1164 )
        // When passing delegates to native codes using GetFunctionPointerForDelegate,
        // it is necessary to store delegates in somewhere in order to prevent deletion by GC.
        // See:
        //  https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.marshal.getfunctionpointerfordelegate?view=netcore-3.1
        //  https://stackoverflow.com/a/4907039
        fdWriteDelegate = (Func<InstanceContext, int, int, int, int, int>)(WasiFdWrite);
        procExitDelegate = (Action<InstanceContext, int>)(WasiProcExit);

        // Prepare external (C#) functions
        // Imports are specified as an array
        //  https://migueldeicaza.github.io/WasmerSharp/api/WasmerSharp/WasmerSharp.Instance.html
        imports = new Import[] {
            // WASI-compatible output for printf debugging
            new Import("wasi_snapshot_preview1", "fd_write", new ImportFunction(fdWriteDelegate)),
            // WASI-compatible exit for AssemblyScript
            new Import("wasi_snapshot_preview1", "proc_exit", new ImportFunction(procExitDelegate))
        };

        Object = obj;
    }

    public void Load(byte[] wasmBinary)
    {
        // Analyze WASM using dotnet-webassembly
        using (var stream = new MemoryStream(wasmBinary))
        {
            module = WebAssembly.Module.ReadFromBinary(stream);
        }
        //module.Exports[0].

        // Load WASM from binary
        instance = new Instance(wasmBinary, imports);

        Logger.Debug("WasmRunner", "WASM instance created");
    }

    public void Initialize()
    {
        // Initialization of WASM code
        // In the future, we are planning to support WASI reactors.
        // (See https://github.com/WebAssembly/WASI/blob/master/design/application-abi.md )
        // However, until we support it, we use ABI (especially function names) that is
        // intentionally different from WASI, because our preliminary ABI is incompatible with WASI reactor.
        // (for example, we use "init" instead of WASI "_initialize")

        // Constructors
        var callCtors = FindExport("__wasm_call_ctors", WebAssembly.ExternalKind.Function);
        if (callCtors != null)
        {
            //CallWasmFunc(callCtors.GetExportFunction());
            instance.Call("__wasm_call_ctors");
        }
        // Initialization
        var init = FindExport("init", WebAssembly.ExternalKind.Function);
        if (init == null)
        {
            Logger.Error("WasmRunner", "Cannot find function init");
            return;
        }
        //CallWasmFunc(init.GetExportFunction());
        instance.Call("init");

        Logger.Debug("WasmRunner", "WASM initialized");

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

        IsReady = true;
    }

    WebAssembly.Export FindExport(string name, WebAssembly.ExternalKind kind)
    {
        return module.Exports.FirstOrDefault(export => export.Name == name && export.Kind == kind);
    }

    // FIXME: It does not work
    /*
    // Search exported object by name and kind (function, memory, etc.)
    //  See: https://migueldeicaza.github.io/WasmerSharp/api/WasmerSharp/WasmerSharp.Export.html
    // Returns null (default value of Export type) if not found
    Export FindExport(string name, ImportExportKind kind)
    {
        UnityEngine.Debug.Log(string.Join(" ", instance.Exports.Select(ex => ex.Name)));
        return instance.Exports.FirstOrDefault(ex => ex.Name == name && ex.Kind == kind);
    }
    */

    // FIXME: It does not work
    //  Probably related to: https://github.com/migueldeicaza/WasmerSharp/blob/0f168586501cd9a22800c1b447f2625d0dbfbea3/WasmerSharp/Wasmer.cs#L1244
    /*
    // Call WASM function
    //  https://migueldeicaza.github.io/WasmerSharp/api/WasmerSharp/WasmerSharp.ExportFunction.html
    // Currently return value is not supported
    void CallWasmFunc(ExportFunction func, object[] args)
    {
        // Convert C# objects to Wasmer values
        //  https://migueldeicaza.github.io/WasmerSharp/api/WasmerSharp/WasmerSharp.WasmerValue.html
        var wasmerArgs = args.Select<object, WasmerValue>(val => {
            switch (val)
            {
                case int intVal:
                    return intVal;
                case long longVal:
                    return longVal;
                case float floatVal:
                    return floatVal;
                case double doubleVal:
                    return doubleVal;
                default:
                    return 0;   // TODO:
            }
        }).ToArray();
        var results = new WasmerValue[0];   // FIXME:
        // It seems the return value is success/failure of a call and error is stored in instance.LastError
        // (if it is same as Instance.Call).
        //  See: https://migueldeicaza.github.io/WasmerSharp/api/WasmerSharp/WasmerSharp.Instance.html
        if (!func.Call(wasmerArgs, results)) throw new Exception(instance.LastError);
    }

    void CallWasmFunc(ExportFunction func) => CallWasmFunc(func, new object[] { 0 });
    */

    // WASI-compatible fd_write
    //  https://github.com/WebAssembly/WASI/blob/master/phases/snapshot/docs.md#fd_write
    //  https://github.com/WebAssembly/WASI/blob/master/phases/snapshot/witx/wasi_snapshot_preview1.witx
    //  https://github.com/WebAssembly/WASI/blob/master/phases/snapshot/witx/typenames.witx
    //  https://github.com/bytecodealliance/wasmtime/blob/main/docs/WASI-tutorial.md#web-assembly-text-example
    int WasiFdWrite(InstanceContext ctx, int fd, int ptrIoVecs, int numIoVecs, int ptrNWritten)
    {
        // It seems WASI uses STDIN=0, STDOUT=1, and STDERR=2
        //  https://github.com/WebAssembly/wasi-libc/blob/5a7ba74c1959691d79580a1c3f4d94bca94bab8e/libc-top-half/musl/include/unistd.h#L10-L12
        if (fd != 2)    // Only STDERR is supported
        {
            return 8;   // errno "Bad file descriptor"
        }

        int bytesWritten = 0;

        var stream = new MemoryStream();
        
        // Get memory from InstanceContext
        //  https://migueldeicaza.github.io/WasmerSharp/api/WasmerSharp/WasmerSharp.InstanceContext.html
        Memory memory = ctx.GetMemory(0);

        unsafe
        {
            // Check boundary of WASM memory
            // Memory size (bytes) is acquired from DataLength
            //  See: https://migueldeicaza.github.io/WasmerSharp/api/WasmerSharp/WasmerSharp.Memory.html
            if (ptrIoVecs + numIoVecs * sizeof(WasiCIoVec) >= memory.DataLength)
            {
                throw new Exception();  // TODO:
            }

            var vecs = (WasiCIoVec*)WasmToIntPtr(memory, ptrIoVecs);
            for (int i = 0; i < numIoVecs; i++)
            {
                int size = vecs[i].Size;
                byte[] array = new byte[size];
                Marshal.Copy(WasmToIntPtr(memory, vecs[i].Buf), array, 0, size);
                stream.Write(array, 0, size);
                bytesWritten += size;
            }
        }

        Marshal.WriteInt32(WasmToIntPtr(memory, ptrNWritten), bytesWritten);

        stream.Position = 0;
        int byteInt;
        while ((byteInt = stream.ReadByte()) >= 0)
        {
            byte b = (byte)byteInt;
            // Detect newline and print into logger (assuming output from WASM is UTF-8 and newline is LF)
            if (b == 0x0A)  // ASCII code for LF '\n'
            {
                Logger.Debug("WasmRunner", Encoding.UTF8.GetString(outBuf.ToArray()));
                outBuf.Clear();
            }
            else
            {
                outBuf.Add(b);
            }
        }

        return 0;   // errno: "success"
    }

    // WASI-compatible proc_exit
    //  https://github.com/WebAssembly/WASI/blob/master/phases/snapshot/docs.md#-proc_exitrval-exitcode
    //  https://github.com/WebAssembly/WASI/blob/master/phases/snapshot/docs.md#-exitcode-u32
    void WasiProcExit(InstanceContext ctx, int exitCode)
    {
        IsReady = false;
    }

    IntPtr WasmToIntPtr(Memory memory, int wasmPtr)
    {
        // Memory is accessed using IntPtr
        //  https://migueldeicaza.github.io/WasmerSharp/api/WasmerSharp/WasmerSharp.Memory.html
        return memory.Data + wasmPtr;
    }

    // WASI ciovec
    //  https://github.com/WebAssembly/WASI/blob/master/phases/snapshot/docs.md#-ciovec-struct
    // Big-endian environment is not supported.
    [StructLayout(LayoutKind.Explicit)]
    struct WasiCIoVec
    {
        [FieldOffset(0)]
        public int Buf; // pointer to the buffer
        [FieldOffset(4)]
        public int Size;
    }

    public void Dispose()
    {
        instance.Dispose();
    }
}
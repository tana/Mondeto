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
    public bool IsReady { get; private set; }

    protected WebAssembly.Module module;

    protected Instance instance;

    List<byte> outBuf = new List<byte>();

    Dictionary<(string, string), Delegate> importDelegates = new Dictionary<(string, string), Delegate>();

    public WasmRunner()
    {
        // Prepare external (C#) functions
        // WASI-compatible output for printf debugging
        AddImportFunction("wasi_snapshot_preview1", "fd_write", (Func<InstanceContext, int, int, int, int, int>)WasiFdWrite);
        // WASI-compatible exit for AssemblyScript
        AddImportFunction("wasi_snapshot_preview1", "proc_exit", (Action<InstanceContext, int>)WasiProcExit);
    }

    // Add imported (C#) function
    // Note: this method must be called before Load.
    protected void AddImportFunction(string module, string name, Delegate func)
    {
        // WasmerSharp uses Marshal.GetFunctionPointerForDelegate.
        //  ( https://github.com/migueldeicaza/WasmerSharp/blob/0f168586501cd9a22800c1b447f2625d0dbfbea3/WasmerSharp/Wasmer.cs#L1164 )
        // When passing delegates to native codes using GetFunctionPointerForDelegate,
        // it is necessary to store delegates in somewhere (other than in ImportFunction) in order to prevent deletion by GC.
        // See:
        //  https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.marshal.getfunctionpointerfordelegate?view=netcore-3.1
        //  https://stackoverflow.com/a/4907039
        importDelegates[(module, name)] = func;
    }

    public void Load(byte[] wasmBinary)
    {
        // Analyze WASM using dotnet-webassembly
        using (var stream = new MemoryStream(wasmBinary))
        {
            module = WebAssembly.Module.ReadFromBinary(stream);
        }

        // Imports are specified as an array
        //  https://migueldeicaza.github.io/WasmerSharp/api/WasmerSharp/WasmerSharp.Instance.html
        var imports = importDelegates.Select(pair => {
            var (module, name) = pair.Key;
            return new Import(module, name, new ImportFunction(pair.Value));
        }).ToArray();

        // Load WASM from binary
        instance = new Instance(wasmBinary, imports);

        WriteLog(Logger.LogType.Debug, "WasmRunner", "WASM instance created");
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
            CallWasmFunc("__wasm_call_ctors");
        }
        // Initialization
        var init = FindExport("init", WebAssembly.ExternalKind.Function);
        if (init == null)
        {
            WriteLog(Logger.LogType.Error, "WasmRunner", "Cannot find function init");
            return;
        }
        CallWasmFunc("init");

        WriteLog(Logger.LogType.Debug, "WasmRunner", "WASM initialized");

        IsReady = true;
    }

    protected WebAssembly.Export FindExport(string name, WebAssembly.ExternalKind kind)
    {
        return module.Exports.FirstOrDefault(export => export.Name == name && export.Kind == kind);
    }

    protected void CallWasmFunc(string name, params object[] args)
    {
        instance.Call(name, args);
        AfterCall();
    }

    protected virtual void AfterCall() {}

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
                WriteLog(Logger.LogType.Log, "WasmRunner", Encoding.UTF8.GetString(outBuf.ToArray()));
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

    protected IntPtr WasmToIntPtr(Memory memory, int wasmPtr)
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

    public virtual void Dispose()
    {
        instance.Dispose();
    }

    public virtual void WriteLog(Logger.LogType type, string component, string message)
    {
        Logger.Write(type, component, message);
    }
}
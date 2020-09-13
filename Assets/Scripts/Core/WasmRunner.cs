using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using WebAssembly;
using WebAssembly.Runtime;

// A class for compiling and running WASM using dotnet-webassembly library
// For usage of dotnet-webassembly library:
//  https://github.com/RyanLamansky/dotnet-webassembly/blob/master/README.md#sample-create-and-execute-a-webassembly-file-in-memory
public class WasmRunner : IDisposable
{
    public SyncObject Object;
    public bool IsReady { get; private set; }

    Module module;
    Instance<Exports> instance;
    ImportDictionary imports;
    
    List<byte> outBuf = new List<byte>();

    // Similar to WASI, the WASM linear memory have to be exported
    //  https://github.com/WebAssembly/WASI/blob/master/design/application-abi.md
    public abstract class Exports
    {
        // Exported memory can be used as a property
        //  https://github.com/RyanLamansky/dotnet-webassembly/blob/8c60c4a657d9caf616f1926acf10adbf26a0980b/WebAssembly.Tests/MemoryReadTestBase.cs#L27
        public abstract UnmanagedMemory memory { get; }
    }

    public WasmRunner(SyncObject obj)
    {
        // Prepare external (C#) functions
        //  See: https://github.com/RyanLamansky/dotnet-webassembly/blob/8c60c4a657d9caf616f1926acf10adbf26a0980b/WebAssembly.Tests/FunctionImportTests.cs#L50-L52
        imports = new ImportDictionary {
            // WASI-compatible output for printf debugging
            { "wasi_snapshot_preview1", "fd_write", new FunctionImport(new Func<int, int, int, int, int>(WasiFdWrite)) },
            // WASI-compatible exit for AssemblyScript
            { "wasi_snapshot_preview1", "proc_exit", new FunctionImport(new Action<int>(WasiProcExit)) }
        };

        Object = obj;
    }

    public void Load(byte[] wasmBinary)
    {
        // Load WASM from binary
        using (var stream = new MemoryStream(wasmBinary))
        {
            module = Module.ReadFromBinary(stream);
        }

        // Compile WASM
        // Although dynamic can be used (as explained in the following URLs), we use reflections to call functions more dynamically.
        //  https://github.com/RyanLamansky/dotnet-webassembly/blob/8c60c4a657d9caf616f1926acf10adbf26a0980b/README.md#sample-create-and-execute-a-webassembly-file-in-memory
        //  https://github.com/RyanLamansky/dotnet-webassembly/blob/2683120c0df708404b44b89281e5843decb7347b/WebAssembly.Tests/CompilerTests.cs#L61
        // Note (for understanding API of dotnet-webassembly):
        //  In comments, there are some references to dotnet-webassembly test codes (WebAssembly.Tests).
        //  These test codes use ToInstance that is not present in library (used only in their tests).
        //      See: https://github.com/RyanLamansky/dotnet-webassembly/blob/6a19dd5816865ef06c5beb056384d11ef216ace9/WebAssembly.Tests/ModuleExtensions.cs#L85

        var instanceCreator = module.Compile<Exports>();
        instance = instanceCreator(imports);

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

        // Call WASM functions using reflection.
        var type = instance.Exports.GetType();
        // Constructors
        var callCtorsMethod = type.GetMethod("__wasm_call_ctors", new Type[0]);
        if (callCtorsMethod != null)
        {
            //callCtorsMethod.Invoke(instance.Exports, new object[0]);
            CallWasmFunc(callCtorsMethod);
        }
        // Initialization
        var initMethod = type.GetMethod("init", new Type[0]);
        if (initMethod == null)
        {
            Logger.Error("WasmRunner", "Cannot find function init");
            return;
        }
        //initMethod.Invoke(instance.Exports, new object[0]);
        CallWasmFunc(initMethod);

        Logger.Debug("WasmRunner", "WASM initialized");

        // Search event handlers
        foreach (var method in type.GetMethods())
        {
            // Event handlers should have names like handle_EVENTNAME
            if (!method.Name.StartsWith("handle_")) continue;
            // Event handlers should have signatures "void handle_EVENTNAME(i32 sender)"
            var parameters = method.GetParameters();
            if (method.ReturnType != typeof(void) || parameters[0].ParameterType != typeof(int)) continue;

            string eventName = method.Name.Substring("handle_".Length);
            // Register as a handler
            // TODO: unregister
            Object.RegisterEventHandler(eventName, (sender, args) => {
                CallWasmFunc(method, new object[] { (int)sender });
            });
        }

        IsReady = true;
    }

    void CallWasmFunc(System.Reflection.MethodInfo method, object[] args)
    {
        method.Invoke(instance.Exports, args);
    }

    void CallWasmFunc(System.Reflection.MethodInfo method)
    {
        CallWasmFunc(method, new object[0]);
    }

    // WASI-compatible fd_write
    //  https://github.com/WebAssembly/WASI/blob/master/phases/snapshot/docs.md#fd_write
    //  https://github.com/WebAssembly/WASI/blob/master/phases/snapshot/witx/wasi_snapshot_preview1.witx
    //  https://github.com/WebAssembly/WASI/blob/master/phases/snapshot/witx/typenames.witx
    //  https://github.com/bytecodealliance/wasmtime/blob/main/docs/WASI-tutorial.md#web-assembly-text-example
    int WasiFdWrite(int fd, int ptrIoVecs, int numIoVecs, int ptrNWritten)
    {
        // It seems WASI uses STDIN=0, STDOUT=1, and STDERR=2
        //  https://github.com/WebAssembly/wasi-libc/blob/5a7ba74c1959691d79580a1c3f4d94bca94bab8e/libc-top-half/musl/include/unistd.h#L10-L12
        if (fd != 2)    // Only STDERR is supported
        {
            return 8;   // errno "Bad file descriptor"
        }

        int bytesWritten = 0;

        var stream = new MemoryStream();

        unsafe
        {
            // Check boundary of WASM memory
            if (ptrIoVecs + numIoVecs * sizeof(WasiCIoVec) >= instance.Exports.memory.Size)
            {
                throw new Exception();  // TODO:
            }

            var vecs = (WasiCIoVec*)WasmToIntPtr(ptrIoVecs);
            for (int i = 0; i < numIoVecs; i++)
            {
                int size = vecs[i].Size;
                byte[] array = new byte[size];
                Marshal.Copy(WasmToIntPtr(vecs[i].Buf), array, 0, size);
                stream.Write(array, 0, size);
                bytesWritten += size;
            }
        }

        Marshal.WriteInt32(WasmToIntPtr(ptrNWritten), bytesWritten);

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
    void WasiProcExit(int exitCode)
    {
        IsReady = false;
    }

    IntPtr WasmToIntPtr(int wasmPtr)
    {
        return instance.Exports.memory.Start + wasmPtr;
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
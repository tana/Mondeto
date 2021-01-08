using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Security.Cryptography;
using WebAssembly;
using WebAssembly.Runtime;

// A class for compiling and running WASM using dotnet-webassembly
//  https://github.com/RyanLamansky/dotnet-webassembly
public class WasmRunner : IDisposable
{
    public bool IsReady { get; set; }

    protected Module Module;

    protected Instance<Exports> Instance;

    List<byte> outBuf = new List<byte>();

    Dictionary<(string, string), Delegate> importDelegates = new Dictionary<(string, string), Delegate>();

    Stopwatch stopwatch = new Stopwatch();
    const long TimeLimitMilliseconds = 1000;    // Temporarily increased to avoid crash

    // Similar to WASI, the WASM linear memory have to be exported
    //  https://github.com/WebAssembly/WASI/blob/master/design/application-abi.md
    public abstract class Exports
    {
        // Exported memory can be used as a property
        //  https://github.com/RyanLamansky/dotnet-webassembly/blob/8c60c4a657d9caf616f1926acf10adbf26a0980b/WebAssembly.Tests/MemoryReadTestBase.cs#L27
        public abstract UnmanagedMemory memory { get; }
    }

    public WasmRunner()
    {
        // Prepare external (C#) functExports.ions
        // Time limiting function
        AddImportFunction("mondeto", "check_time", (Func<int>)CheckTime);
        // WASI-compatible output for printf debugging
        AddImportFunction("wasi_snapshot_preview1", "fd_write", (Func<int, int, int, int, int>)WasiFdWrite);
        // WASI-compatible exit for AssemblyScript
        AddImportFunction("wasi_snapshot_preview1", "proc_exit", (Action<int>)WasiProcExit);
        // WASI-compatible random number generation (for seed)
        AddImportFunction("wasi_snapshot_preview1", "random_get", (Func<int, int, int>)WasiRandomGet);
    }

    // Add imported (C#) function
    // Note: this method must be called before Load.
    protected void AddImportFunction(string module, string name, Delegate func)
    {
        importDelegates[(module, name)] = func;
    }

    public void Load(byte[] wasmBinary)
    {
        // Analyze WASM using dotnet-webassembly
        using (var stream = new MemoryStream(wasmBinary))
        {
            Module = WebAssembly.Module.ReadFromBinary(stream);
        }

        // Patch the module to allow time-limiting
        WasmPatcher.PatchModule(Module);
        /*
        // Get patched WASM binary
        byte[] newBinary;
        using (var stream = new MemoryStream())
        {
            Module.WriteToBinary(stream);
            newBinary = stream.ToArray();
        }

        using (var stream = new FileStream("patched.wasm", FileMode.Create))
        {
            Module.WriteToBinary(stream);
        }
        */

        // Imported (C#) functions are specified as an dictionary of dictionaries
        //  https://github.com/RyanLamansky/dotnet-webassembly/blob/8c60c4a657d9caf616f1926acf10adbf26a0980b/WebAssembly.Tests/FunctionImportTests.cs#L50-L52
        //  https://github.com/RyanLamansky/dotnet-webassembly/blob/e827504c22d2695908de7dee0413d1a82f76aadd/WebAssembly/Runtime/ImportDictionary.cs#L10
        var imports = new ImportDictionary();
        foreach (var pair in importDelegates)
        {
            var (moduleName, funcName) = pair.Key;
            var func = pair.Value;

            if (!imports.ContainsKey(moduleName))
            {
                imports[moduleName] = new Dictionary<string, RuntimeImport>();
            }
            imports[moduleName][funcName] = new FunctionImport(func);
        }

        // Compile WASM and create an instance
        //  https://github.com/RyanLamansky/dotnet-webassembly/blob/8c60c4a657d9caf616f1926acf10adbf26a0980b/README.md#sample-create-and-execute-a-webassembly-file-in-memory
        var instanceCreator = Module.Compile<Exports>();
        Instance = instanceCreator(imports);

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

        IsReady = true;

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
    }

    protected WebAssembly.Export FindExport(string name, WebAssembly.ExternalKind kind)
    {
        return Module.Exports.FirstOrDefault(export => export.Name == name && export.Kind == kind);
    }

    protected void CallWasmFunc(string name, params object[] args)
    {
        if (!IsReady) throw new InvalidOperationException("WasmRunner is not ready");

        stopwatch.Reset();
        stopwatch.Start();

        // Although dynamic can be used (as explained in the following URLs), we use reflections to call functions more dynamically.
        //  https://github.com/RyanLamansky/dotnet-webassembly/blob/8c60c4a657d9caf616f1926acf10adbf26a0980b/README.md#sample-create-and-execute-a-webassembly-file-in-memory
        //  https://github.com/RyanLamansky/dotnet-webassembly/blob/2683120c0df708404b44b89281e5843decb7347b/WebAssembly.Tests/CompilerTests.cs#L61
        // Note (for understanding API of dotnet-webassembly):
        //  In comments, there are some references to dotnet-webassembly test codes (WebAssembly.Tests).
        //  These test codes use ToInstance that is not present in library (used only in their tests).
        //      See: https://github.com/RyanLamansky/dotnet-webassembly/blob/6a19dd5816865ef06c5beb056384d11ef216ace9/WebAssembly.Tests/ModuleExtensions.cs#L85

        var methodInfo = Instance.Exports.GetType().GetMethod(name, args.Select(arg => arg.GetType()).ToArray());
        methodInfo.Invoke(Instance.Exports, args);

        stopwatch.Stop();

        AfterCall();
    }

    protected virtual void AfterCall() {}

    int CheckTime()
    {
        if (stopwatch.ElapsedMilliseconds > TimeLimitMilliseconds)
        {
            WriteLog(Logger.LogType.Error, "WasmRunner", "Time limit exceeded");
            return 1;
        }
        else
        {
            return 0;
        }
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

        int ioVecSize = Marshal.SizeOf<WasiCIoVec>();

        // Check boundary of WASM memory
        if (ptrIoVecs + numIoVecs * ioVecSize >= Instance.Exports.memory.Size)
        {
            throw new Exception();  // TODO:
        }

        for (int i = 0; i < numIoVecs; i++)
        {
            IntPtr ptr = WasmToIntPtr(Instance.Exports.memory, ptrIoVecs) + ioVecSize * i;
            var vec = Marshal.PtrToStructure<WasiCIoVec>(ptr);

            int size = vec.Size;
            // boundary check
            if (vec.Buf + vec.Size >= Instance.Exports.memory.Size)
            {
                throw new Exception();  // TODO:
            }
            byte[] array = new byte[size];
            Marshal.Copy(WasmToIntPtr(Instance.Exports.memory, vec.Buf), array, 0, size);
            stream.Write(array, 0, size);
            bytesWritten += size;
        }

        Marshal.WriteInt32(WasmToIntPtr(Instance.Exports.memory, ptrNWritten), bytesWritten);

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
    void WasiProcExit(int exitCode)
    {
        IsReady = false;
    }

    // WASI-compatible random_get
    //  https://github.com/WebAssembly/WASI/blob/master/phases/snapshot/docs.md#-random_getbuf-pointeru8-buf_len-size---errno
    int WasiRandomGet(int bufPtr, int bufLen)
    {
        var randomBytes = new byte[bufLen];
        // Use cryptographic random number
        //  https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.rngcryptoserviceprovider?view=net-5.0
        using (var rng = new RNGCryptoServiceProvider())
        {
            rng.GetBytes(randomBytes);
        }
        Marshal.Copy(randomBytes, 0, WasmToIntPtr(Instance.Exports.memory, bufPtr), bufLen);

        // errno: "success"
        //  https://github.com/WebAssembly/WASI/blob/master/phases/snapshot/docs.md#-errno-enumu16
        //  https://github.com/WebAssembly/WASI/blob/master/phases/snapshot/witx/typenames.witx
        return 0;
    }

    protected IntPtr WasmToIntPtr(UnmanagedMemory memory, int wasmPtr)
    {
        return memory.Start + wasmPtr;
    }

    // Boundary check of WASM pointer
    protected bool CheckWasmPtr(UnmanagedMemory memory, int wasmPtr)
    {
        return (0 <= wasmPtr) && (wasmPtr < memory.Size);
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
        if (Instance != null)
        {
            Instance.Dispose();
        }
    }

    public virtual void WriteLog(Logger.LogType type, string component, string message)
    {
        Logger.Write(type, component, message);
    }
}
using System;
using System.IO;
using System.Collections.Generic;
using WebAssembly;
using WebAssembly.Runtime;

// A class for compiling and running WASM using dotnet-webassembly library
// For usage of dotnet-webassembly library:
//  https://github.com/RyanLamansky/dotnet-webassembly/blob/master/README.md#sample-create-and-execute-a-webassembly-file-in-memory
class WasmRunner : IDisposable
{
    Module module;
    Instance<object> instance;

    public WasmRunner(byte[] wasmBinary)
    {
        // Load WASM from binary
        using (var stream = new MemoryStream(wasmBinary))
        {
            module = Module.ReadFromBinary(stream);
        }

        // Compile WASM
        // Although dynamic can be used (as explained in the following URLs), we use object type instead (because we use reflections to call functions more dynamically).
        //  https://github.com/RyanLamansky/dotnet-webassembly/blob/8c60c4a657d9caf616f1926acf10adbf26a0980b/README.md#sample-create-and-execute-a-webassembly-file-in-memory
        //  https://github.com/RyanLamansky/dotnet-webassembly/blob/2683120c0df708404b44b89281e5843decb7347b/WebAssembly.Tests/CompilerTests.cs#L61
        // Note (for understanding API of dotnet-webassembly):
        //  In comments, there are some references to dotnet-webassembly test codes (WebAssembly.Tests).
        //  These test codes use ToInstance that is not present in library (used only in their tests).
        //      See: https://github.com/RyanLamansky/dotnet-webassembly/blob/6a19dd5816865ef06c5beb056384d11ef216ace9/WebAssembly.Tests/ModuleExtensions.cs#L85

        var instanceCreator = module.Compile<object>();
        instance = instanceCreator(new ImportDictionary());

        Logger.Debug("WasmRunner", "WASM instance created");
    }

    public void Initialize()
    {
        // Initialization of WASM code
        // In the future, we are planning to support WASI reactors.
        // (See https://github.com/WebAssembly/WASI/blob/master/design/application-abi.md )
        // However, until we support it, we use ABI (especially function names) that is
        // intentionally different from WASI, because our preliminary ABI is incompatible with WASI reactor.
        // (for example, we use "_init" instead of WASI "_initialize")

        // Call WASM functions using reflection.
        var type = instance.Exports.GetType();
        // Constructors
        var callCtorsMethod = type.GetMethod("__wasm_call_ctors", new Type[0]);
        if (callCtorsMethod != null)
        {
            callCtorsMethod.Invoke(instance.Exports, new object[0]);
        }
        // Initialization
        var initMethod = type.GetMethod("_init", new Type[0]);
        if (initMethod == null)
        {
            Logger.Error("WasmRunner", "Cannot find function _init");
            return;
        }
        initMethod.Invoke(instance.Exports, new object[0]);

        Logger.Debug("WasmRunner", "WASM initialized");
    }

    public void Dispose()
    {
        instance.Dispose();
    }
}
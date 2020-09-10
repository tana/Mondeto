using System;
using System.IO;
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
        var type = instance.GetType();
        // Constructors
        var callCtorsMethod = type.GetMethod("__wasm_call_ctors", new Type[0]);
        if (callCtorsMethod != null)
        {
            callCtorsMethod.Invoke(instance, new object[0]);
        }
        // Initialization
        var initMethod = type.GetMethod("_init", new Type[0]);
        if (initMethod != null)
        {
            Logger.Error("WasmRunner", "Cannot find function _init");
        }
        initMethod.Invoke(instance, new object[0]);
    }

    public void Dispose()
    {
        instance.Dispose();
    }
}
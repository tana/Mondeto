using UnityEngine;
using WebAssembly;
using WebAssembly.Runtime;

public class WasmTest : MonoBehaviour
{
    // Test of WASM compilation and execution
    //  https://github.com/RyanLamansky/dotnet-webassembly/blob/master/README.md#sample-create-and-execute-a-webassembly-file-in-memory

    public abstract class Fibonacci
    {
        public abstract long fib(int n);
    }

    public void Start()
    {
        Module module = Module.ReadFromBinary(@"G:\OneDrive\wasm\fibonacci\fibonacci.wasm");
        var instanceCreator = module.Compile<Fibonacci>();
        using (var instance = instanceCreator(new ImportDictionary()))
        {
            Debug.Log($"{instance.Exports.fib(30)}");
        }
    }

    public void Update()
    {
    }
}
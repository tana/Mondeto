# WebAssembly scripting samples
It uses AssemblyScript. Please run `npm run build` to build.

## Notes
- When compiling, `-Oz` option is necessary (specified in `package.json`). Without this option, WASM loading fails.
    - This is a compiler option for size optimization. See [https://www.assemblyscript.org/compiler.html#command-line-options]
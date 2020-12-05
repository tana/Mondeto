// This file is loaded when mondeto-as is imported as a package.
// Export everything defined in mondeto.ts to make them available for importer,
// while C#-defined function is imported from the WASM module called "mondeto"
// See:
//  https://github.com/AssemblyScript/assemblyscript/pull/594
//  https://github.com/AssemblyScript/assemblyscript/blob/1fcc374e985a1caca0736126a4e3ca6ca7776eac/tests/packages/packages/a/assembly/index.ts#L1
export * from "./mondeto"
// Also export wrappers
export * from "./wrapper"
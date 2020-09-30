using System;
using System.Collections.Generic;
using System.Linq;
using WebAssembly;
using WebAssembly.Instructions;

class WasmPatcher
{
    // Modify WASM code to allow time-limiting.
    // The idea of "modifying WASM code to limit execution time" was inspired by the EWASM of Ethereum.
    //  See: https://github.com/ewasm/design/blob/master/metering.md
    internal static void PatchModule(Module module)
    {
        // Add type declaration for check_time
        // i32 check_time(void)
        module.Types.Add(new WebAssemblyType {
            Form = FunctionType.Function,
            Parameters = new WebAssemblyValueType[0],
            Returns = new [] { WebAssemblyValueType.Int32 }
        });
        uint checkTimeTypeIdx = (uint)(module.Types.Count - 1);
        // Add function declaration for check_time
        module.Imports.Add(new Import.Function { Module = "mondeto", Field = "check_time", TypeIndex = checkTimeTypeIdx });
        uint checkTimeFuncIdx = (uint)(module.Imports.Count(imp => imp.Kind == ExternalKind.Function) - 1);

        // Modify code of each function
        foreach (FunctionBody body in module.Codes)
        {
            var newCode = new List<Instruction>();

            // Inject time-limiting code in the beginning of each function
            AddTimeLimitingCode(newCode, checkTimeFuncIdx);

            // Iterate over instructions
            foreach (Instruction insn in body.Code)
            {
                if (insn is Call callInsn)
                {
                    // Update indices of function calls (because new function import is added, function index have to be updated)
                    // See: https://webassembly.github.io/spec/core/syntax/modules.html#syntax-funcidx
                    callInsn.Index = (callInsn.Index >= checkTimeFuncIdx) ? (callInsn.Index + 1) : callInsn.Index;
                    newCode.Add(callInsn);
                }
                else if (insn is Loop loopInsn)
                {
                    // Inject time-limiting call in the beginning of a loop
                    newCode.Add(loopInsn);
                    AddTimeLimitingCode(newCode, checkTimeFuncIdx);
                }
                else
                {
                    newCode.Add(insn);
                }
            }

            body.Code = newCode;
        }

        // Update indices of function exports (because new function import is added, function index have to be updated)
        // Similar to function calls. (See: https://webassembly.github.io/spec/core/syntax/modules.html#syntax-funcidx)
        foreach (var export in module.Exports)
        {
            if (export.Kind == ExternalKind.Function)
            {
                export.Index = (export.Index >= checkTimeFuncIdx) ? (export.Index + 1) : export.Index;
            }
        }

        // Update indices of the function table (because new function import is added, function index have to be updated)
        // Similar to function calls and exports. (See: https://webassembly.github.io/spec/core/syntax/modules.html#syntax-funcidx)
        Table funcTable = module.Tables.FirstOrDefault(table => table.ElementType == ElementType.FunctionReference);
        if (funcTable != null)
        {
            int funcTableIdx = module.Tables.IndexOf(funcTable);
            var elements = module.Elements.FirstOrDefault(elems => elems.Index == funcTableIdx);
            if (elements != null)
            {
                elements.Elements = elements.Elements.Select(idx => (idx >= checkTimeFuncIdx) ? (idx + 1) : idx).ToList();
            }
        }
    }

    private static void AddTimeLimitingCode(List<Instruction> code, uint checkTimeFuncIdx)
    {
        code.Add(new Call(checkTimeFuncIdx));   // call $check_time
        // If the result of check_time is non-zero, a trap happens
        //  https://webassembly.github.io/spec/core/exec/instructions.html#xref-syntax-instructions-syntax-instr-control-mathsf-if-xref-syntax-instructions-syntax-blocktype-mathit-blocktype-xref-syntax-instructions-syntax-instr-mathit-instr-1-ast-xref-syntax-instructions-syntax-instr-control-mathsf-else-xref-syntax-instructions-syntax-instr-mathit-instr-2-ast-xref-syntax-instructions-syntax-instr-control-mathsf-end
        //  https://webassembly.github.io/spec/core/exec/instructions.html#xref-syntax-instructions-syntax-instr-control-mathsf-unreachable
        code.Add(new If()); // if
        code.Add(new Unreachable());    // unreachable
        code.Add(new Else());   // else
        code.Add(new End());    // end
    }
}
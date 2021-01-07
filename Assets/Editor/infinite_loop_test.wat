;; Written in WebAssembly Text Format
;; (See https://developer.mozilla.org/en-US/docs/WebAssembly/Understanding_the_text_format )
;; Converted to binary format with the following command line:
;;  wat2wasm infinite_loop_test.wat -o infinite_loop_test.wasm
(module
    (func $init (export "init")
        call $loop
    )
    (func $loop
        ;; Infinite loop using "loop" instruction and unconditional branch
        ;; See: https://webassembly.github.io/spec/core/syntax/instructions.html#syntax-instr-control
        loop $loop1
            br $loop1
        end
    )
    (memory (export "memory") 1)
)
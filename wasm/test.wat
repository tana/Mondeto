;; Written in WebAssembly Text Format
;; https://developer.mozilla.org/en-US/docs/WebAssembly/Understanding_the_text_format
;; Converted to binary format with the following command line:
;;  wat2wasm test.wat -o test.wasm
(module
    (func $_init (param) (result)
        ;; DO NOTHING
    )
    (export "_init" (func $_init))
)
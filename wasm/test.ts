// Using trace to write string.
// See https://www.assemblyscript.org/exports-and-imports.html#anatomy-of-a-module
import "wasi"

export function _init(): void {
    trace("hello")
}

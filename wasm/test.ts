// Using trace to write string.
// See https://www.assemblyscript.org/exports-and-imports.html#anatomy-of-a-module
import "wasi"

export function init(): void {
    trace("hello")
}

export function handle_collisionStart(sender: u32): void {
    trace("collisionStart")
}

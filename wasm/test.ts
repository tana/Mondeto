// Using trace to write string.
// See https://www.assemblyscript.org/exports-and-imports.html#anatomy-of-a-module
import "wasi"

import { get_field } from "./mondeto"

export function init(): void {
    trace("hello")
}

export function handle_collisionStart(sender: u32): void {
    trace("collisionStart from " + sender.toString())
    
    var name = "position";
    // https://www.assemblyscript.org/stdlib/string.html#encoding-api
    var buf = String.UTF8.encode("position");
    // https://www.assemblyscript.org/runtime.html#interface
    // https://www.assemblyscript.org/stdlib/arraybuffer.html#constructor
    var field = get_field(changetype<usize>(buf), buf.byteLength);

    trace(field.toString());
}

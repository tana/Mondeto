// Using trace to write string.
// See https://www.assemblyscript.org/exports-and-imports.html#anatomy-of-a-module
import "wasi"

import { get_field, get_type, decomp_vec, TypeCode } from "./mondeto"

class Vec {
    x: f32;
    y: f32;
    z: f32;
}

export function init(): void {
    trace("hello");
}

export function handle_collisionStart(sender: u32): void {
    trace("collisionStart from " + sender.toString());
    
    var name = "position";
    // https://www.assemblyscript.org/stdlib/string.html#encoding-api
    var buf = String.UTF8.encode("position");
    // https://www.assemblyscript.org/runtime.html#interface
    // https://www.assemblyscript.org/stdlib/arraybuffer.html#constructor
    var field = get_field(changetype<usize>(buf), buf.byteLength);
    trace("Value ID: " + field.toString());
    if (field < 0)
    {
        trace("Field not found");
        return;
    }

    var type = get_type(field as u32);
    trace("Type: " + (type as i32).toString())
    if (type != TypeCode.Vec)
    {
        trace("Incorrect type");
        return;
    }

    var vec = new Vec();
    // https://www.assemblyscript.org/environment.html#sizes-and-alignments
    // https://www.assemblyscript.org/interoperability.html#class-layout
    var ptr = changetype<usize>(vec);
    var xOffset = offsetof<Vec>("x");
    var yOffset = offsetof<Vec>("y");
    var zOffset = offsetof<Vec>("z");
    decomp_vec(field as u32, ptr + xOffset, ptr + yOffset, ptr + zOffset);

    trace("(" + vec.x.toString() + "," + vec.y.toString() + "," + vec.z.toString() + ")");
}

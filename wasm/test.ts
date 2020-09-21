// Using trace to write string.
// See https://www.assemblyscript.org/exports-and-imports.html#anatomy-of-a-module
import "wasi"

import { get_type, TypeCode, getField, get_int, get_float, getString, getVec } from "./mondeto"

export function init(): void {
    trace("hello");
}

export function handle_collisionStart(sender: u32): void {
    trace("collisionStart from " + sender.toString());
    
    const fieldRaw = getField("position");
    if (fieldRaw < 0)
    {
        trace("Field not found");
        return;
    }
    const field = fieldRaw as u32;
    trace("Value ID: " + field.toString());

    const type = get_type(field);
    trace("Type: " + type.toString());
    if (type != TypeCode.Vec)
    {
        trace("Incorrect type");
        return;
    }

    const vec = getVec(field);
    trace(vec.toString());

    // Other fields
    trace("testInt=" + get_int(getField("testInt") as u32).toString());
    trace("testFloat=" + get_float(getField("testFloat") as u32).toString());
    trace("testString=" + getString(getField("testString") as u32));
}

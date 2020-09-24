// Using trace to write string.
// See https://www.assemblyscript.org/exports-and-imports.html#anatomy-of-a-module
import "wasi"

import { get_type, TypeCode, getField, get_int, get_float, getString, getVec, getQuat, setField, make_int, make_float, make_string, makeString, make_vec } from "./mondeto"

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
    var testInt = get_int(getField("testInt") as u32);
    var testFloat = get_float(getField("testFloat") as u32);
    var testString = getString(getField("testString") as u32);
    trace("rotation=" + getQuat(getField("rotation") as u32).toString());
    trace("testInt=" + testInt.toString());
    trace("testFloat=" + testFloat.toString());
    trace("testString=" + testString);

    // Field update
    setField("testInt", make_int(testInt + 1));
    setField("testFloat", make_float(testFloat + 1.0));
    setField("testString", makeString(testString + "a"));
}

var phase: f32 = 0.0;

// Called every frame
// Parameter dt is time difference in seconds.
export function update(dt: f32): void {
    // Blink with 0.5 Hz frequency
    phase = (phase + 2 * 0.5 * Mathf.PI * dt) % (2 * Mathf.PI);
    var brightness = (Mathf.cos(phase) + 1) / 2;

    setField("color", make_vec(brightness, brightness, brightness));
}
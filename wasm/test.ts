// Using trace to write string.
// See https://www.assemblyscript.org/exports-and-imports.html#anatomy-of-a-module
import "wasi"

import { getField, getQuat, setField, make_vec, Vec, Quat, makeQuat, make_int, get_new_object, objectSetField, request_new_object, make_sequence, makeSequence, makeString, get_object_id, object_is_original } from "./mondeto"

export function init(): void {
    trace("init");
    trace("object ID = " + get_object_id().toString());
}

var state: boolean = false;

// Called when clicked.
export function handle_clickStart(sender: u32): void {
    trace("clickStart from " + sender.toString());
    state = !state;
    if (state) {
        setField("color", make_vec(1.0, 0.0, 0.0));
    } else {
        setField("color", make_vec(1.0, 1.0, 1.0));
    }

    setField("audioPlaying", make_int(1));

    // If this object is original (running on original node)
    if (object_is_original(get_object_id())) {
        request_new_object(); // Create new object
    }
}

export function handle_clickEnd(sender: u32): void {
    trace("clickEnd from " + sender.toString());
}

// Called every frame
// Parameter dt is time difference in seconds.
export function update(dt: f32): void {
    const rotation = getQuat(getField("rotation") as u32);
    const newRotation = Quat.fromAngleAxis(2 * Mathf.PI * 0.25 * dt, new Vec(1, 1, 1)) * rotation;
    setField("rotation", makeQuat(newRotation));

    // Check if new object is ready
    const newObjResult = get_new_object();
    if (newObjResult >= 0) {   // New object is ready
        trace("new object created");
        const objId = newObjResult as u32;
        // Set fields of new object
        objectSetField(objId, "position", make_vec(0.0, 5.0, 5.0));
        objectSetField(objId, "tags", makeSequence([
            makeString("sphere"), makeString("collider"), makeString("physics")
        ]));
    }
}
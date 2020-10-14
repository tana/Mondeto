import "wasi"

import { getField, get_new_object, get_object_id, makeSequence, makeString, make_int, make_vec, objectSetField, object_is_original, request_new_object, setField } from "./mondeto"

export function init(): void {
    trace("Raygun init");
}

export function handle_clickStart(sender: u32): void {
    // If this object is original (running on original node)
    if (object_is_original(get_object_id())) {
        request_new_object(); // Request creation of a new object (for bullet)
    }
}

export function update(dt: f32): void {
    // Check if new object is ready
    const newObjResult = get_new_object();
    if (newObjResult >= 0) {   // New object is ready
        // Create bullet
        const objId = newObjResult as u32;
        // Set fields of new object
        objectSetField(objId, "position", getField("position") as u32); // Same position
        objectSetField(objId, "scale", make_vec(0.3, 0.1, 0.1));
        objectSetField(objId, "color", make_vec(1.0, 0.0, 0.0));    // Red
        objectSetField(objId, "velocity", make_vec(-10.0, 0.0, 0.0));
        objectSetField(objId, "tags", makeSequence([
            makeString("cube"), makeString("material"), makeString("collider")
        ]));
        trace(getField("bulletCode").toString());
        objectSetField(objId, "codes", makeSequence([
            getField("bulletCode") as u32
        ]));

        setField("audioPlaying", make_int(1));  // Play sound
    }
}
// Using trace to write string.
// See https://www.assemblyscript.org/exports-and-imports.html#anatomy-of-a-module
import "wasi"

import { getField, getQuat, setField, make_vec, Vec, Quat, makeQuat } from "./mondeto"

export function init(): void {
}

var state: boolean = false;

// Called when collided with another collider.
export function handle_collisionStart(sender: u32): void {
    if (state) {
        setField("color", make_vec(1.0, 0.0, 0.0));
    } else {
        setField("color", make_vec(1.0, 1.0, 1.0));
    }
    state = !state;
}

// Called every frame
// Parameter dt is time difference in seconds.
export function update(dt: f32): void {
    const rotation = getQuat(getField("rotation") as u32);
    const newRotation = Quat.fromAngleAxis(2 * Mathf.PI * 0.25 * dt, new Vec(1, 1, 1)) * rotation;
    setField("rotation", makeQuat(newRotation));
}
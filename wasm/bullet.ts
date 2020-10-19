import "wasi"

import { delete_self, getField, getVec, get_object_id, makeVec, object_is_original, sendEvent, setField } from "./mondeto"

export function init(): void {
    trace("Bullet init");
}

export function update(dt: f32): void {
    const velocity = getVec(getField("velocity") as u32);
    // Fly at constant velocity
    let position = getVec(getField("position") as u32);
    position += velocity.multiply(dt);
    setField("position", makeVec(position));
}

export function handle_collisionStart(sender: u32): void {
    if (object_is_original(get_object_id())) {
        sendEvent(sender, "bulletHit", [], true);
        delete_self();
    }
}
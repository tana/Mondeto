import "wasi"

import { delete_self, getField, readVec, get_object_id, makeVec, object_is_original, sendEvent, setField } from "mondeto-as"

var lifetime: f32 = 2.0;

export function init(): void {
    trace("Bullet init");
}

export function update(dt: f32): void {
    lifetime -= dt;
    if (lifetime < 0) {
        delete_self();  // Disappear in 2 seconds
    }

    const velocity = readVec(getField("velocity") as u32);
    // Fly at constant velocity
    let position = readVec(getField("position") as u32);
    position += velocity.multiply(dt);
    setField("position", makeVec(position));
}

export function handle_collisionStart(sender: u32): void {
    if (object_is_original(get_object_id())) {
        sendEvent(sender, "bulletHit", [], true);
        delete_self();
    }
}
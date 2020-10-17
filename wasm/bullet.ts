import "wasi"

import { delete_self, getField, getVec, makeVec, setField } from "./mondeto"

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
    // TODO: this is not called, probably because collider related bug
    trace("Bullet collisionStart");
    delete_self();
}
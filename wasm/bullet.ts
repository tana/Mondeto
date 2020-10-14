import "wasi"

import { getField, getVec, makeVec, setField } from "./mondeto"

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
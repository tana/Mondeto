import "wasi";
import { getField, readQuat, Quat, Vec, setField, makeQuat } from "mondeto-as";

export function init(): void {
    trace("Earth init");
}

export function update(dt: f32): void {
    const rotation = readQuat(getField("rotation") as u32);
    const newRotation = Quat.fromAngleAxis(0.25 * dt, new Vec(0, 1, 0)) * rotation;
    setField("rotation", makeQuat(newRotation));
}
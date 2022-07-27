import "wasi"
import { make_int, setField, getField, readVec, makeVec } from "mondeto-as/assembly"

var exploding: boolean = false;
var explosionTimer: f32 = 1.5;

export function init(): void {
    trace("Target init");
}

export function update(dt: f32): void {
}

// When a bullet hits this target
export function handle_bulletHit(sender: u32): void {
    setField("audioPlaying", make_int(1));

    let position = readVec(getField("position") as u32);

    // Move using random number
    const moveLen: f32 = 1.0;
    position.x += (Math.random() < 0.5) ? -moveLen : moveLen;
    position.y += (Math.random() < 0.5) ? -moveLen : moveLen;
    position.z += (Math.random() < 0.5) ? -moveLen : moveLen;

    setField("position", makeVec(position));
}

import "wasi"
import { delete_self, make_int, setField } from "./mondeto"

var exploding: boolean = false;
var explosionTimer: f32 = 1.5;

export function init(): void {
    trace("Target init");
}

export function update(dt: f32): void {
    if (exploding) {
        explosionTimer -= dt;
        if (explosionTimer < 0) {
            delete_self();
        }
    }
}

// When a bullet hits this target
export function handle_bulletHit(sender: u32): void {
    exploding = true;
    setField("audioPlaying", make_int(1));
}

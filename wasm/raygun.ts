import "wasi"

import { getField, make_int, setField } from "./mondeto"

export function init(): void {
    trace("Raygun init");
}

export function handle_clickStart(sender: u32): void {
    setField("audioPlaying", make_int(1));  // Play sound
}
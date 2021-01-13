import "wasi";
import { getField, read_object_ref, read_int, makeSequence, sendEvent } from "mondeto-as";

export function init(): void {
}

export function handle_collisionStart(sender: u32): void {
    const target = read_object_ref(getField("target") as u32);
    const noteNum = read_int(getField("noteNum") as u32);
    sendEvent(target, "noteOn", [noteNum]);
}

export function handle_collisionEnd(sender: u32): void {
    const target = read_object_ref(getField("target") as u32);
    sendEvent(target, "noteOff", []);
}
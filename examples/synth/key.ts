import "wasi";
import { getField, read_object_ref, read_int, make_int, sendEvent } from "mondeto-as/assembly";

export function init(): void {
}

export function handle_collisionStart(sender: u32): void {
    sendNoteOn();
}

export function handle_collisionEnd(sender: u32): void {
    sendNoteOff();
}

export function handle_clickStart(sender: u32): void {
    sendNoteOn();
}

export function handle_clickEnd(sender: u32): void {
    sendNoteOff();
}

function sendNoteOn(): void {
    const target = read_object_ref(getField("target") as u32);
    const noteNum = read_int(getField("noteNum") as u32);
    sendEvent(target, "noteOn", [make_int(noteNum)], true);
}

function sendNoteOff(): void {
    const target = read_object_ref(getField("target") as u32);
    sendEvent(target, "noteOff", [], true);
}
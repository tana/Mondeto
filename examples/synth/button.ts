import "wasi";
import { read_object_ref, readString, getField, sendEvent } from "mondeto-as/assembly";

export function init(): void {
}

export function handle_collisionStart(sender: u32): void {
    sendClickEvent();
}

export function handle_clickStart(sender: u32): void {
    sendClickEvent();
}

function sendClickEvent(): void {
    const target = read_object_ref(getField("target") as u32);
    const eventName = readString(getField("eventName") as u32);
    sendEvent(target, eventName, [], true);
}
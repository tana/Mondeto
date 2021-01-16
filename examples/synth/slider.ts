import "wasi";
import { read_float, getWorldCoordinate, get_object_id, Transform, Vec, getField, setField, makeVec, read_object_ref, readString, make_float, sendEvent } from "mondeto-as";

let isGrabbed: bool = false;
let grabber: u32;
let selfTransform: Transform;
let selfX: Vec;
let length: f32;

export function init(): void {
    // Get length parameter
    length = read_float(getField("length") as u32);
    // Get initial transform and X axis of this object (in world coordinate)
    selfTransform = getWorldCoordinate(get_object_id()) as Transform;
    selfX = selfTransform.rotation.rotateVec(new Vec(1, 0, 0));
}

export function update(dt: f32): void {
    if (isGrabbed) {
        // Get displacement between grabber and initial position of this object (in world coordinate)
        const grabberTransform = getWorldCoordinate(grabber) as Transform;
        const displacement = grabberTransform.position - selfTransform.position;
        // Calculate slider value (horizontal displacement / length) and constrain between 0 to 1
        const value = Mathf.min(Mathf.max(0, displacement.dot(selfX) / length), 1);
        // Calculate new position in local coordinate
        const position = selfTransform.apply(new Vec(value * length, 0, 0));
        setField("position", makeVec(position));
        // Send value to another object
        sendValue(value);
    }
}

export function handle_grab(sender: u32): void {
    if (!isGrabbed) {
        grabber = sender;
        isGrabbed = true;
    }
}

export function handle_ungrab(sender: u32): void {
    if (isGrabbed) {
        isGrabbed = false;
    }
}

function sendValue(value: f32): void {
    const target = read_object_ref(getField("target") as u32);
    const eventName = readString(getField("eventName") as u32);
    sendEvent(target, eventName, [make_float(value)]);
}
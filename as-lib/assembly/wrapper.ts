/**
 * High-level, AssemblyScript-friendly wrappers of Mondeto WASM API
 * @packageDocumentation
 */
// Note: @packageDocumentation is needed for file-level doc comment.
// See: https://typedoc.org/guides/doccomments/#files

import { get_event_args, get_event_args_count, get_field, get_sequence_length, get_string_length, get_world_coordinate, make_quat, make_sequence, make_string, make_vec, object_get_field, object_set_field, read_quat, read_sequence, read_string, read_vec, send_event, set_field, write_audio } from "./mondeto";
import { Vec, Quat, Transform } from "./math";

/**
 * @param name Field name.
 * @returns Value ID. Usually within range of u32, but becomes negative if failed.
 */
export function getField(name: string): i64 {
    // https://www.assemblyscript.org/stdlib/string.html#encoding-api
    const buf = String.UTF8.encode(name);
    // https://www.assemblyscript.org/runtime.html#interface
    // https://www.assemblyscript.org/stdlib/arraybuffer.html#constructor
    return get_field(changetype<usize>(buf), buf.byteLength);
}

export function setField(name: string, valueID: u32): void {
    // https://www.assemblyscript.org/stdlib/string.html#encoding-api
    const buf = String.UTF8.encode(name);
    // https://www.assemblyscript.org/runtime.html#interface
    // https://www.assemblyscript.org/stdlib/arraybuffer.html#constructor
    set_field(changetype<usize>(buf), buf.byteLength, valueID);
}

/**
 * @param objId Object ID.
 * @param name Field name.
 * @returns Value ID. Usually within range of u32, but becomes negative if failed.
 */
export function objectGetField(objId: u32, name: string): i64 {
    // https://www.assemblyscript.org/stdlib/string.html#encoding-api
    const buf = String.UTF8.encode(name);
    // https://www.assemblyscript.org/runtime.html#interface
    // https://www.assemblyscript.org/stdlib/arraybuffer.html#constructor
    return object_get_field(objId, changetype<usize>(buf), buf.byteLength);
}

export function objectSetField(objId: u32, name: string, valueID: u32): i32 {
    // https://www.assemblyscript.org/stdlib/string.html#encoding-api
    const buf = String.UTF8.encode(name);
    // https://www.assemblyscript.org/runtime.html#interface
    // https://www.assemblyscript.org/stdlib/arraybuffer.html#constructor
    return object_set_field(objId, changetype<usize>(buf), buf.byteLength, valueID);
}

export function readString(valueID: u32): string {
    const len = get_string_length(valueID);
    const buf = new ArrayBuffer(len as i32);
    read_string(valueID, changetype<usize>(buf), buf.byteLength);
    // https://www.assemblyscript.org/stdlib/string.html#encoding-api
    return String.UTF8.decode(buf, false);
}

export function makeString(str: string): u32 {
    // https://www.assemblyscript.org/stdlib/string.html#encoding-api
    const buf = String.UTF8.encode(str);
    // https://www.assemblyscript.org/runtime.html#interface
    // https://www.assemblyscript.org/stdlib/arraybuffer.html#constructor
    return make_string(changetype<usize>(buf), buf.byteLength);
}

export function readVec(valueID: u32): Vec {
    const vec = new Vec(0, 0, 0);
    // https://www.assemblyscript.org/environment.html#sizes-and-alignments
    // https://www.assemblyscript.org/interoperability.html#class-layout
    const ptr = changetype<usize>(vec);
    const xOffset = offsetof<Vec>("x");
    const yOffset = offsetof<Vec>("y");
    const zOffset = offsetof<Vec>("z");
    read_vec(valueID, ptr + xOffset, ptr + yOffset, ptr + zOffset);

    return vec;
}

export function readQuat(valueID: u32): Quat {
    const quat = new Quat();
    // https://www.assemblyscript.org/environment.html#sizes-and-alignments
    // https://www.assemblyscript.org/interoperability.html#class-layout
    const ptr = changetype<usize>(quat);
    const wOffset = offsetof<Quat>("w");
    const xOffset = offsetof<Quat>("x");
    const yOffset = offsetof<Quat>("y");
    const zOffset = offsetof<Quat>("z");
    read_quat(valueID, ptr + wOffset, ptr + xOffset, ptr + yOffset, ptr + zOffset);

    return quat;
}

/**
 * @param valueID Value ID of a sequence.
 * @returns Array of Value IDs.
 */
export function readSequence(valueID: u32): u32[] {
    const len = i32(get_sequence_length(valueID));
    const array = new Array<u32>(len);
    // AssemblyScript array contains an ArrayBuffer. Pointer can be acquired by changetype.
    // See:
    //  https://www.assemblyscript.org/memory.html#internals
    //  https://www.assemblyscript.org/runtime.html#interface
    read_sequence(valueID, changetype<usize>(array.buffer), len);
    return array;
}

export function makeVec(v: Vec): u32 {
    return make_vec(v.x, v.y, v.z);
}

export function makeQuat(q: Quat): u32 {
    return make_quat(q.w, q.x, q.y, q.z);
}

export function makeSequence(elems: u32[]): u32 {
    // AssemblyScript array contains an ArrayBuffer. Pointer can be acquired by changetype.
    // See:
    //  https://www.assemblyscript.org/memory.html#internals
    //  https://www.assemblyscript.org/runtime.html#interface
    return make_sequence(changetype<usize>(elems.buffer), elems.length);
}

/**
 * Calculates the world coordinate of an object.
 * @param objID Object ID.
 * @returns A {@linkcode Transform} that represents the calculated world coordinate, or null when failed to calculate.
 */
export function getWorldCoordinate(objID: u32): Transform | null {
    const vec = new Vec(), quat = new Quat();

    // https://www.assemblyscript.org/environment.html#sizes-and-alignments
    // https://www.assemblyscript.org/interoperability.html#class-layout
    const vecPtr = changetype<usize>(vec);
    const vxOffset = offsetof<Vec>("x");
    const vyOffset = offsetof<Vec>("y");
    const vzOffset = offsetof<Vec>("z");
    const quatPtr = changetype<usize>(quat);
    const qwOffset = offsetof<Quat>("w");
    const qxOffset = offsetof<Quat>("x");
    const qyOffset = offsetof<Quat>("y");
    const qzOffset = offsetof<Quat>("z");
    const ret = get_world_coordinate(
        objID,
        vecPtr + vxOffset, vecPtr + vyOffset, vecPtr + vzOffset,
        quatPtr + qwOffset, quatPtr + qxOffset, quatPtr + qyOffset, quatPtr + qzOffset
    );
    if (ret == 0) {
        return new Transform(vec, quat);
    } else {
        return null;
    }
}

/**
 * Sends an event to an object.
 * @param receiverID Object ID of the receiving object of the event.
 * @param name Name of the event.
 * @param args Arguments of the event. Array of Value IDs.
 * @param localOnly If true, the event is also broadcast to the receiver in another node.
 * @returns 0 when success. -1 when failure (e.g. receiverID is invalid).
 */
export function sendEvent(receiverID: u32, name: string, args: u32[], localOnly: boolean = false): i32 {
    // https://www.assemblyscript.org/stdlib/string.html#encoding-api
    const buf = String.UTF8.encode(name);
    // https://www.assemblyscript.org/runtime.html#interface
    // https://www.assemblyscript.org/stdlib/arraybuffer.html#constructor
    const namePtr = changetype<usize>(buf);
    const nameLen = buf.byteLength;

    // AssemblyScript array contains an ArrayBuffer. Pointer can be acquired by changetype.
    // See:
    //  https://www.assemblyscript.org/memory.html#internals
    //  https://www.assemblyscript.org/runtime.html#interface
    const argsPtr = changetype<usize>(args.buffer);
    const argsLen = args.length;

    return send_event(receiverID, namePtr, nameLen, argsPtr, argsLen, localOnly ? 1 : 0);
}

/**
 * Retrieves arguments of an event inside an event handler.
 * @returns Array of Value IDs.
 */
export function getEventArgs(): u32[] {
    const count = i32(get_event_args_count());
    const array = new Array<u32>(count);
    // AssemblyScript array contains an ArrayBuffer. Pointer can be acquired by changetype.
    // See:
    //  https://www.assemblyscript.org/memory.html#internals
    //  https://www.assemblyscript.org/runtime.html#interface
    get_event_args(changetype<usize>(array.buffer), count);
    return array;
}

/**
 * Play audio samples (48000 Hz sampling rate, 32-bit floating point, value is from -1 to 1).
 * It can write any number of samples, but 960 is the best. This is related to [frame size restriction of Opus codec](https://opus-codec.org/docs/opus_api-1.3.1/group__opus__encoder.html#ga4ae9905859cd241ef4bb5c59cd5e5309).
 * @param samples Audio samples.
 */
export function writeAudio(samples: f32[]): void {
    // AssemblyScript array contains an ArrayBuffer. Pointer can be acquired by changetype.
    // See:
    //  https://www.assemblyscript.org/memory.html#internals
    //  https://www.assemblyscript.org/runtime.html#interface
    write_audio(changetype<usize>(samples.buffer), samples.length);
}
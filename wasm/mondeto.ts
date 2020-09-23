export enum TypeCode
{
    Int = 0,
    Long = 2,
    Float = 4,
    Double = 5,
    String = 6,
    Vec = 7,
    Quat = 8,
    BlobHandle = 9,
    Sequence = 10,
    ObjectRef = 11
}

// Declarations for imported functions
// https://www.assemblyscript.org/exports-and-imports.html#imports
export declare function get_field(name_ptr: usize, name_len: usize): i64;
export declare function get_type(value_id: u32): TypeCode;
export declare function get_vec(value_id: u32, x_ptr: usize, y_ptr: usize, z_ptr: usize): void;
export declare function get_quat(value_id: u32, w_ptr: usize, x_ptr: usize, y_ptr: usize, z_ptr: usize): void;
export declare function get_int(value_id: u32): i32;
export declare function get_long(value_id: u32): i64;
export declare function get_float(value_id: u32): f32;
export declare function get_double(value_id: u32): f64;
export declare function get_string_length(value_id: u32): i32;
export declare function get_string(value_id: u32, ptr: usize, max_len: i32): i32;
export declare function make_int(value: i32): u32;
export declare function make_long(value: i64): u32;
export declare function make_float(value: f32): u32;
export declare function make_double(value: f64): u32;
export declare function make_vec(x: f32, y: f32, z: f32): u32;
export declare function make_quat(w: f32, x: f32, y: f32, z: f32): u32;
export declare function make_string(ptr: usize, len: i32): u32;

// Wrappers
class Vec {
    x: f32;
    y: f32;
    z: f32;

    toString(): string {
        return "Vec(" + this.x.toString() + "," + this.y.toString() + "," + this.z.toString() + ")"
    }
}

class Quat {
    w: f32;
    x: f32;
    y: f32;
    z: f32;

    toString(): string {
        return "Quat(" + this.w.toString() + "," + this.x.toString() + "," + this.y.toString() + "," + this.z.toString() + ")"
    }
}

export function getField(name: string): i64 {
    // https://www.assemblyscript.org/stdlib/string.html#encoding-api
    const buf = String.UTF8.encode(name);
    // https://www.assemblyscript.org/runtime.html#interface
    // https://www.assemblyscript.org/stdlib/arraybuffer.html#constructor
    return get_field(changetype<usize>(buf), buf.byteLength);
}

export function getString(valueID: u32): string {
    const len = get_string_length(valueID);
    const buf = new ArrayBuffer(len);
    get_string(valueID, changetype<usize>(buf), buf.byteLength);
    // https://www.assemblyscript.org/stdlib/string.html#encoding-api
    return String.UTF8.decode(buf, false);
}

export function getVec(valueID: u32): Vec
{
    const vec = new Vec();
    // https://www.assemblyscript.org/environment.html#sizes-and-alignments
    // https://www.assemblyscript.org/interoperability.html#class-layout
    const ptr = changetype<usize>(vec);
    const xOffset = offsetof<Vec>("x");
    const yOffset = offsetof<Vec>("y");
    const zOffset = offsetof<Vec>("z");
    get_vec(valueID, ptr + xOffset, ptr + yOffset, ptr + zOffset);

    return vec;
}

export function getQuat(valueID: u32): Quat
{
    const quat = new Quat();
    // https://www.assemblyscript.org/environment.html#sizes-and-alignments
    // https://www.assemblyscript.org/interoperability.html#class-layout
    const ptr = changetype<usize>(quat);
    const wOffset = offsetof<Quat>("w");
    const xOffset = offsetof<Quat>("x");
    const yOffset = offsetof<Quat>("y");
    const zOffset = offsetof<Quat>("z");
    get_quat(valueID, ptr + wOffset, ptr + xOffset, ptr + yOffset, ptr + zOffset);

    return quat;
}
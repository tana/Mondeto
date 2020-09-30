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
export declare function set_field(name_ptr: usize, name_len: usize, value_id: u32): void;
export declare function get_type(value_id: u32): TypeCode;
export declare function get_vec(value_id: u32, x_ptr: usize, y_ptr: usize, z_ptr: usize): void;
export declare function get_quat(value_id: u32, w_ptr: usize, x_ptr: usize, y_ptr: usize, z_ptr: usize): void;
export declare function get_int(value_id: u32): i32;
export declare function get_long(value_id: u32): i64;
export declare function get_float(value_id: u32): f32;
export declare function get_double(value_id: u32): f64;
export declare function get_string_length(value_id: u32): usize;
export declare function get_string(value_id: u32, ptr: usize, max_len: usize): i32;
export declare function make_int(value: i32): u32;
export declare function make_long(value: i64): u32;
export declare function make_float(value: f32): u32;
export declare function make_double(value: f64): u32;
export declare function make_vec(x: f32, y: f32, z: f32): u32;
export declare function make_quat(w: f32, x: f32, y: f32, z: f32): u32;
export declare function make_string(ptr: usize, len: usize): u32;

// Wrappers
export class Vec {
    x: f32;
    y: f32;
    z: f32;

    constructor(x: f32, y: f32, z: f32) {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    toString(): string {
        return "Vec(" + this.x.toString() + "," + this.y.toString() + "," + this.z.toString() + ")"
    }

    magnitude(): f32 {
        return Mathf.sqrt(this.x * this.x + this.y * this.y + this.z * this.z);
    }

    normalize(): Vec {
        const mag = this.magnitude();
        return new Vec(this.x / mag, this.y / mag, this.z / mag);
    }
}

export class Quat {
    w: f32;
    x: f32;
    y: f32;
    z: f32;

    constructor(w: f32 = 1.0, x: f32 = 0.0, y: f32 = 0.0, z: f32 = 0.0) {
        this.w = w;
        this.x = x;
        this.y = y;
        this.z = z;
    }

    toString(): string {
        return "Quat(" + this.w.toString() + "," + this.x.toString() + "," + this.y.toString() + "," + this.z.toString() + ")"
    }

    // Quaternion operations
    //  https://mathworld.wolfram.com/Quaternion.html
    // Some of them are implemented using operator overloads of AssemblyScript.
    //  https://www.assemblyscript.org/peculiarities.html#operator-overloads

    @operator("*")
    static __op(a: Quat, b: Quat): Quat {
        return new Quat(
            a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z,
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
            a.w * b.y + a.y * b.w + a.z * b.x - a.x * b.z,
            a.w * b.z + a.z * b.w + a.x * b.y - a.y * b.x
        );
    }

    // angle is in radians
    static fromAngleAxis(angle: f32, axis: Vec): Quat {
        const normalized = axis.normalize();
        const s = Mathf.sin(angle / 2.0);
        return new Quat(
            Mathf.cos(angle / 2.0),
            normalized.x * s,
            normalized.y * s,
            normalized.z * s
        );
    }
}

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

export function getString(valueID: u32): string {
    const len = get_string_length(valueID);
    const buf = new ArrayBuffer(len as i32);
    get_string(valueID, changetype<usize>(buf), buf.byteLength);
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

export function getVec(valueID: u32): Vec {
    const vec = new Vec(0, 0, 0);
    // https://www.assemblyscript.org/environment.html#sizes-and-alignments
    // https://www.assemblyscript.org/interoperability.html#class-layout
    const ptr = changetype<usize>(vec);
    const xOffset = offsetof<Vec>("x");
    const yOffset = offsetof<Vec>("y");
    const zOffset = offsetof<Vec>("z");
    get_vec(valueID, ptr + xOffset, ptr + yOffset, ptr + zOffset);

    return vec;
}

export function getQuat(valueID: u32): Quat {
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

export function makeQuat(q: Quat): u32 {
    return make_quat(q.w, q.x, q.y, q.z);
}
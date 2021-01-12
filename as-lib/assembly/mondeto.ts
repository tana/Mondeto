/**
 * Direct, low-level bindings to C#-defined WASM API
 * @packageDocumentation
 */
// Note: @packageDocumentation is needed for file-level doc comment.
// See: https://typedoc.org/guides/doccomments/#files

/** Represents type of a Mondeto value */
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
// Object manipulation
/** Requests creation of a new object. The new object will be available from {@linkcode get_new_object}. */
export declare function request_new_object(): void;
/**
 * Gets the Object ID of a new object requested by {@linkcode request_new_object}.
 * @returns Object ID of the new object. Becomes negative if the new object is not ready.
 */
export declare function get_new_object(): i64;
/** Gets the Object ID of the object which the code is attached to. */
export declare function get_object_id(): u32;
/** Checks whether the object is original. */
export declare function object_is_original(obj_id: u32): i32;
/** Deletes the object which the code is attached to. */
export declare function delete_self(): void;
// Field manipulation
/**
 * @param name_ptr Pointer to the field name.
 * @param name_len Length of the field name in bytes.
 * @returns Value ID. Usually within range of u32, but becomes negative if failed.
 */
export declare function get_field(name_ptr: usize, name_len: usize): i64;
/**
 * @param name_ptr Pointer to the field name.
 * @param name_len Length of the field name in bytes.
 * @param value_id Value ID.
 */
export declare function set_field(name_ptr: usize, name_len: usize, value_id: u32): void;
/**
 * @param obj_id Object ID.
 * @param name_ptr Pointer to the field name.
 * @param name_len Length of the field name in bytes.
 * @returns Value ID. Usually within range of u32, but becomes negative if failed.
 */
export declare function object_get_field(obj_id: u32, name_ptr: usize, name_len: usize): i64;
/**
 * @param obj_id Object ID.
 * @param name_ptr Pointer to the field name.
 * @param name_len Length of the field name in bytes.
 * @param value_id Value ID.
 */
export declare function object_set_field(obj_id: u32, name_ptr: usize, name_len: usize, value_id: u32): i32;
// IValue-related
export declare function get_type(value_id: u32): TypeCode;
export declare function read_vec(value_id: u32, x_ptr: usize, y_ptr: usize, z_ptr: usize): void;
export declare function read_quat(value_id: u32, w_ptr: usize, x_ptr: usize, y_ptr: usize, z_ptr: usize): void;
export declare function read_int(value_id: u32): i32;
export declare function read_long(value_id: u32): i64;
export declare function read_float(value_id: u32): f32;
export declare function read_double(value_id: u32): f64;
export declare function get_string_length(value_id: u32): usize;
export declare function read_string(value_id: u32, ptr: usize, max_len: usize): i32;
export declare function read_object_ref(value_id: u32): u32;
export declare function make_int(value: i32): u32;
export declare function make_long(value: i64): u32;
export declare function make_float(value: f32): u32;
export declare function make_double(value: f64): u32;
export declare function make_vec(x: f32, y: f32, z: f32): u32;
export declare function make_quat(w: f32, x: f32, y: f32, z: f32): u32;
export declare function make_string(ptr: usize, len: usize): u32;
export declare function make_sequence(elems_ptr: usize, elems_len: usize): u32;
export declare function make_object_ref(obj_id: u32): u32;
// Event-related
export declare function send_event(receiver_id: u32, name_ptr: usize, name_len: usize, args_ptr: usize, args_len: usize, local_only: i32): i32;
// Other
export declare function get_world_coordinate(obj_id: u32, vx_ptr: usize, vy_ptr: usize, vz_ptr: usize, qw_ptr: usize, qx_ptr: usize, qy_ptr: usize, qz_ptr: usize): i32;
export declare function write_audio(ptr: usize, len: usize): void;
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
export declare function decomp_vec(value_id: u32, x_ptr: usize, y_ptr: usize, z_ptr: usize): void;

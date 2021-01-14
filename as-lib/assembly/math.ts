/**
 * Vector, quaternion and transformation.
 * @packageDocumentation
 */
// Note: @packageDocumentation is needed for file-level doc comment.
// See: https://typedoc.org/guides/doccomments/#files

/**
 * 3-dimensional vector.
 */
export class Vec {
    x: f32;
    y: f32;
    z: f32;

    constructor(x: f32 = 0.0, y: f32 = 0.0, z: f32 = 0.0) {
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

    multiply(a: f32): Vec {
        return new Vec(this.x * a, this.y * a, this.z * a);
    }

    dot(other: Vec): f32 {
        return this.x * other.x + this.y * other.y + this.z * other.z;
    }

    cross(other: Vec): Vec {
        return new Vec(
            this.y * other.z - this.z - other.y,
            this.z * other.x - this.x - other.z,
            this.x * other.y - this.y - other.x
        );
    }

    // Operator overloads
    //  https://www.assemblyscript.org/peculiarities.html#operator-overloads

    @operator("+")
    static __opPlus(a: Vec, b: Vec): Vec {
        return new Vec(a.x + b.x, a.y + b.y, a.z + b.z);
    }

    @operator("-")
    static __opMinus(a: Vec, b: Vec): Vec {
        return new Vec(a.x - b.x, a.y - b.y, a.z - b.z);
    }

    @operator.prefix("-")
    static __opPrefixPlus(a: Vec): Vec {
        return new Vec(-a.x, -a.y, -a.z);
    }
}

/**
 * Quaternion.
 */
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
    static __opMultiply(a: Quat, b: Quat): Quat {
        return new Quat(
            a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z,
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
            a.w * b.y + a.y * b.w + a.z * b.x - a.x * b.z,
            a.w * b.z + a.z * b.w + a.x * b.y - a.y * b.x
        );
    }

    /** Rotates a vector. */
    rotateVec(v: Vec): Vec {
        const vQuat = new Quat(0, v.x, v.y, v.z);
        const after = this * vQuat * this.conjugate();
        return new Vec(after.x, after.y, after.z);
    }

    conjugate(): Quat {
        return new Quat(this.w, -this.x, -this.y, -this.z);
    }

    /**
     * Create a quaternion from angle-axis form.
     * @param angle Rotation angle in radians.
     * @param axis Axis of rotation.
     */
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

/** Pair of a {@linkcode Vec} and a {@linkcode Quat}. */
export class Transform {
    position: Vec;
    rotation: Quat;

    constructor(pos: Vec, rot: Quat) {
        this.position = pos;
        this.rotation = rot;
    }

    /**
     * Apply this transformation to a point.
     * @param p a point to be transformed.
     */
    apply(p: Vec): Vec {
        return this.rotation.rotateVec(p) + this.position;
    }
}

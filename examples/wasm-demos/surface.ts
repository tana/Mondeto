import "wasi";
import { make_vec, makeSequence, setField, make_int } from "mondeto-as/assembly";

const DIV: i32 = 32;
let vertices: Array<u32>;
let indices: Array<u32>;

let time: f64 = 0;

export function init(): void {
    vertices = new Array<u32>((DIV + 1) * (DIV + 1));
    indices = new Array<u32>(6 * DIV * DIV);
}

export function update(dt: f32): void {
    time += dt;

    // Plot
    for (let i = 0; i <= DIV; i++) {
        const y = 2.0 * i / DIV - 1.0;
        for (let j = 0; j <= DIV; j++) {
            const x = 2.0 * j / DIV - 1.0;
            const z = func(x, y, time);

            // f64 values are cast to f32 (see: https://www.assemblyscript.org/types.html#type-rules )
            vertices[i * (DIV + 1) + j] = make_vec(f32(x), f32(y), f32(-z));
        }
    }
    let p = 0;
    for (let i = 0; i < DIV; i++) {
        for (let j = 0; j < DIV; j++) {
            indices[p++] = make_int(i * (DIV + 1) + j);           // -x -y
            indices[p++] = make_int((i + 1) * (DIV + 1) + j);     // -x +y
            indices[p++] = make_int((i + 1) * (DIV + 1) + j + 1); // +x +y
            indices[p++] = make_int(i * (DIV + 1) + j);           // -x -y
            indices[p++] = make_int((i + 1) * (DIV + 1) + j + 1); // +x +y
            indices[p++] = make_int(i * (DIV + 1) + j + 1);       // +x -y
        }
    }

    setField("vertices", makeSequence(vertices));
    setField("indices", makeSequence(indices));
}

// Function to plot
function func(x: f64, y: f64, t: f64): f64 {
    // 2D wave animation
    return 0.1 * Math.sin(2 * Math.PI * (t + x + 0.5 * y));
}
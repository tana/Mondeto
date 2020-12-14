import "wasi";
import { make_vec, makeSequence, setField, make_int } from "mondeto-as";

const DIV: i32 = 32;

export function init(): void {
    trace("plot start");

    const vertices = new Array<u32>((DIV + 1) * (DIV + 1));
    const indices = new Array<u32>(6 * (DIV + 1) * (DIV + 1));
    // Plot
    for (let i = 0; i <= DIV; i++) {
        const y = 2.0 * i / DIV - 1.0;
        for (let j = 0; j <= DIV; j++) {
            const x = 2.0 * j / DIV - 1.0;
            const z = func(x, y);

            // f64 values are cast to f32 (see: https://www.assemblyscript.org/types.html#type-rules )
            vertices[i * (DIV + 1) + j] = make_vec(f32(x), f32(y), f32(-z));
        }
    }
    for (let i = 0; i < DIV; i++) {
        for (let j = 0; j < DIV; j++) {
            indices[6 * i * j] = make_int(i * (DIV + 1) + j);               // -x -y
            indices[6 * i * j + 1] = make_int((i + 1) * (DIV + 1) + j);     // -x +y
            indices[6 * i * j + 2] = make_int((i + 1) * (DIV + 1) + j + 1); // +x +y
            indices[6 * i * j + 3] = make_int(i * (DIV + 1) + j);           // -x -y
            indices[6 * i * j + 4] = make_int((i + 1) * (DIV + 1) + j + 1); // +x +y
            indices[6 * i * j + 5] = make_int(i * (DIV + 1) + j + 1);       // +x -y
        }
    }

    setField("vertices", makeSequence(vertices));
    setField("indices", makeSequence(indices));

    trace("plot done");
}

export function update(dt: f32): void {
}

// Function to plot
function func(x: f64, y: f64): f64 {
    const r = 10 * Math.sqrt(x * x + y * y);
    // Sinc function ( https://mathworld.wolfram.com/SincFunction.html )
    if (r < 0.001) return 1.0;
    else return Math.sin(r) / r;
}
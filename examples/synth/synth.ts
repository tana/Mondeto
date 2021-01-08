import "wasi";

const FS: f32 = 44100;  // sampling frequency

let t: f32 = 0;

export function generateSample(): f32 {
    const val = Mathf.sin(2 * Mathf.PI * 440 * t);
    t += 1 / FS;
    return val;
}
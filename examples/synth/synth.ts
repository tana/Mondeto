import "wasi";
import { SawtoothOscillator } from "./sawtoothOscillator";

const FS: f32 = 44100;  // sampling frequency

let sawtooth: SawtoothOscillator;

export function init(): void {
    sawtooth = new SawtoothOscillator(FS);
}

export function generateSample(): f32 {
    const val = f32(0.2) * sawtooth.compute();
    return val;
}
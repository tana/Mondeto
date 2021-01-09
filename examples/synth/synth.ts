import "wasi";
import { SawtoothOscillator } from "./sawtoothOscillator";
import { BiquadFilter } from "./biquadFilter";

const FS: f32 = 44100;  // sampling frequency

let sawtooth: SawtoothOscillator;
let filter: BiquadFilter;

export function init(): void {
    sawtooth = new SawtoothOscillator(FS);
    filter = new BiquadFilter(FS);
    filter.setCharacteristics(1000, 20);
}

export function generateSample(): f32 {
    const val = f32(0.2) * filter.compute(sawtooth.compute());
    return val;
}
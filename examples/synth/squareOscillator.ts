import { Oscillator } from "./oscillator";

// Square wave oscillator
// Output value is between -1 and 1
export class SquareOscillator extends Oscillator {
    private phase: f64 = 0; // phase (from 0 to 1)
    freq: f32 = 440;    // frequency (Hz)
    duty: f32 = 0.5;    // duty ratio (from 0 to 1)

    constructor(fs: f32) {
        super(fs);
    }

    compute(): f32 {
        // TODO: bandlimit (prevent aliasing)
        const output: f32 = (this.phase < this.duty) ? -1.0 : 1.0;

        const phaseIncr = this.freq * this.samplePeriod;
        this.phase = (this.phase + phaseIncr) % 1.0;

        return output;
    }

    // Generate waveform of one period (for display of setting)
    getWaveform(len: i32): Array<f32> {
        const array = new Array<f32>(len);
        for (let i = 0; i < len; i++) {
            array[i] = ((f32(i) / f32(len)) < this.duty) ? -1.0 : 1.0;
        }
        return array;
    }
}

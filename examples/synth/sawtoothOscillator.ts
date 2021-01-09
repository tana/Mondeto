// Sawtooth wave oscillator
// Output value is between -1 and 1
export class SawtoothOscillator {
    private readonly samplePeriod: f32;    // sampling period (seconds)
    private phase: f64 = 0; // phase (from 0 to 1)
    freq: f32 = 440;    // frequency (Hz)

    constructor(fs: f32) {
        this.samplePeriod = 1 / fs;
    }

    compute(): f32 {
        // TODO: bandlimit (prevent aliasing)
        const output = f32(2.0 * this.phase - 1.0);

        const phaseIncr = this.freq * this.samplePeriod;
        this.phase = (this.phase + phaseIncr) % 1.0;

        return output;
    }
}
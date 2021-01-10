import { SawtoothOscillator } from "./sawtoothOscillator";
import { BiquadFilter } from "./biquadFilter";

// Monophonic subtractive synthesizer
export class SubtractiveSynth {
    private readonly fs: f32;   // sampling freq
    private readonly osc: SawtoothOscillator;
    private readonly filter: BiquadFilter;
    private amp: f32 = 0.0;

    constructor(fs: f32) {
        this.fs = fs;
        this.osc = new SawtoothOscillator(this.fs);
        this.filter = new BiquadFilter(this.fs);

        this.filter.setCharacteristics(1000, 0.5);
    }

    compute(): f32 {
        return this.filter.compute(this.osc.compute());
    }

    noteOn(freq: f32): void {
        this.osc.freq = freq;
        this.amp = 1.0;
    }

    noteOff(): void {
        this.amp = 0.0;
    }
}
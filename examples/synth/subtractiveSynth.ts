import { SawtoothOscillator } from "./sawtoothOscillator";
import { BiquadFilter } from "./biquadFilter";
import { ADSLEnvelope } from "./adslEnvelope";

// Monophonic subtractive synthesizer
export class SubtractiveSynth {
    private readonly fs: f32;   // sampling freq
    private readonly osc: SawtoothOscillator;
    private readonly filter: BiquadFilter;
    private readonly envelope: ADSLEnvelope;

    constructor(fs: f32) {
        this.fs = fs;
        this.osc = new SawtoothOscillator(this.fs);
        this.filter = new BiquadFilter(this.fs);
        this.envelope = new ADSLEnvelope(this.fs);

        this.filter.setCharacteristics(1000, 0.5);
        this.envelope.attackTime = 0.1;
        this.envelope.decayTime = 0.5;
        this.envelope.sustainLevel = 0.3;
        this.envelope.releaseTime = 0.0;
    }

    compute(): f32 {
        return this.filter.compute(this.osc.compute()) * this.envelope.compute();
    }

    noteOn(freq: f32): void {
        this.osc.freq = freq;
        this.envelope.noteOn();
    }

    noteOff(): void {
        this.envelope.noteOff();
    }
}
import { SawtoothOscillator } from "./sawtoothOscillator";
import { BiquadFilter } from "./biquadFilter";
import { ADSLEnvelope } from "./adslEnvelope";

// Monophonic subtractive synthesizer
export class SubtractiveSynth {
    private readonly fs: f32;   // sampling freq
    private readonly osc: SawtoothOscillator;
    private readonly filter: BiquadFilter;
    private readonly envelope: ADSLEnvelope;
    private filterRelativeFreq: f32;
    private filterQ: f32;

    constructor(fs: f32) {
        this.fs = fs;
        this.osc = new SawtoothOscillator(this.fs);
        this.filter = new BiquadFilter(this.fs);
        this.envelope = new ADSLEnvelope(this.fs);

        this.setAmpADSR(0.1, 0.5, 0.3, 0.2);
        this.setFilterRelativeFreq(2);
        this.setFilterQ(0.5);
    }

    compute(): f32 {
        return this.filter.compute(this.osc.compute()) * this.envelope.compute();
    }

    noteOn(freq: f32): void {
        this.osc.freq = freq;
        this.setFilter();
        this.envelope.noteOn();
    }

    noteOff(): void {
        this.envelope.noteOff();
    }

    setAmpADSR(attackTime: f32, decayTime: f32, sustainLevel: f32, releaseTime: f32): void {
        this.envelope.attackTime = attackTime;
        this.envelope.decayTime = decayTime;
        this.envelope.sustainLevel = sustainLevel;
        this.envelope.releaseTime = releaseTime;
    }

    setFilterRelativeFreq(relativeFreq: f32): void {
        this.filterRelativeFreq = relativeFreq;
        this.setFilter();
    }

    setFilterQ(q: f32): void {
        this.filterQ = q;
        this.setFilter();
    }

    private setFilter(): void {
        const filterFreq = this.osc.freq * this.filterRelativeFreq;
        this.filter.setCharacteristics(filterFreq, this.filterQ);
    }
}
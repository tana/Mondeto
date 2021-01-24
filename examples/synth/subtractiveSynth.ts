import { SawtoothOscillator } from "./sawtoothOscillator";
import { BiquadFilter } from "./biquadFilter";
import { ADSREnvelope } from "./adsrEnvelope";

// Monophonic subtractive synthesizer
export class SubtractiveSynth {
    private readonly fs: f32;   // sampling freq
    private readonly osc: SawtoothOscillator;
    private readonly filter: BiquadFilter;
    private readonly envelope: ADSREnvelope;
    private filterRelativeFreq: f32;
    private filterQ: f32;

    constructor(fs: f32) {
        this.fs = fs;
        this.osc = new SawtoothOscillator(this.fs);
        this.filter = new BiquadFilter(this.fs);
        this.envelope = new ADSREnvelope(this.fs);

        this.setAmpAttack(0.1);
        this.setAmpDecay(0.5);
        this.setAmpRelease(0.3);
        this.setAmpRelease(0.2);
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

    setAmpAttack(attackTime: f32): void {
        this.envelope.attackTime = attackTime;
    }

    setAmpDecay(decayTime: f32): void {
        this.envelope.decayTime = decayTime;
    }

    setAmpSustain(sustainLevel: f32): void{
        this.envelope.sustainLevel = sustainLevel;
    }

    setAmpRelease(releaseTime: f32): void {
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

    getOscWaveform(len: i32): Array<f32> {
        return this.osc.getWaveform(len);
    }

    getFilterFrequencyResponse(len: i32): Array<f32> {
        return this.filter.getFrequencyResponse(len);
    }

    private setFilter(): void {
        const filterFreq = this.osc.freq * this.filterRelativeFreq;
        this.filter.setCharacteristics(filterFreq, this.filterQ);
    }
}
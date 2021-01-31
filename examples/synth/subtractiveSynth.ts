import { Oscillator } from "./oscillator";
import { SawtoothOscillator } from "./sawtoothOscillator";
import { NoiseOscillator } from "./noiseOscillator";
import { BiquadFilter } from "./biquadFilter";
import { ADSREnvelope } from "./adsrEnvelope";
import { SquareOscillator } from "./squareOscillator";

export enum OscillatorType {
    Sawtooth,
    Square,
    Noise
}

// Monophonic subtractive synthesizer
export class SubtractiveSynth {
    private readonly fs: f32;   // sampling freq
    private readonly sawtoothOsc: SawtoothOscillator;
    private readonly squareOsc: SquareOscillator;
    private readonly noiseOsc: NoiseOscillator;
    private osc: Oscillator;
    private readonly filter: BiquadFilter;
    private readonly envelope: ADSREnvelope;
    private freq: f32 = 440.0;
    private filterRelativeFreq: f32;
    private filterQ: f32;

    constructor(fs: f32) {
        this.fs = fs;
        this.sawtoothOsc = new SawtoothOscillator(this.fs);
        this.squareOsc = new SquareOscillator(this.fs);
        this.noiseOsc = new NoiseOscillator(this.fs);
        this.osc = this.sawtoothOsc;
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
        this.freq = freq;
        if (this.osc instanceof SawtoothOscillator) {
            (this.osc as SawtoothOscillator).freq = this.freq;
        } else if (this.osc instanceof SquareOscillator) {
            (this.osc as SquareOscillator).freq = this.freq;
        }
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

    set oscillatorType(oscType: OscillatorType) {
        switch (oscType) {
            case OscillatorType.Sawtooth:
                this.osc = this.sawtoothOsc;
                break;
            case OscillatorType.Square:
                this.osc = this.squareOsc;
                break;
            case OscillatorType.Noise:
                this.osc = this.noiseOsc;
                break;
        }
    }

    get oscillatorType(): OscillatorType {
        if (this.osc instanceof SawtoothOscillator) {
            return OscillatorType.Sawtooth;
        } else if (this.osc instanceof SquareOscillator) {
            return OscillatorType.Square;
        } else if (this.osc instanceof NoiseOscillator) {
            return OscillatorType.Noise;
        } else {    // This should not happen
            return OscillatorType.Sawtooth;
        }
    }

    getOscWaveform(len: i32): Array<f32> {
        return this.osc.getWaveform(len);
    }

    getFilterFrequencyResponse(len: i32): Array<f32> {
        return this.filter.getFrequencyResponse(len);
    }
    
    getEnvelopeCurve(len: i32): Array<f32> {
        return this.envelope.getEnvelopeCurve(len);
    }

    private setFilter(): void {
        const filterFreq = this.freq * this.filterRelativeFreq;
        this.filter.setCharacteristics(filterFreq, this.filterQ);
    }
}
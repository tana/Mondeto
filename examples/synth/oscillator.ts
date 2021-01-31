export abstract class Oscillator {
    protected readonly fs: f32; // sampling rate (Hz)
    protected readonly samplePeriod: f32;    // sampling period (seconds)

    constructor(fs: f32) {
        this.fs = fs;
        this.samplePeriod = 1 / fs;
    }

    abstract compute(): f32;

    // Generate waveform of one period (for display of setting)
    abstract getWaveform(len: i32): Array<f32>;
}
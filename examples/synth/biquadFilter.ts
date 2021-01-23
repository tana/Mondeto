import { Complex } from "./complex";

// IIR Biquad low-pass filter
// Calculation is based on:
//  Robert Bristow-Johnson, "Cookbook formulae for audio equalizer biquad filter coefficients" (edited by Raymond Toy and Doug Schepers)
//  https://webaudio.github.io/Audio-EQ-Cookbook/audio-eq-cookbook.html
export class BiquadFilter {
    private readonly fs: f32;   // sampling frequency
    // Filter coefficients
    private a0: f32 = 1;
    private a1: f32 = 0;
    private a2: f32 = 0;
    private b0: f32 = 1;
    private b1: f32 = 0;
    private b2: f32 = 0;
    // Past input and output samples
    private x1: f32 = 0;    // x[n-1]
    private x2: f32 = 0;    // x[n-2]
    private y1: f32 = 0;    // y[n-1]
    private y2: f32 = 0;    // y[n-2]

    constructor(fs: f32) {
        this.fs = fs;
    }

    compute(x: f32): f32 {
        // See: Equation 4 of the Cookbook
        const y =
            (this.b0 / this.a0) * x
            + (this.b1 / this.a0) * this.x1
            + (this.b2 / this.a0) * this.x2
            - (this.a1 / this.a0) * this.y1
            - (this.a2 / this.a0) * this.y2;

        this.x2 = this.x1;
        this.x1 = x;
        this.y2 = this.y1;
        this.y1 = y;

        return y;
    }

    // Change filter characteristics
    // Currently, only low-pass filter is supported
    setCharacteristics(f0: f32, q: f32): void {
        // Calculate filter coefficients using formulae of Bristow-Johnson's article
        const omega0 = f32(2) * Mathf.PI * f0 / this.fs;
        const c = Mathf.cos(omega0);
        const s = Mathf.sin(omega0);
        const alpha = s / (2 * q);
        this.b0 = (1 - c) / 2;
        this.b1 = 1 - c;
        this.b2 = (1 - c) / 2;
        this.a0 = 1 + alpha;
        this.a1 = -2 * c;
        this.a2 = 1 - alpha;
    }

    // Generate frequnency response (in dB, from 1 Hz to Nyquist freqency)
    getFrequencyResponse(len: i32): Array<f32> {
        const array = new Array<f32>(len);
        const maxFreq = this.fs / 2;
        for (let i = 0; i < len; i++) {
            const freq = Mathf.pow(10, Mathf.log10(maxFreq) * f32(i) / len);
            // Compute frequency response of a transfer function H(z)
            //  https://ccrma.stanford.edu/~jos/fp/Frequency_Response_I.html
            const z = (new Complex(0, 2 * Mathf.PI * freq / this.fs)).exp();
            const zMinus1 = new Complex(1, 0) / z;
            const zMinus2 = new Complex(1, 0) / (z * z);
            // H(z) (See: Equation 1 of the Cookbook)
            const h = (this.b0 + zMinus1.multiply(this.b1) + zMinus2.multiply(this.b2)) / (this.a0 + zMinus1.multiply(this.a1) + zMinus2.multiply(this.b2));
            array[i] = 20 * Mathf.log10(h.abs());
        }
    }
}
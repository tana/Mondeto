import { Oscillator } from "./oscillator";

// White noise oscillator
// Output value is between -1 and 1
export class NoiseOscillator extends Oscillator {
    private readonly stddev: f32 = 0.4; // TODO:

    constructor(fs: f32) {
        super(fs);
    }

    compute(): f32 {
        return gaussianRandom() * this.stddev;
    }

    // Generate waveform of one period (for display of setting)
    getWaveform(len: i32): Array<f32> {
        const array = new Array<f32>(len);
        for (let i = 0; i < len; i++) {
            array[i] = gaussianRandom() * this.stddev;
        }
        return array;
    }
}

// Generate standard Gaussian random number using Box-Muller transformation
// See: https://mathworld.wolfram.com/Box-MullerTransformation.html
function gaussianRandom(): f32 {
    const x1 = Mathf.random();
    const x2 = Mathf.random();
    // Although Box-Muller generates two independent Gaussian random numbers,
    // Only one of them was used here.
    return Mathf.sqrt(-2 * Mathf.log(x1)) * Mathf.cos(2 * Mathf.PI * x2);
}
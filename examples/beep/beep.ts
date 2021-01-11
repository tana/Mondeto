import "wasi";
import { setField, make_vec, writeAudio } from "mondeto-as";

const FS: f32 = 48000;   // Sampling rate

const BEEP_FREQ: f32 = 1000; // Frequency of beep sound
const BEEP_AMP: f32 = 0.01;  // Amplitude of beep sound (warning: 1.0 is extremely loud!)

let beeping = false;

let phase: f32 = 0; // Phase of sine wave

export function init(): void {
    trace("Beep init");
}

export function update(dt: f32): void {
    if (!beeping) return;   // If not beeping, do nothing

    // Calculate number of samples generated in this update
    // TODO: it sometimes cause overflow or underflow of audio signal (and glitch). Improvement is needed.
    const len = i32(FS * dt);

    // Generate sine wave signal
    const samples = new Array<f32>(len);
    for (let i = 0; i < len; i++) {
        samples[i] = BEEP_AMP * Mathf.sin(phase);
        phase = (phase + 2.0 * Mathf.PI * BEEP_FREQ / FS)
    }

    // Play signal
    writeAudio(samples);
}

// Toggle on/off by click
export function handle_clickStart(sender: u32): void {
    beeping = !beeping;
    
    // Change color based on beeping state
    if (beeping) {
        setField("color", make_vec(1.0, 0.0, 0.0));
    } else {
        setField("color", make_vec(1.0, 1.0, 1.0));
    }
}
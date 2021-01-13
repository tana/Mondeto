import "wasi";
import { writeAudio, getEventArgs, read_int } from "mondeto-as";
import { SubtractiveSynth } from "./subtractiveSynth";

const FS: f32 = 48000;  // sampling frequency
const CHUNK_SIZE = 960;

let synth: SubtractiveSynth;

let pos = 0;

let samples: Array<f32>;

export function init(): void {
    synth = new SubtractiveSynth(FS);

    samples = new Array<f32>(CHUNK_SIZE);
}

export function update(dt: f32): void {
    for (let i = 0; i < CHUNK_SIZE; i++) {
        samples[i] = generateSample();
    }
    writeAudio(samples);
}

// Handle noteOn event sent from keys
export function handle_noteOn(sender: u32): void {
    const args = getEventArgs();
    if (args.length < 1) return;
    const noteNum = read_int(args[0]);  // MIDI note number

    synth.noteOn(midiNoteToFreq(noteNum));
}

// Handle noteOff event sent from keys
export function handle_noteOff(sender: u32): void {
    synth.noteOff();
}

function generateSample(): f32 {
    const val = f32(0.2) * synth.compute();
    return val;
}

// Convert MIDI note number to frequency
//  See: https://en.wikipedia.org/wiki/MIDI_tuning_standard (accessed on Jan 10, 2021)
function midiNoteToFreq(note: i32): f32 {
    return Mathf.pow(2, f32(note - 69) / 12) * f32(440);
}
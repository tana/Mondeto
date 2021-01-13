import "wasi";
import { writeAudio } from "mondeto-as";
import { SubtractiveSynth } from "./subtractiveSynth";

const FS: f32 = 48000;  // sampling frequency
const CHUNK_SIZE = 960;

// note constants
// See: http://newt.phys.unsw.edu.au/jw/notes.html
// Only white keys. To change octave, add or subtract 12.
enum NoteNumbers {
    C = 60,
    D = 62,
    E = 64,
    F = 65,
    G = 67,
    A = 69,
    B = 71
}

let time: f64 = 0.0;    // using f64 because f32 precision is not enough for long time (minutes)
let synth: SubtractiveSynth;

let pos = 0;
let notes: Array<i32>;

let samples: Array<f32>;

export function init(): void {
    synth = new SubtractiveSynth(FS);
    notes = [
        NoteNumbers.C,
        NoteNumbers.D,
        NoteNumbers.E,
        NoteNumbers.F,
        NoteNumbers.G,
        NoteNumbers.A,
        NoteNumbers.B,
        NoteNumbers.C + 12
    ];

    samples = new Array<f32>(CHUNK_SIZE);
}

export function update(dt: f32): void {
    for (let i = 0; i < CHUNK_SIZE; i++) {
        samples[i] = generateSample();
    }
    writeAudio(samples);
}

function generateSample(): f32 {
    if ((time % 1.0) < (1 / FS)) {
        synth.noteOff();
        synth.noteOn(midiNoteToFreq(notes[pos % notes.length]));
        pos++;
    }

    const val = f32(0.2) * synth.compute();
    time += 1 / FS;
    return val;
}

// Convert MIDI note number to frequency
//  See: https://en.wikipedia.org/wiki/MIDI_tuning_standard (accessed on Jan 10, 2021)
function midiNoteToFreq(note: i32): f32 {
    return Mathf.pow(2, f32(note - 69) / 12) * f32(440);
}
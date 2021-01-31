import "wasi";
import { writeAudio, getEventArgs, read_int, read_float, sendEvent, getField, make_float, read_object_ref, makeSequence } from "mondeto-as";
import { SubtractiveSynth, OscillatorType } from "./subtractiveSynth";

const FS: f32 = 48000;  // sampling frequency
const CHUNK_SIZE = 960;
const PLOT_LEN = 100;

let synth: SubtractiveSynth;

let samples: Array<f32>;

export function init(): void {
    synth = new SubtractiveSynth(FS);

    samples = new Array<f32>(CHUNK_SIZE);

    plotOscWaveform();
    plotFilter();
    plotEnvelope();
}

export function update(dt: f32): void {
    for (let i = 0; i < CHUNK_SIZE; i++) {
        samples[i] = generateSample();
    }
    writeAudio(samples);

    visualize(samples);
}

// Handle noteOn event sent from keys
export function handle_noteOn(sender: u32): void {
    const args = getEventArgs();
    if (args.length < 1) return;
    const noteNum = read_int(args[0]);  // MIDI note number

    synth.noteOn(midiNoteToFreq(noteNum));
    plotFilter();
}

// Handle noteOff event sent from keys
export function handle_noteOff(sender: u32): void {
    synth.noteOff();
}

// Handle changeWaveform event sent from a button
export function handle_changeWaveform(sender: u32): void {
    if (synth.oscillatorType === OscillatorType.Sawtooth) {
        synth.oscillatorType = OscillatorType.Noise;
    } else if (synth.oscillatorType === OscillatorType.Noise) {
        synth.oscillatorType = OscillatorType.Sawtooth;
    }

    plotOscWaveform();
}

// Handle events from sliders
export function handle_setAttack(sender: u32): void {
    const args = getEventArgs();
    if (args.length < 1) return;
    synth.setAmpAttack(read_float(args[0]));
    plotEnvelope();
}
export function handle_setDecay(sender: u32): void {
    const args = getEventArgs();
    if (args.length < 1) return;
    synth.setAmpDecay(read_float(args[0]));
    plotEnvelope();
}
export function handle_setSustain(sender: u32): void {
    const args = getEventArgs();
    if (args.length < 1) return;
    synth.setAmpSustain(read_float(args[0]));
    plotEnvelope();
}
export function handle_setRelease(sender: u32): void {
    const args = getEventArgs();
    if (args.length < 1) return;
    synth.setAmpRelease(read_float(args[0]));
    plotEnvelope();
}
export function handle_setCutOff(sender: u32): void {
    const args = getEventArgs();
    if (args.length < 1) return;
    synth.setFilterRelativeFreq(3 * read_float(args[0]));   // from 0 to 3
    plotFilter();
}
export function handle_setResonance(sender: u32): void {
    const args = getEventArgs();
    if (args.length < 1) return;
    synth.setFilterQ(Mathf.pow(2, 4 * read_float(args[0]) - 2));    // from 0.25 to 4
    plotFilter();
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

function plotOscWaveform(): void {
    plot(synth.getOscWaveform(PLOT_LEN), "oscWaveformPlot");
}

function plotFilter(): void {
    plot(synth.getFilterFrequencyResponse(PLOT_LEN), "filterPlot");
}

function plotEnvelope(): void {
    plot(synth.getEnvelopeCurve(PLOT_LEN), "envelopePlot");
}

function visualize(samples: f32[]): void {
    plot(samples, "visualizerPlot");
}

function plot(array: f32[], targetName: string): void {
    const target = read_object_ref(getField(targetName) as u32);

    const valueIDs = new Array<u32>(array.length);
    for (let i = 0; i < array.length; i++) {
        valueIDs[i] = make_float(array[i]);
    }
    
    sendEvent(target, "plot", [makeSequence(valueIDs)]);
}
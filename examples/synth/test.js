const fs = require("fs");
const { env } = require("process");
const Speaker = require("speaker");
const { WASI } = require("wasi");
const { plot } = require("nodeplotlib");

const FS = 44100;
const CHUNK_SIZE = 1024;

// Plot signal using NodePlotLib
//  See: https://www.npmjs.com/package/nodeplotlib
function plotSignal(array) {
    // Generate time axis
    const t = new Array(array.length);
    for (let i = 0; i < array.length; i++) {
        t[i] = i / FS;
    }

    plot([{
        x: t,
        y: Array.from(array),   // Float32Array have be converted into Array
        type: 'line'
    }]);
}

// Use Node.js WASI
//  See: https://nodejs.org/api/wasi.html
const wasi = new WASI();
const imports = { "wasi_snapshot_preview1": wasi.wasiImport };

const fileBuf = fs.readFileSync("synth.wasm");
(async function () {
    const { module, instance } = await WebAssembly.instantiate(fileBuf, imports);

    instance.exports.init();

    // Sound output using Speaker
    // See: https://www.npmjs.com/package/speaker
    const speaker = new Speaker({
        channels: 1,
        sampleRate: FS,
        bitDepth: 16,
        signed: true
    });

    const array = new Int16Array(CHUNK_SIZE);
    const buf = Buffer.from(array.buffer);
    const plotArray = new Float32Array(CHUNK_SIZE);
    let plotted = false;
    function generate() {
        for (let i = 0; i < CHUNK_SIZE; i++) {
            let value = instance.exports.generateSample();
            plotArray[i] = value;
            array[i] = 32767 * value;
        }
        speaker.write(buf, generate);

        if (!plotted) {
            plotSignal(plotArray);
            plotted = true;
        }
    }
    generate();
})().catch(err => { throw err });
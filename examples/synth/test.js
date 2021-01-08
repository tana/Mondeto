const fs = require("fs");
const Speaker = require("speaker");
const { WASI } = require("wasi");

const FS = 44100;
const CHUNK_SIZE = 1024;

// Use Node.js WASI
//  See: https://nodejs.org/api/wasi.html
const wasi = new WASI();
const imports = { "wasi_snapshot_preview1": wasi.wasiImport };

const fileBuf = fs.readFileSync("synth.wasm");
(async function () {
    const { module, instance } = await WebAssembly.instantiate(fileBuf, imports);

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
    function generate() {
        for (let i = 0; i < CHUNK_SIZE; i++) {
            let value = instance.exports.generateSample();
            array[i] = 32767 * value;
        }
        speaker.write(buf, generate);
    }
    generate();
})().catch(err => { throw err });
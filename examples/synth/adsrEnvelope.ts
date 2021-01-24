export class ADSREnvelope {
    private readonly fs: f32;
    private isOn: bool = false;
    private isReleasing: bool = false;
    private noteTime: f32 = 0;  // time after noteOn
    private timeAfterOff: f32 = 0;   // time after NoteOff
    // parameters
    attackTime: f32 = 0;    // time of linear increase between noteOn and maximum amplitude
    decayTime: f32 = 0;     // time of linear decay between maximam amplitude and sustain level
    sustainLevel: f32 = 1;  // amplitude during sustain state
    releaseTime: f32 = 0;   // time of linear decay between noteOff and complete stop

    constructor(fs: f32) {
        this.fs = fs;
    }

    compute(): f32 {
        let value: f32;
        if (this.isOn) {
            if (this.noteTime < this.attackTime) {    // Attack state
                value = this.noteTime / this.attackTime;
            } else if (this.noteTime < (this.attackTime + this.decayTime)) {  // Decay state
                value = 1 - (1 - this.sustainLevel) * (this.noteTime - this.attackTime) / this.decayTime;
            } else {    // Sustain state
                value = this.sustainLevel;
            }

            this.noteTime += 1 / this.fs;
        } else if (this.isReleasing) {   // Release state
            if (this.timeAfterOff >= this.releaseTime) {
                this.isReleasing = false;    // sound is completely stopped
            }

            value = this.sustainLevel * (1 - this.timeAfterOff / this.releaseTime);

            this.timeAfterOff += 1 / this.fs;
        } else {
            value = 0;
        }

        return value;
    }

    noteOn(): void {
        this.noteTime = 0;
        this.isOn = true;
    }

    noteOff(): void {
        if (this.isOn) {
            this.isOn = false;
            this.isReleasing = true;
            this.timeAfterOff = 0;
        }
    }

    // Generate curve of envelope
    getEnvelopeCurve(len: i32): Array<f32> {
        const array = new Array<f32>(len);
        const duration: f32 = 3.0;
        const sustainLen: f32 = 1.0;
        for (let i = 0; i < len; i++) {
            const t = duration * f32(i) / f32(len);
            let value: f32;
            if (t < this.attackTime) {
               value = t / this.attackTime;
            } else if (t < (this.attackTime + this.decayTime)) {
                value = 1 - (1 - this.sustainLevel) * (t - this.attackTime) / this.decayTime;
            } else if (t < (this.attackTime + this.decayTime + sustainLen)) {
                value = this.sustainLevel;
            } else if (t < (this.attackTime + this.decayTime + sustainLen + this.releaseTime)) {
                value = this.sustainLevel * (1 - (t - this.attackTime - this.decayTime - sustainLen) / this.releaseTime);
            } else {
                value = 0;
            }
            array[i] = value;
        }

        return array;
    }
}
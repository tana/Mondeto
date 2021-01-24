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
            } else if (this.noteTime < this.decayTime) {  // Decay state
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
}
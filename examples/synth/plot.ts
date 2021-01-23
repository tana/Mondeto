import "wasi";
import { getEventArgs, readSequence, read_float, make_vec, getField, setField, makeSequence } from "mondeto-as";

export function init(): void {
}

export function handle_plot(sender: u32): void {
    const args = getEventArgs();
    if (args.length < 1) return;
    const yIDs = readSequence(args[0]);
    const len = yIDs.length;

    const minValue = read_float(getField("minValue") as u32);
    const maxValue = read_float(getField("maxValue") as u32);

    const pointIDs = new Array<u32>(len);

    for (let i = 0; i < len; i++) {
        const y = read_float(yIDs[i]);

        pointIDs[i] = make_vec(
            f32(i) / f32(len),
            (y - minValue) / (maxValue - minValue),
            0
        );
    }

    setField("points", makeSequence(pointIDs));
}
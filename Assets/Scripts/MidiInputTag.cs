using System.Collections.Concurrent;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Devices;
using Mondeto.Core;

namespace Mondeto
{

public class MidiInputTag : ITag
{
    InputDevice device;
    ConcurrentQueue<MidiEvent> queue = new ConcurrentQueue<MidiEvent>();

    public void Setup(SyncObject syncObject)
    {
        if (syncObject.OriginalNodeId != syncObject.Node.NodeId)
        {
            Logger.Log("MidiInputTag", "midiInput tag only works on the original node");
            return;
        }

        if (InputDevice.GetDevicesCount() == 0)
        {
            Logger.Error("MidiInputTag", "MIDI input device not found");
            return;
        }

        device = InputDevice.GetById(0);    // Open the first device
        device.StartEventsListening();
        device.EventReceived += OnEventReceived;

        syncObject.BeforeSync += OnBeforeSync;  // Add update function
    }

    // Called when a MIDI event is received from the MIDI device
    void OnEventReceived(object sender, MidiEventReceivedEventArgs eventArgs)
    {
        // This event handler is invoked in different thread.
        //  See: https://melanchall.github.io/drywetmidi/articles/devices/Common-problems.html#startcoroutine-can-only-be-called-from-the-main-thread-in-unity
        // Send MIDI event to the main thread
        queue.Enqueue(eventArgs.Event);
    }

    // Called when updating this object
    void OnBeforeSync(SyncObject syncObject, float dt)
    {
        // Process events received from another thread (which runs OnEventReceived)
        while (queue.TryDequeue(out var midiEvent))
        {
            // Currently, only NoteOn and NoteOff events are supported.
            switch (midiEvent)
            {
                case NoteOnEvent noteOnEvent:
                    OnNoteOn(syncObject, noteOnEvent);
                    break;
                case NoteOffEvent noteOffEvent:
                    OnNoteOff(syncObject, noteOffEvent);
                    break;
            }
        }
    }

    void OnNoteOn(SyncObject syncObject, NoteOnEvent noteOnEvent)
    {
        syncObject.SendEvent(
            "noteOn",
            syncObject.Id,
            new IValue[] { new Primitive<int>(noteOnEvent.NoteNumber) },
            localOnly: true
        );
    }

    void OnNoteOff(SyncObject syncObject, NoteOffEvent noteOnEvent)
    {
        syncObject.SendEvent(
            "noteOff",
            syncObject.Id,
            new IValue[] { },
            localOnly: true
        );
    }

    public void Cleanup(SyncObject syncObject)
    {
        if (device != null)
        {
            device.EventReceived -= OnEventReceived;
            device.Dispose();
        }

        syncObject.BeforeSync -= OnBeforeSync;
    }
}

}   // end namespace
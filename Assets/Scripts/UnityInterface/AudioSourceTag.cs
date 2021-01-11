using System;
using UnityEngine;

public class AudioSourceTag : MonoBehaviour, ITag
{
    AudioSource audioSource;
    SyncObject obj;
    ObjectSync objectSync;
    AudioClip outClip;
    int outPos;

    float[] overflow;

    public void Setup(SyncObject syncObject)
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        obj = syncObject;
        objectSync = GetComponent<ObjectSync>();

        audioSource.Stop();

        outClip = AudioClip.Create("", SyncObject.AudioSamplingRate, 1, SyncObject.AudioSamplingRate, false);
        audioSource.loop = false;
        audioSource.clip = outClip;

        obj.AudioReceived += OnAudioReceived;
    }

    void OnAudioReceived(float[] data)
    {
        // When noLocalAudioOutput is true, don't play sound on original node
        if (objectSync.IsOriginal &&
            obj.TryGetFieldPrimitive("noLocalAudioOutput", out int doNotPlayLocally) &&
            doNotPlayLocally != 0)
        {
            return;
        }

        if (data.Length == 0) return;   // When array has no element, SetData raises an exception

        try
        {
            if (overflow != null)
            {
                outClip.SetData(overflow, outPos);
                outPos += overflow.Length;
            }
            outClip.SetData(data, outPos);
            outPos += data.Length;
        }
        catch (ArgumentException e)
        {
            Debug.Log(e);
        }
        if (outPos > outClip.samples)
        {
            // When outClip overflowed
            overflow = new float[outPos - outClip.samples];
            Array.Copy(data, data.Length - overflow.Length, overflow, 0, overflow.Length);
            outPos = 0;
            Logger.Debug("AudioSourceTag", $"Overflow {overflow.Length} samples");
        }
        else if (outPos == outClip.samples)
        {
            // It expects the playback of current clip ends before new audio data arrives.
            // If not, some glitch may occur.
            outPos = 0;
        }
        else
        {
            overflow = null;
        }

        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    public void Cleanup(SyncObject syncObject)
    {
        Destroy(this);
    }

    void OnDestroy()
    {
        obj.AudioReceived -= OnAudioReceived;
        Destroy(audioSource);
    }
}
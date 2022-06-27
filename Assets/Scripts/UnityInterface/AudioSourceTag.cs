using System;
using System.Collections.Concurrent;
using UnityEngine;
using Mondeto.Core;

namespace Mondeto
{

public class AudioSourceTag : MonoBehaviour, ITag
{
    AudioSource audioSource;
    SyncObject obj;
    ObjectSync objectSync;
    ConcurrentQueue<float> audioQueue = new ConcurrentQueue<float>();

    public void Setup(SyncObject syncObject)
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        obj = syncObject;
        objectSync = GetComponent<ObjectSync>();

        // Ensure Unity's output sampling rate is same as Mondeto's internal sampling rate (48kHz)
        if (AudioSettings.outputSampleRate != SyncObject.AudioSamplingRate)
        {
            Mondeto.Core.Logger.Error("AudioSourceTag", $"Output sample rate is not {SyncObject.AudioSamplingRate} Hz (actual value = {AudioSettings.outputSampleRate} Hz)");
            return;
        }

        audioSource.spatialize = true;
        audioSource.spatialBlend = 1.0f;

        audioSource.clip = AudioClip.Create("", SyncObject.AudioSamplingRate, 1, SyncObject.AudioSamplingRate, false);
        audioSource.loop = true;

        obj.AudioReceived += OnAudioReceived;

        audioSource.Play();
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

        foreach (var sample in data)
        {
            audioQueue.Enqueue(sample);
        }
    }

    void OnAudioFilterRead(float[] buf, int channels)
    {
        var len = buf.Length / channels;

        for (int i = 0; i < len; i++)
        {
            float sample = 0.0f;
            audioQueue.TryDequeue(out sample);

            for (int j = 0; j < channels; j++)
            {
                buf[channels * i + j] = sample;
            }
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

}   // end namespace
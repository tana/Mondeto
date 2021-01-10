using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MicrophoneCapture : MonoBehaviour
{
    public string DeviceName = "";

    private bool micEnabled = true;

    public bool MicrophoneEnabled
    {
        get => micEnabled;
        set
        {
            micEnabled = value;
            Logger.Debug("MicrophoneCapture", "Microphone " + (micEnabled ? "on" : "off"));
        }
    }

    const int SamplingRate = SyncObject.AudioSamplingRate;
    const int ClipLength = 1;

    bool ready;

    AudioClip micClip;
    int lastPos;
    float[] tempBuf;
    float[] buf = new float[SamplingRate];  // 1 sec

    AudioClip outClip;
    int outPos;

    float[] overflow;

    void Update()
    {
        if (!ready) return;

        ObjectSync sync = GetComponent<ObjectSync>();
        if (sync.IsOriginal && MicrophoneEnabled)
        {
            CaptureAndSend();
        }
    }
    
    void CaptureAndSend()
    {
        int pos = Microphone.GetPosition(DeviceName);
        micClip.GetData(tempBuf, 0);
        int len;    // Number of samples captured after last frame
        if (pos >= lastPos)
        {
            Array.Copy(tempBuf, lastPos, buf, 0, pos - lastPos);
            len = pos - lastPos;
        }
        else
        {
            // When recording position on micClip is looped back
            Array.Copy(tempBuf, lastPos, buf, 0, micClip.samples - lastPos);
            Array.Copy(tempBuf, 0, buf, micClip.samples - lastPos, pos);
            len = (micClip.samples - lastPos) + pos;
        }
        lastPos = pos;

        ObjectSync sync = GetComponent<ObjectSync>();

        // Convert to byte array (TODO encoding)
        len = Math.Min(len, SyncObject.OpusFrameSize);  // FIXME
        var buf2 = new float[len];
        Array.Copy(buf, buf2, len);

        sync.SyncObject.SendAudio(buf2);
    }

    void OnSyncReady()
    {
        ObjectSync sync = GetComponent<ObjectSync>();
        if (sync.IsOriginal)
        {
            Logger.Debug("MicrophoneCapture", $"MicrophoneCapture Original (obj={gameObject.name})");
            micClip = Microphone.Start(DeviceName, true, ClipLength, SamplingRate);
            tempBuf = new float[micClip.samples];
        }
        else
        {
            Logger.Debug("MicrophoneCapture", $"MicrophoneCapture Clone (obj={gameObject.name})");
            var source = GetComponent<AudioSource>();
            source.Stop();

            outClip = AudioClip.Create("", SamplingRate, 1, SamplingRate, false);
            source.loop = false;
            source.clip = outClip;

            sync.SyncObject.AudioReceived += OnAudioReceived;
        }

        ready = true;
    }

    void OnAudioReceived(float[] data)
    {
        if (data.Length == 0) return;   // When array has no element, SetData raises an exception

        var source = GetComponent<AudioSource>();

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
            Logger.Debug("MicrophoneCapture", $"Overflow {overflow.Length} samples");
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

        if (!source.isPlaying)
        {
            source.Play();
        }
    }
    
    void OnDestroy()
    {
        Microphone.End(DeviceName);
    }
}
using System;
using UnityEngine;

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

        sync.SyncObject.WriteAudio(buf2);
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
        }

        ready = true;
    }
    
    void OnDestroy()
    {
        Microphone.End(DeviceName);
    }
}
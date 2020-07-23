using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MicrophoneCapture : MonoBehaviour
{
    public string DeviceName = "";

    public bool MicrophoneEnabled { get; set; } = true;

    const int SamplingRate = 8000;
    const int ClipLength = 1;

    bool ready;

    AudioClip micClip;
    int lastPos;
    float[] tempBuf;
    float[] buf = new float[SamplingRate];  // 1 sec

    AudioClip outClip;
    int outPos;

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
        len = Math.Min(len, 1024);  // FIXME
        var data = new byte[len];
        for (int i = 0; i < len; i++)
        {
            data[i] = (byte)(127 * buf[i] + 127);
        }

        sync.SyncObject.SendAudio(data);
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
            source.loop = true;
            source.clip = outClip;
            source.Play();

            sync.SyncObject.AudioReceived += OnAudioReceived;
        }

        ready = true;
    }

    void OnAudioReceived(byte[] data)
    {
        if (data.Length == 0) return;   // When array has no element, SetData raises an exception
        float[] buf = data.Select(b => 2 * (float)b / 256 - 1).ToArray();
        outClip.SetData(buf, outPos);
        outPos = (outPos + data.Length) % outClip.samples;
    }
    
    void OnDestroy()
    {
        Microphone.End(DeviceName);
    }
}
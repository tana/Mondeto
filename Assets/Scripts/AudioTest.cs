using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class AudioTest : MonoBehaviour
{
    const string micDev = "";
    const int fs = 8000;
    AudioClip clip;
    int lastPos;
    float[] audioData;

    float[] buf = new float[fs];

    AudioClip outClip;
    int outPos;

    // Start is called before the first frame update
    void Start()
    {
        outClip = AudioClip.Create("out", 10 * fs, 1, fs, false);
        GetComponent<AudioSource>().clip = outClip;
        GetComponent<AudioSource>().loop = true;

        clip = Microphone.Start(micDev, true, 2, fs);
        audioData = new float[clip.samples];

        LatePlay();
    }

    async void LatePlay()
    {
        await Task.Delay(1000);
        GetComponent<AudioSource>().Play();
    }

    // Update is called once per frame
    void Update()
    {
        int pos = Microphone.GetPosition(micDev);
        clip.GetData(audioData, 0);
        int len;
        if (pos >= lastPos)
        {
            Array.Copy(audioData, lastPos, buf, 0, pos - lastPos);
            len = pos - lastPos;
        }
        else
        {
            Debug.Log("loop");
            Array.Copy(audioData, lastPos, buf, 0, clip.samples - lastPos);
            Array.Copy(audioData, 0, buf, clip.samples - lastPos, pos);
            len = (clip.samples - lastPos) + pos;
        }
        lastPos = pos;

        outClip.SetData(buf, outPos);
        outPos = (outPos + len) % outClip.samples;

        Debug.Log(len);
    }

    void OnDestroy()
    {
        Microphone.End(micDev);
    }
}

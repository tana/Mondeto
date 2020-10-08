using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;

public class AudioPlayer : MonoBehaviour
{
    SyncObject obj;

    AudioSource audioSource;

    public void Initialize(SyncObject obj)
    {
        this.obj = obj;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialize = true;
        audioSource.spatialBlend = 1.0f;

        obj.RegisterFieldUpdateHandler("audioFile", HandleFileUpdate);
        obj.RegisterFieldUpdateHandler("audioVolume", HandleUpdate);

        HandleFileUpdate();
        HandleUpdate();
    }

    async void HandleFileUpdate()
    {
        if (obj.TryGetField("audioFile", out BlobHandle blobHandle))
        {
            string path = await obj.Node.GetBlobTempFile(blobHandle);
            string fullPath = System.IO.Path.GetFullPath(path);
            // Use file URI scheme to load audio file at runtime
            // See: https://stackoverflow.com/questions/30852691/loading-mp3-files-at-runtime-in-unity
            //  (although we use UnityWebRequest instead of deprecated WWW class)
            // See: https://docs.unity3d.com/ja/2019.4/ScriptReference/Networking.UnityWebRequestMultimedia.GetAudioClip.html
            UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip("file://" + fullPath, AudioType.WAV);
            await request.SendWebRequest();
            if (request.isDone)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                audioSource.clip = clip;
                audioSource.loop = true;
                audioSource.Play();
            }
        }
    }

    void HandleUpdate()
    {
        if (obj.TryGetFieldPrimitive("audioVolume", out float audioVolume))
        {
            audioSource.volume = audioVolume;
        }
    }

    public void OnDestroy()
    {
        obj.DeleteFieldUpdateHandler("audioFile", HandleFileUpdate);
        Destroy(audioSource);
    }
}
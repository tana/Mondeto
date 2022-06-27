using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;
using Mondeto.Core;

namespace Mondeto
{

public class AudioPlayer : MonoBehaviour, ITag
{
    SyncObject obj;

    AudioSource audioSource;

    public void Setup(SyncObject obj)
    {
        this.obj = obj;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialize = true;
        audioSource.spatialBlend = 1.0f;

        obj.RegisterFieldUpdateHandler("audioFile", HandleFileUpdate);
        obj.RegisterFieldUpdateHandler("audioVolume", HandleUpdate);
        obj.RegisterFieldUpdateHandler("audioPlaying", HandleUpdate);
        obj.RegisterFieldUpdateHandler("audioLoop", HandleUpdate);

        obj.BeforeSync += OnBeforeSync;

        HandleFileUpdate();
        HandleUpdate();
    }

    async void HandleFileUpdate()
    {
        if (obj.TryGetField("audioFile", out BlobHandle blobHandle))
        {
            (string path, string mimeType) = await obj.Node.GetBlobTempFile(blobHandle);
            string fullPath = System.IO.Path.GetFullPath(path);

            // Use file URI scheme to load audio file at runtime
            // See: https://stackoverflow.com/questions/30852691/loading-mp3-files-at-runtime-in-unity
            //  (although we use UnityWebRequest instead of deprecated WWW class)
            // See: https://docs.unity3d.com/ja/2019.4/ScriptReference/Networking.UnityWebRequestMultimedia.GetAudioClip.html

            // Convert MIME type to Unity AudioType
            AudioType audioType;
            switch (mimeType)
            {
                // See ../../config/mime.types
                case "audio/x-wav":
                    audioType = AudioType.WAV;
                    break;
                case "audio/mpeg":
                    audioType = AudioType.MPEG;
                    break;
                case "audio/ogg":
                    audioType = AudioType.OGGVORBIS;
                    break;
                case "audio/x-aiff":
                    audioType = AudioType.AIFF;
                    break;
                default:
                    obj.WriteErrorLog("AudioPlayer", "Unknown audio file type: " + mimeType);
                    return; // Error
            }

            // Load
            UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip("file://" + fullPath, audioType);
            await request.SendWebRequest();

            if (request.isDone)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                audioSource.clip = clip;
            }
        }
    }

    void HandleUpdate()
    {
        if (obj.TryGetFieldPrimitive("audioVolume", out float audioVolume))
        {
            audioSource.volume = audioVolume;
        }

        if (obj.TryGetFieldPrimitive("audioPlaying", out int audioPlaying))
        {
            if ((audioPlaying != 0) && !audioSource.isPlaying)
            {
                audioSource.Play();
            }
            if (!(audioPlaying != 0) && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }

        if (obj.TryGetFieldPrimitive("audioLoop", out int audioLoop))
        {
            audioSource.loop = (audioLoop != 0);
        }
    }

    void OnBeforeSync(SyncObject sender, float dt)
    {
        if (GetComponent<ObjectSync>().IsOriginal)
        {
            obj.SetField("audioPlaying", new Primitive<int>(audioSource.isPlaying ? 1 : 0));
        }
    }

    public void Cleanup(SyncObject obj)
    {
        Destroy(this);
    }

    public void OnDestroy()
    {
        obj.DeleteFieldUpdateHandler("audioFile", HandleFileUpdate);
        obj.DeleteFieldUpdateHandler("audioVolume", HandleUpdate);
        obj.DeleteFieldUpdateHandler("audioPlaying", HandleUpdate);
        obj.DeleteFieldUpdateHandler("audioLoop", HandleUpdate);
        obj.BeforeSync -= OnBeforeSync;
        Destroy(audioSource);
    }
}

}   // end namespace
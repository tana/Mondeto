using UnityEngine;

public class WorldTag : MonoBehaviour, ITag
{
    SyncObject obj;

    Material skyboxMaterial;

    public void Setup(SyncObject syncObject)
    {
        obj = syncObject;

        skyboxMaterial = new Material(Shader.Find("Skybox/Panoramic"));

        obj.RegisterFieldUpdateHandler("skyboxTexture", HandleSkyboxTextureUpdate);
        obj.RegisterFieldUpdateHandler("skyboxType", HandleUpdate);

        HandleUpdate();
        HandleSkyboxTextureUpdate();
    }

    async void HandleSkyboxTextureUpdate()
    {
        if (obj.TryGetField("skyboxTexture", out BlobHandle textureHandle))
        {
            Blob blob = await obj.Node.ReadBlob(textureHandle);
            // Texture size is automatically updated by LoadImage,
            // so we can specify arbitrary value when creating a Texture2D.
            //  See: https://docs.unity3d.com/ja/2019.4/ScriptReference/ImageConversion.LoadImage.html
            var texture = new Texture2D(1, 1);
            texture.LoadImage(blob.Data);
            // Use WrapMode.Clamp to suppress lines of texture edges
            // See:
            //  https://docs.unity3d.com/ja/2018.4/Manual/HOWTO-UseSkybox.html
            //  https://sat-box.hatenablog.jp/entry/2017/09/11/143801
            // (FIXME: Not suppressed yet?)
            texture.wrapMode = TextureWrapMode.Clamp;

            skyboxMaterial.mainTexture = texture;
            obj.WriteLog("WorldTag", "Skybox texture loaded");

            // Use this skybox texture for both background and ambient light
            // See: https://docs.unity3d.com/ja/2019.4/Manual/skyboxes-using.html
            RenderSettings.skybox = skyboxMaterial;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        }
    }

    void HandleUpdate()
    {
        // TODO: support skybox types other than panoramic
    }

    public void Cleanup(SyncObject obj)
    {
        Destroy(this);
    }

    void OnDestroy()
    {
        obj.DeleteFieldUpdateHandler("skyboxTexture", HandleSkyboxTextureUpdate);
        obj.DeleteFieldUpdateHandler("skyboxType", HandleUpdate);
    }
}
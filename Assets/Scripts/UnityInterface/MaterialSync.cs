using UnityEngine;

public class MaterialSync : MonoBehaviour, ITag
{
    SyncObject obj;

    Renderer meshRenderer;

    public void Setup(SyncObject obj)
    {
        this.obj = obj;
        meshRenderer = GetComponent<MeshRenderer>();

        obj.RegisterFieldUpdateHandler("color", HandleUpdate);
        obj.RegisterFieldUpdateHandler("alpha", HandleUpdate);
        obj.RegisterFieldUpdateHandler("texture", HandleTextureUpdate);
        obj.RegisterFieldUpdateHandler("shader", HandleShaderUpdate);

        HandleUpdate();
        HandleTextureUpdate();
        HandleShaderUpdate();
    }

    void HandleUpdate()
    {
        var color = new Color(0.0f, 0.0f, 0.0f, 1.0f);
        
        if (obj.TryGetField("color", out Vec colorVec))
        {
            color.r = colorVec.X;
            color.g = colorVec.Y;
            color.b = colorVec.Z;
        }

        // TODO: currently alpha does not work
        if (obj.TryGetFieldPrimitive("alpha", out float alpha))
        {
            color.a = alpha;
        }

        meshRenderer.material.color = color;
    }

    async void HandleTextureUpdate()
    {
        if (obj.TryGetField("texture", out BlobHandle blobHandle))
        {
            Blob blob = await obj.Node.ReadBlob(blobHandle);
            // Texture size is automatically updated by LoadImage,
            // so we can specify arbitrary value when creating aTexture2D.
            //  See: https://docs.unity3d.com/ja/2019.4/ScriptReference/ImageConversion.LoadImage.html
            var texture = new Texture2D(1, 1);
            texture.LoadImage(blob.Data);
            meshRenderer.material.mainTexture = texture;
            obj.WriteLog("MaterialSync", "Texture loaded");
        }
    }

    void HandleShaderUpdate()
    {
        if (obj.TryGetFieldPrimitive("shader", out string shader))
        {
            string shaderName;
            switch (shader)
            {
                case "unlit":
                    shaderName = "Unlit/Texture";
                    break;
                case "standard":
                default:
                    shaderName = "Standard";
                    break;
            }

            meshRenderer.material.shader = Shader.Find(shaderName);
        }
    }

    public void Cleanup(SyncObject obj)
    {
        Destroy(this);
    }

    public void OnDestroy()
    {
        obj.DeleteFieldUpdateHandler("color", HandleUpdate);
        obj.DeleteFieldUpdateHandler("alpha", HandleUpdate);
        obj.DeleteFieldUpdateHandler("texture", HandleTextureUpdate);
        obj.DeleteFieldUpdateHandler("shader", HandleShaderUpdate);
    }
}
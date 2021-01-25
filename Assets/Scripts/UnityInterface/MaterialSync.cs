using UnityEngine;

public class MaterialSync : MonoBehaviour, ITag
{
    enum ShaderType
    {
        Standard,
        Unlit
    }

    enum RenderMode
    {
        Opaque,
        AlphaCutoff,
        Transparent
    }

    SyncObject obj;

    Renderer theRenderer;

    ShaderType shaderType = ShaderType.Standard;
    RenderMode renderMode = RenderMode.Opaque;

    public void Setup(SyncObject obj)
    {
        this.obj = obj;
        theRenderer = GetComponent<Renderer>();
        UpdateBaseMaterial();

        obj.RegisterFieldUpdateHandler("color", HandleUpdate);
        obj.RegisterFieldUpdateHandler("alpha", HandleUpdate);
        obj.RegisterFieldUpdateHandler("metallic", HandleUpdate);
        obj.RegisterFieldUpdateHandler("smoothness", HandleUpdate);
        obj.RegisterFieldUpdateHandler("texture", HandleTextureUpdate);
        obj.RegisterFieldUpdateHandler("shader", HandleShaderUpdate);
        obj.RegisterFieldUpdateHandler("renderMode", HandleRenderModeUpdate);

        HandleUpdate();
        HandleTextureUpdate();
        HandleShaderUpdate();
        HandleRenderModeUpdate();
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

        if (obj.TryGetFieldPrimitive("alpha", out float alpha))
        {
            color.a = alpha;
        }

        theRenderer.material.color = color;

        if (obj.TryGetFieldPrimitive("metallic", out float metallic))
        {
            theRenderer.material.SetFloat("_Metallic", metallic);
        }

        if (obj.TryGetFieldPrimitive("smoothness", out float smoothness))
        {
            theRenderer.material.SetFloat("_Glossiness", smoothness);
        }
    }

    async void HandleTextureUpdate()
    {
        if (obj.TryGetField("texture", out BlobHandle blobHandle))
        {
            Blob blob = await obj.Node.ReadBlob(blobHandle);
            // Texture size is automatically updated by LoadImage,
            // so we can specify arbitrary value when creating a Texture2D.
            //  See: https://docs.unity3d.com/ja/2019.4/ScriptReference/ImageConversion.LoadImage.html
            var texture = new Texture2D(1, 1);
            texture.LoadImage(blob.Data);
            theRenderer.material.mainTexture = texture;
            obj.WriteLog("MaterialSync", "Texture loaded");
        }
    }

    void HandleShaderUpdate()
    {
        if (obj.TryGetFieldPrimitive("shader", out string shader))
        {
            switch (shader)
            {
                case "unlit":
                    shaderType = ShaderType.Unlit;
                    break;
                case "standard":
                default:
                    shaderType = ShaderType.Standard;
                    break;
            }

            UpdateBaseMaterial();
        }
    }

    void HandleRenderModeUpdate()
    {
        if (obj.TryGetFieldPrimitive("renderMode", out string renderModeStr))
        {
            switch (renderModeStr)
            {
                case "alphaCutoff":
                    renderMode = RenderMode.AlphaCutoff;
                    break;
                case "transparent":
                    renderMode = RenderMode.Transparent;
                    break;
                case "opaque":
                default:
                    renderMode = RenderMode.Opaque;
                    break;
            }

            UpdateBaseMaterial();
        }
    }

    void UpdateBaseMaterial()
    {
        string shader = "Standard";
        switch (shaderType)
        {
            case ShaderType.Standard:
                shader = "Standard";
                break;
            case ShaderType.Unlit:
                shader = "Unlit";
                break;
        }

        string mode = "Opaque";
        switch (renderMode)
        {
            case RenderMode.Opaque:
                mode = "Opaque";
                break;
            case RenderMode.AlphaCutoff:    // TODO:
            case RenderMode.Transparent:
                mode = "Transparent";
                break;
        }

        var material = Instantiate(Resources.Load<Material>($"Materials/{shader}{mode}"));
        // Copy properties
        material.color = theRenderer.material.color;
        material.mainTexture = theRenderer.material.mainTexture;
        if (material.HasProperty("_Metallic") && theRenderer.material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", theRenderer.material.GetFloat("_Metallic"));
        if (material.HasProperty("_Glossiness") && theRenderer.material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", theRenderer.material.GetFloat("_Glossiness"));

        theRenderer.material = material;
    }

    public void Cleanup(SyncObject obj)
    {
        Destroy(this);
    }

    public void OnDestroy()
    {
        obj.DeleteFieldUpdateHandler("color", HandleUpdate);
        obj.DeleteFieldUpdateHandler("alpha", HandleUpdate);
        obj.DeleteFieldUpdateHandler("metallic", HandleUpdate);
        obj.DeleteFieldUpdateHandler("smoothness", HandleUpdate);
        obj.DeleteFieldUpdateHandler("texture", HandleTextureUpdate);
        obj.DeleteFieldUpdateHandler("shader", HandleShaderUpdate);
        obj.DeleteFieldUpdateHandler("renderMode", HandleRenderModeUpdate);
    }
}
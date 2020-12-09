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

        HandleUpdate();
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

    public void Cleanup(SyncObject obj)
    {
        Destroy(this);
    }

    public void OnDestroy()
    {
        obj.DeleteFieldUpdateHandler("color", HandleUpdate);
        obj.DeleteFieldUpdateHandler("alpha", HandleUpdate);
    }
}
using UnityEngine;

public class MaterialSync : MonoBehaviour
{
    Renderer meshRenderer;

    public void Initialize(SyncObject obj)
    {
        meshRenderer = GetComponent<MeshRenderer>();

        obj.RegisterFieldUpdateHandler("color", () => ApplyFieldValues(obj));
        obj.RegisterFieldUpdateHandler("alpha", () => ApplyFieldValues(obj));

        ApplyFieldValues(obj);
    }

    void ApplyFieldValues(SyncObject obj)
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
}
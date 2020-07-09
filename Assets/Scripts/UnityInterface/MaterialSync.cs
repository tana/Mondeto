using UnityEngine;

public class MaterialSync : MonoBehaviour
{
    Renderer meshRenderer;

    public void Initialize(SyncObject obj)
    {
        meshRenderer = GetComponent<MeshRenderer>();

        obj.BeforeSync += OnBeforeSync;
        obj.AfterSync += OnAfterSync;

        ApplyState(obj);
    }

    void ApplyState(SyncObject obj)
    {
        var color = new Color(0.0f, 0.0f, 0.0f, 1.0f);
        
        if (obj.HasField("color") && obj.GetField("color") is Vec colorVec)
        {
            color.r = colorVec.X;
            color.g = colorVec.Y;
            color.b = colorVec.Z;
        }

        // TODO: currently alpha does not work
        if (obj.HasField("alpha") && obj.GetField("alpha") is Primitive<float> alpha)
        {
            color.a = alpha.Value;
        }

        meshRenderer.material.color = color;
    }

    public void OnBeforeSync(SyncObject obj)
    {
        Color color = meshRenderer.material.color;

        obj.SetField("color", new Vec(color.r, color.g, color.b));
        obj.SetField("alpha", new Primitive<float>(color.a));
    }

    public void OnAfterSync(SyncObject obj)
    {
        if (GetComponent<ObjectSync>().IsOriginal) return;

        ApplyState(obj);
    }
}
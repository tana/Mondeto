using UnityEngine;

public class TextSync : MonoBehaviour
{
    SyncObject obj;
    TextMesh textMesh;

    public void Initialize(SyncObject obj)
    {
        this.obj = obj;
        textMesh = gameObject.AddComponent<TextMesh>();
        textMesh.richText = false;
        // Replace shader to Z-test enabled one (slightly modified version of standard font shader)
        // See: https://kan-kikuchi.hatenablog.com/entry/TextMesh_Transparent
        GetComponent<MeshRenderer>().material.shader = Resources.Load<Shader>("Shaders/Font_ZTest");

        obj.RegisterFieldUpdateHandler("text", HandleUpdate);
        obj.RegisterFieldUpdateHandler("fontSize", HandleUpdate);
        obj.RegisterFieldUpdateHandler("useRichText", HandleUpdate);
        HandleUpdate();
    }

    void HandleUpdate()
    {
        if (obj.TryGetFieldPrimitive("text", out string text))
        {
            textMesh.text = text;
        }
        if (obj.TryGetFieldPrimitive("fontSize", out int fontSize))
        {
            textMesh.fontSize = fontSize;
        }
        if (obj.TryGetFieldPrimitive("useRichText", out int useRichText))
        {
            textMesh.richText = (useRichText != 0);
        }
    }

    public void OnDestroy()
    {
        obj.DeleteFieldUpdateHandler("text", HandleUpdate);
        obj.DeleteFieldUpdateHandler("fontSize", HandleUpdate);
        obj.DeleteFieldUpdateHandler("useRichText", HandleUpdate);
    }
}
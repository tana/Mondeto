using UnityEngine;

// Tag for rendering a line specified by field value
public class LineTag : MonoBehaviour, ITag
{
    SyncObject obj;

    LineRenderer lineRenderer;

    public void Setup(SyncObject syncObject)
    {
        obj = syncObject;

        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false; // points are specified in local coordinate

        obj.RegisterFieldUpdateHandler("points", HandlePointsUpdate);
        obj.RegisterFieldUpdateHandler("width", HandleUpdate);

        HandlePointsUpdate();
        HandleUpdate();
    }

    void HandleUpdate()
    {
        if (obj.TryGetFieldPrimitive("width", out float width))
        {
            lineRenderer.widthMultiplier = width;
        }
    }

    void HandlePointsUpdate()
    {
        if (obj.TryGetField("points", out Sequence pointsSeq))
        {
            Vector3[] points = UnityUtil.VecSequenceToUnity(pointsSeq);
            // positionCount have to be updated Before calling SetPositions
            // See: https://docs.unity3d.com/2019.4/Documentation/ScriptReference/LineRenderer.SetPositions.html
            lineRenderer.positionCount = points.Length;
            lineRenderer.SetPositions(points);
        }
    }

    public void Cleanup(SyncObject syncObject)
    {
        obj.DeleteFieldUpdateHandler("points", HandlePointsUpdate);
        obj.DeleteFieldUpdateHandler("width", HandleUpdate);
    }
}
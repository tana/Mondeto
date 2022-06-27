using UnityEngine;
using Mondeto.Core;

namespace Mondeto
{

// Tag for rendering 3D mesh specified by field value
public class MeshTag : MonoBehaviour, ITag
{
    SyncObject obj;

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;

    bool updated = false;
    Sequence verticesSeq, indicesSeq, normalsSeq;

    public void Setup(SyncObject syncObject)
    {
        obj = syncObject;

        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();

        obj.RegisterFieldUpdateHandler("vertices", HandleUpdate);
        obj.RegisterFieldUpdateHandler("indices", HandleUpdate);

        HandleUpdate();
    }

    void HandleUpdate()
    {
        // vertices and indices are required
        if (obj.TryGetField<Sequence>("vertices", out verticesSeq) &&
            obj.TryGetField<Sequence>("indices", out indicesSeq))
        {
            updated = true;

            // normals are optional
            if (!obj.TryGetField<Sequence>("normals", out normalsSeq))
            {
                normalsSeq = null;  // null indicate normals was not specified
            }
        }
    }

    void Update()
    {
        // Actual mesh update is done during Unity's Update
        if (updated)
        {
            updated = false;
            Mesh mesh = meshFilter.mesh;

            // Modify properties of a Mesh object to dynamically generate mesh from a program
            //  See: https://docs.unity3d.com/ja/2019.4/ScriptReference/Mesh.html

            mesh.vertices = UnityUtil.VecSequenceToUnity(verticesSeq);
            if (normalsSeq != null)
            {
                mesh.normals = UnityUtil.VecSequenceToUnity(normalsSeq);
            }
            mesh.triangles = UnityUtil.IntSequenceToUnity(indicesSeq);

            if (normalsSeq == null) // Automatic normal calculation
            {
                mesh.RecalculateNormals();
            }
        }
    }

    public void Cleanup(SyncObject syncObject)
    {
        obj.DeleteFieldUpdateHandler("vertices", HandleUpdate);
        obj.DeleteFieldUpdateHandler("indices", HandleUpdate);
    }
}

}   // end namespace
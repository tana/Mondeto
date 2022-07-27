using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mondeto.Core;

namespace Mondeto
{

public class ModelSync : MonoBehaviour, ITag
{
    public delegate void LoadCompleteDelegate(ModelSync ms);
    public event LoadCompleteDelegate LoadComplete;
    public bool Loaded = false;

    List<GameObject> meshes;

    public async void Setup(SyncObject obj)
    {
        // TODO: dynamic model change handling
        if (!obj.HasField("model") || !(obj.GetField("model") is BlobHandle))
        {
            // TODO:
            obj.WriteErrorLog("Model", $"This object has no model field or not a blob handle. Ignoring.");
            return;
        }

        BlobHandle handle = (BlobHandle)obj.GetField("model");

        Blob blob = await obj.Node.ReadBlob(handle);
        obj.WriteDebugLog("Model", $"Blob {handle} loaded");

        // Because UniGLTF.ImporterContext is the parent class of VRMImporterContext,
        // loading procedure is probably almost same (See PlyayerAvatar.cs for VRM loading).
        // https://vrm-c.github.io/UniVRM/ja/api/sample/SimpleViewer.html
        // https://github.com/vrm-c/UniVRM/blob/e91ab9fc519aa387dc9b39044aa2189ff0382f15/Assets/VRM_Samples/SimpleViewer/ViewerUI.cs
        // Currently, only GLB (glTF binary format) is supported because it is self-contained
        try
        {
            UniGLTF.GltfData gltf = new UniGLTF.GlbBinaryParser(blob.Data, blob.ToString()).Parse();
            using (var ctx = new UniGLTF.ImporterContext(gltf))
            {
                UniGLTF.RuntimeGltfInstance instance = await ctx.LoadAsync(new VRMShaders.ImmediateCaller());   // This is not actually async because of ImmediateCaller

                // Move the model inside this gameObject
                instance.Root.transform.SetParent(transform);
                instance.Root.transform.localPosition = Vector3.zero;
                instance.Root.transform.localRotation = Quaternion.identity;

                instance.ShowMeshes();

                // For GetMeshes
                meshes = instance.Nodes.Select(tf => tf.gameObject).ToList();
            }
        }
        catch (System.Exception e)
        {
            obj.WriteErrorLog("Model", e.ToString());
        }

        obj.WriteDebugLog("Model", "Model load completed");

        LoadComplete?.Invoke(this);
        Loaded = true;
    }

    public List<GameObject> GetMeshes()
    {
        return meshes;
    }

    public void Cleanup(SyncObject syncObject)
    {
    }
}

}   // end namespace
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class ModelSync : MonoBehaviour
{
    public delegate void LoadCompleteDelegate(ModelSync ms);
    public event LoadCompleteDelegate LoadComplete;

    UniGLTF.ImporterContext ctx;

    public async void Initialize(SyncObject obj)
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
        //  ( https://github.com/vrm-c/UniVRM/blob/3b68eb7f99bfe78ea9c83ea75511282ef1782f1a/Assets/VRM/UniVRM/Scripts/Format/VRMImporterContext.cs#L11 )
        // loading procedure is probably almost same (See PlyayerAvatar.cs for VRM loading).
        //  https://github.com/vrm-c/UniVRM/blob/3b68eb7f99bfe78ea9c83ea75511282ef1782f1a/Assets/VRM/UniGLTF/Editor/Tests/UniGLTFTests.cs#L46
        ctx = new UniGLTF.ImporterContext();
        // ParseGlb parses GLB file.
        //  https://github.com/vrm-c/UniVRM/blob/3b68eb7f99bfe78ea9c83ea75511282ef1782f1a/Assets/VRM/UniGLTF/Scripts/IO/ImporterContext.cs#L239
        // Currently, only GLB (glTF binary format) is supported because it is self-contained
        ctx.ParseGlb(blob.Data);
        ctx.Root = gameObject;
        await ctx.LoadAsyncTask();
        // UniGLTF also has ShowMeshes https://github.com/ousttrue/UniGLTF/wiki/Rutime-API#import
        ctx.ShowMeshes();

        obj.WriteDebugLog("Model", "Model load completed");

        LoadComplete?.Invoke(this);
    }

    public List<GameObject> GetMeshes()
    {
        if (ctx == null)
        {
            return new List<GameObject>();
        }
        else
        {
            return ctx.Nodes.Select(tf => tf.gameObject).ToList();
        }
    }

    public void OnDestroy()
    {
        ctx.Dispose();
    }
}
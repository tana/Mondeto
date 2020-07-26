using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ObjectSync : MonoBehaviour
{
    public string InitialTags = "";

    // If ObjectSync is created programatically, this flag prevents overwriting already specified tags
    public bool SetInitialTags = false; 

    public GameObject NetManager;

    public bool IsOriginal;

    public SyncObject SyncObject;

    SyncBehaviour syncBehaviour
    {
        get
        {
            if (syncBehaviourCache == null) syncBehaviourCache = NetManager.GetComponent<SyncBehaviour>();
            return syncBehaviourCache;
        }
    }
    SyncBehaviour syncBehaviourCache;
    
    public SyncNode Node { get => syncBehaviour.Node; }

    public void Initialize(SyncObject obj)
    {
        SyncObject = obj;

        SyncObject.BeforeSync += OnBeforeSync;
        SyncObject.AfterSync += OnAfterSync;
        SyncObject.RegisterFieldUpdateHandler("parent", HandleParentChange);

        if (IsOriginal && SetInitialTags)
        {
            SyncObject.SetField("tags", new Sequence { 
                Elements = InitialTags.Split(' ').Where(str => str.Length > 0).Select(tag => (IValue)(new Primitive<string> { Value = tag })).ToList()
            });
        }

        HandleParentChange();

        // TODO: consider better design
        ApplyState(); // Set initial state of Unity GameObject based on SyncObject

        SendMessage("OnSyncReady", options: SendMessageOptions.DontRequireReceiver);
        return;
    }

    void OnDestroy()
    {
        SyncObject.BeforeSync -= OnBeforeSync;
        SyncObject.AfterSync -= OnAfterSync;
        SyncObject.DeleteFieldUpdateHandler("parent", HandleParentChange);
    }

    void HandleParentChange()
    {
        if (SyncObject.TryGetField("parent", out ObjectRef parentRef))
        {
            SyncObject parentObj = Node.Objects[parentRef.Id];

            Sequence children;
            if (parentObj.TryGetField("children", out Sequence oldChildren))
                children = oldChildren;
            else
                children = new Sequence();

            children.Elements.Add(SyncObject.GetObjectRef());
            parentObj.SetField("children", children);

            if (syncBehaviour.GameObjects.ContainsKey(parentRef.Id))
            {
                GameObject parentGameObj = syncBehaviour.GameObjects[parentRef.Id];
                transform.SetParent(parentGameObj.transform, true);
            }
        }
    }

    void OnBeforeSync(SyncObject obj)
    {
        obj.SetField("position", UnityUtil.ToVec(transform.localPosition));
        obj.SetField("rotation", UnityUtil.ToQuat(transform.localRotation));
    }

    void OnAfterSync(SyncObject obj)
    {
        if (IsOriginal) return;
        ApplyState();
    }

    public void ApplyState()   // TODO: move
    {
        if (SyncObject.HasField("position") && SyncObject.GetField("position") is Vec position)
            transform.localPosition = UnityUtil.FromVec(position);
        if (SyncObject.HasField("rotation") && SyncObject.GetField("rotation") is Quat rotation)
            transform.localRotation = UnityUtil.FromQuat(rotation);
        if (SyncObject.HasField("scale") && SyncObject.GetField("scale") is Vec scale)
            transform.localScale = UnityUtil.FromVec(scale);
    }
}

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

    bool beforeSync = false;

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
        SyncObject.RegisterFieldUpdateHandler("parent", HandleParentChange);
        SyncObject.RegisterFieldUpdateHandler("position", HandleUpdate);
        SyncObject.RegisterFieldUpdateHandler("rotation", HandleUpdate);
        SyncObject.RegisterFieldUpdateHandler("scale", HandleUpdate);

        if (IsOriginal && SetInitialTags)
        {
            SyncObject.SetField("tags", new Sequence { 
                Elements = InitialTags.Split(' ').Where(str => str.Length > 0).Select(tag => (IValue)(new Primitive<string> { Value = tag })).ToList()
            });
        }

        HandleParentChange();
        HandleUpdate();

        SendMessage("OnSyncReady", options: SendMessageOptions.DontRequireReceiver);
        return;
    }

    void OnDestroy()
    {
        SyncObject.BeforeSync -= OnBeforeSync;
        SyncObject.DeleteFieldUpdateHandler("parent", HandleParentChange);
        SyncObject.DeleteFieldUpdateHandler("position", HandleUpdate);
        SyncObject.DeleteFieldUpdateHandler("rotation", HandleUpdate);
        SyncObject.DeleteFieldUpdateHandler("scale", HandleUpdate);
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

    void OnBeforeSync(SyncObject obj, float dt)
    {
        beforeSync = true;

        obj.SetField("position", UnityUtil.ToVec(transform.localPosition));
        obj.SetField("rotation", UnityUtil.ToQuat(transform.localRotation));
        
        beforeSync = false;
    }

    void HandleUpdate()
    {
        if (beforeSync) return;

        if (SyncObject.TryGetField("position", out Vec position))
            transform.localPosition = UnityUtil.FromVec(position);
        if (SyncObject.TryGetField("rotation", out Quat rotation))
            transform.localRotation = UnityUtil.FromQuat(rotation);
        if (SyncObject.TryGetField("scale", out Vec scale))
            transform.localScale = UnityUtil.FromVec(scale);
    }
}

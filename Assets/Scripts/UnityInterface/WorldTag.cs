using UnityEngine;

public class WorldTag : MonoBehaviour, ITag
{
    SyncObject obj;

    public void Setup(SyncObject syncObject)
    {
        obj = syncObject;

        obj.WriteLog("WorldTag", "world tag setup completed");
    }

    public void Cleanup(SyncObject obj)
    {
        Destroy(this);
    }

    void OnDestroy()
    {
    }
}
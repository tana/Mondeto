using UnityEngine;

// Dummy MonoBehaviour/ITag for primitives
public class PrimitiveTag : MonoBehaviour, ITag
{
    public void Setup(SyncObject syncObject) {}
    public void Cleanup(SyncObject syncObject) {}
}
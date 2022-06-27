using UnityEngine;
using Mondeto.Core;

namespace Mondeto
{

// Dummy MonoBehaviour/ITag for primitives
public class PrimitiveTag : MonoBehaviour, ITag
{
    public void Setup(SyncObject syncObject) {}
    public void Cleanup(SyncObject syncObject) {}
}

}   // end namespace
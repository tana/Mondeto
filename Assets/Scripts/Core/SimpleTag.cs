// Abstract base class for simple (Unity-independent) tags
public abstract class SimpleTag : ITag
{
    public SimpleTag()
    {
    }

    public abstract void Setup(SyncObject syncObject);
    public abstract void Cleanup(SyncObject syncObject);
}